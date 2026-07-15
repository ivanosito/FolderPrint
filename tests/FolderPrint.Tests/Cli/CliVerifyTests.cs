using System.Security.Cryptography;
using System.Text;
using FolderPrint.Cli;
using FolderPrint.Core.Catalog;
using FolderPrint.Core.Models;
using FolderPrint.Core.Verification;

namespace FolderPrint.Tests.Cli;

public sealed class CliVerifyTests
{
    private static readonly DateTimeOffset Created = new(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Scanned = new(2026, 7, 15, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Run_VerifyUnchangedFolder_PrintsCleanPersistsTimestampAndReturnsSuccess()
    {
        using var fixture = new VerifyFixture([Fingerprint("same.txt", "same")]);
        var scanCalls = 0;
        var snapshot = fixture.Snapshot([Fingerprint("same.txt", "same")]);
        var runner = fixture.Runner(_ => { scanCalls++; return snapshot; });
        var requestedPath = OperatingSystem.IsWindows()
            ? fixture.RootPath.ToUpperInvariant() + Path.DirectorySeparatorChar
            : fixture.RootPath + Path.DirectorySeparatorChar;

        var exitCode = runner.Run(["verify", requestedPath]);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(1, scanCalls);
        Assert.Contains("Result: Clean", fixture.Output.ToString());
        Assert.Equal(string.Empty, fixture.Error.ToString());
        var persisted = Assert.Single(fixture.Store.Load().Catalog!.RegisteredFolders);
        Assert.Equal(Scanned, persisted.LastVerifiedAtUtc);
        Assert.Equal("folder-id", persisted.Id);
        Assert.Equal(Created, persisted.CreatedAtUtc);
        Assert.Equal([Fingerprint("same.txt", "same")], persisted.Files);
    }

    [Fact]
    public void Run_VerifyMixedDifferences_PrintsEveryCategoryPersistsTimestampAndReturnsDifferences()
    {
        var baseline = new[]
        {
            Fingerprint("same.txt", "same"),
            Fingerprint("modified.txt", "old"),
            Fingerprint("missing.txt", "missing"),
            Fingerprint("old-name.txt", "move"),
            Fingerprint("amb-old-b.txt", "shared"),
            Fingerprint("amb-old-a.txt", "shared")
        };
        using var fixture = new VerifyFixture(baseline);
        var current = new[]
        {
            Fingerprint("amb-new-b.txt", "shared"),
            Fingerprint("new.txt", "new"),
            Fingerprint("new-name.txt", "move"),
            Fingerprint("modified.txt", "changed"),
            Fingerprint("same.txt", "same"),
            Fingerprint("amb-new-a.txt", "shared")
        };

        var exitCode = fixture.Runner(_ => fixture.Snapshot(current, ["locked.txt"]))
            .Run(["verify", fixture.RootPath]);

        var output = fixture.Output.ToString();
        Assert.Equal(ExitCodes.DifferencesFound, exitCode);
        Assert.Contains("Result: Differences found", output);
        Assert.Contains("[Unchanged]", output);
        Assert.Contains("[Modified]", output);
        Assert.Contains("[Missing]", output);
        Assert.Contains("[New]", output);
        Assert.Contains("[Moved/Renamed]", output);
        Assert.Contains("old-name.txt -> new-name.txt", output);
        Assert.Contains("[Ambiguous Moved/Renamed]", output);
        Assert.Contains("[Duplicate Groups]", output);
        Assert.Contains("[Unreadable]", output);
        Assert.Contains("locked.txt", output);
        Assert.Equal(Scanned, Assert.Single(fixture.Store.Load().Catalog!.RegisteredFolders).LastVerifiedAtUtc);
    }

    [Fact]
    public void Run_VerifyDuplicateOnlyOrUnreadableOnly_ReturnsDifferences()
    {
        var duplicateFiles = new[] { Fingerprint("a.txt", "shared"), Fingerprint("b.txt", "shared") };
        using var duplicates = new VerifyFixture(duplicateFiles);
        using var unreadable = new VerifyFixture([]);

        var duplicateExit = duplicates.Runner(_ => duplicates.Snapshot(duplicateFiles))
            .Run(["verify", duplicates.RootPath]);
        var unreadableExit = unreadable.Runner(_ => unreadable.Snapshot([], ["locked.txt"]))
            .Run(["verify", unreadable.RootPath]);

        Assert.Equal(ExitCodes.DifferencesFound, duplicateExit);
        Assert.Contains("Duplicate groups: 1", duplicates.Output.ToString());
        Assert.Equal(ExitCodes.DifferencesFound, unreadableExit);
        Assert.Contains("Unreadable: 1", unreadable.Output.ToString());
    }

    [Fact]
    public void Run_VerifyUnregisteredFolder_ReturnsNotFoundBeforeScanOrSave()
    {
        using var fixture = new VerifyFixture([], register: false);
        var scanCalls = 0;
        var runner = fixture.Runner(_ => { scanCalls++; return fixture.Snapshot([]); });

        var exitCode = runner.Run(["verify", fixture.RootPath]);

        Assert.Equal(ExitCodes.NotFound, exitCode);
        Assert.Equal(0, scanCalls);
        Assert.Equal(string.Empty, fixture.Output.ToString());
        Assert.Contains("not registered", fixture.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(fixture.CatalogPath));
    }

    [Fact]
    public void Run_VerifyMissingRootOrFileRoot_ReturnsNotFoundWithoutScanOrTimestamp()
    {
        using var missing = new VerifyFixture([]);
        Directory.Delete(missing.RootPath);
        using var file = new VerifyFixture([], rootIsFile: true);
        var scanCalls = 0;

        var missingExit = missing.Runner(_ => { scanCalls++; return missing.Snapshot([]); })
            .Run(["verify", missing.RootPath]);
        var fileExit = file.Runner(_ => { scanCalls++; return file.Snapshot([]); })
            .Run(["verify", file.RootPath]);

        Assert.Equal(ExitCodes.NotFound, missingExit);
        Assert.Equal(ExitCodes.NotFound, fileExit);
        Assert.Equal(0, scanCalls);
        Assert.Null(Assert.Single(missing.Store.Load().Catalog!.RegisteredFolders).LastVerifiedAtUtc);
        Assert.Null(Assert.Single(file.Store.Load().Catalog!.RegisteredFolders).LastVerifiedAtUtc);
    }

    [Fact]
    public void Run_VerifyMalformedPath_ReturnsNotFoundWithoutLoadingOrScanning()
    {
        using var fixture = new VerifyFixture([], register: false);
        var scanCalls = 0;

        var exitCode = fixture.Runner(_ => { scanCalls++; return fixture.Snapshot([]); })
            .Run(["verify", "\0"]);

        Assert.Equal(ExitCodes.NotFound, exitCode);
        Assert.Equal(0, scanCalls);
        Assert.Contains("invalid", fixture.Error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Run_VerifyMalformedCatalog_ReturnsCatalogErrorWithoutScan()
    {
        using var fixture = new VerifyFixture([], register: false);
        Directory.CreateDirectory(Path.GetDirectoryName(fixture.CatalogPath)!);
        File.WriteAllText(fixture.CatalogPath, "malformed");
        var scanCalls = 0;

        var exitCode = fixture.Runner(_ => { scanCalls++; return fixture.Snapshot([]); })
            .Run(["verify", fixture.RootPath]);

        Assert.Equal(ExitCodes.CatalogError, exitCode);
        Assert.Equal(0, scanCalls);
        Assert.Equal(string.Empty, fixture.Output.ToString());
    }

    [Fact]
    public void Run_VerifyTraversalFailure_ReturnsScanErrorWithoutTimestamp()
    {
        using var fixture = new VerifyFixture([]);

        var exitCode = fixture.Runner(_ => throw new IOException("scan failed"))
            .Run(["verify", fixture.RootPath]);

        Assert.Equal(ExitCodes.ScanError, exitCode);
        Assert.Equal(string.Empty, fixture.Output.ToString());
        Assert.Contains("scan", fixture.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Null(Assert.Single(fixture.Store.Load().Catalog!.RegisteredFolders).LastVerifiedAtUtc);
    }

    [Fact]
    public void Run_VerifyChildDisappearsDuringTraversal_ReturnsScanErrorWhileRootStillExists()
    {
        using var fixture = new VerifyFixture([]);

        var exitCode = fixture.Runner(_ => throw new DirectoryNotFoundException("child disappeared"))
            .Run(["verify", fixture.RootPath]);

        Assert.Equal(ExitCodes.ScanError, exitCode);
        Assert.Contains("scan", fixture.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Null(Assert.Single(fixture.Store.Load().Catalog!.RegisteredFolders).LastVerifiedAtUtc);
    }

    [Fact]
    public void Run_VerifyCatalogChangesAfterLoad_ReturnsCatalogErrorAndPreservesConcurrentChange()
    {
        using var fixture = new VerifyFixture([]);
        var concurrentFolder = new RegisteredFolder(
            "concurrent-id",
            Path.Combine(Path.GetDirectoryName(fixture.RootPath)!, "other"),
            Created,
            null,
            []);
        var runner = fixture.Runner(
            _ => fixture.Snapshot([]),
            (baseline, snapshot) =>
            {
                Assert.True(fixture.Store.Save(new IntegrityCatalog([baseline, concurrentFolder])).IsSuccess);
                return new VerificationService().Compare(baseline, snapshot);
            });

        var exitCode = runner.Run(["verify", fixture.RootPath]);

        Assert.Equal(ExitCodes.CatalogError, exitCode);
        Assert.Equal(string.Empty, fixture.Output.ToString());
        Assert.Contains("changed", fixture.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        var persisted = fixture.Store.Load().Catalog!.RegisteredFolders;
        Assert.Equal(2, persisted.Count);
        Assert.Null(persisted[0].LastVerifiedAtUtc);
        Assert.Equal("concurrent-id", persisted[1].Id);
    }

    [Fact]
    public void Run_VerifyUnexpectedComparisonFailure_ReturnsUnexpectedErrorWithoutTimestamp()
    {
        using var fixture = new VerifyFixture([]);
        var runner = fixture.Runner(
            _ => fixture.Snapshot([]),
            (_, _) => throw new InvalidOperationException("environment-specific detail"));

        var exitCode = runner.Run(["verify", fixture.RootPath]);

        Assert.Equal(ExitCodes.UnexpectedError, exitCode);
        Assert.Equal(string.Empty, fixture.Output.ToString());
        Assert.Contains("Unexpected error", fixture.Error.ToString());
        Assert.DoesNotContain("environment-specific", fixture.Error.ToString());
        Assert.Null(Assert.Single(fixture.Store.Load().Catalog!.RegisteredFolders).LastVerifiedAtUtc);
    }

    [Fact]
    public void Run_VerifyTimestampSaveFailure_ReturnsCatalogErrorWithoutSuccessReport()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new VerifyFixture([]);
        using var catalogLock = new FileStream(fixture.CatalogPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var exitCode = fixture.Runner(_ => fixture.Snapshot([])).Run(["verify", fixture.RootPath]);

        Assert.Equal(ExitCodes.CatalogError, exitCode);
        Assert.Equal(string.Empty, fixture.Output.ToString());
        Assert.Contains("catalog", fixture.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        catalogLock.Dispose();
        Assert.Null(Assert.Single(fixture.Store.Load().Catalog!.RegisteredFolders).LastVerifiedAtUtc);
    }

    private static FileFingerprint Fingerprint(string path, string hash) =>
        new(path, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hash))).ToLowerInvariant(), 1, Created);

    private sealed class VerifyFixture : IDisposable
    {
        private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), $"FolderPrint-Verify-{Guid.NewGuid():N}");

        public VerifyFixture(IReadOnlyList<FileFingerprint> baseline, bool register = true, bool rootIsFile = false)
        {
            Directory.CreateDirectory(tempDirectory);
            RootPath = Path.Combine(tempDirectory, "root");
            if (rootIsFile)
            {
                File.WriteAllText(RootPath, "root-file");
            }
            else
            {
                Directory.CreateDirectory(RootPath);
            }

            RootPath = Path.GetFullPath(RootPath);
            CatalogPath = Path.Combine(tempDirectory, "state", "catalog.json");
            Store = new CatalogStore(CatalogPath);
            Output = new StringWriter();
            Error = new StringWriter();

            if (register)
            {
                var registered = new RegisteredFolder("folder-id", RootPath, Created, null, baseline.ToArray());
                Assert.True(Store.Save(new IntegrityCatalog([registered])).IsSuccess);
            }
        }

        public string RootPath { get; private set; }
        public string CatalogPath { get; }
        public CatalogStore Store { get; }
        public StringWriter Output { get; }
        public StringWriter Error { get; }

        public CliRunner Runner(
            Func<string, FolderSnapshot> scan,
            Func<RegisteredFolder, FolderSnapshot, VerificationResult>? compare = null) =>
            new(Store, Output, Error, scan, compare);

        public FolderSnapshot Snapshot(
            IReadOnlyList<FileFingerprint> files,
            IReadOnlyList<string>? unreadable = null) =>
            new(RootPath, Scanned, files, unreadable ?? []);

        public void Dispose()
        {
            Output.Dispose();
            Error.Dispose();
            try { Directory.Delete(tempDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
