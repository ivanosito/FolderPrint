using FolderPrint.Cli;
using FolderPrint.Core.Catalog;
using FolderPrint.Core.Models;
using System.Security.Cryptography;

namespace FolderPrint.Tests.Cli;

public sealed class CliRefreshTests : IDisposable
{
    private static readonly DateTimeOffset Created = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly string tempDirectory = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), $"FolderPrint-Refresh-Cli-{Guid.NewGuid():N}"))
        .FullName;

    [Fact]
    public void Run_RefreshAlias_PersistsBaselineAndWritesExactDeterministicSuccess()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "target")).FullName;
        var fixture = CreateFixture("success");
        var target = Folder("target-id", root, Created.AddMinutes(1));
        fixture.Save(new IntegrityCatalog([target]));
        var refreshedAt = Created.AddHours(1);
        var snapshot = new FolderSnapshot(
            "ignored",
            refreshedAt,
            [Fingerprint("b.txt", 'b'), Fingerprint("a.txt", 'a')],
            []);
        fixture.SetRunner(_ => snapshot, () => refreshedAt);

        var exitCode = fixture.Runner.Run([
            "refresh",
            root.ToUpperInvariant() + Path.DirectorySeparatorChar
        ]);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(
            $"Refreshed folder: {target.RootPath}{Environment.NewLine}Files: 2{Environment.NewLine}",
            fixture.Output.ToString());
        Assert.Equal(string.Empty, fixture.Error.ToString());
        var persisted = Assert.Single(fixture.Store.Load().Catalog!.RegisteredFolders);
        Assert.Equal(target.Id, persisted.Id);
        Assert.Equal(target.RootPath, persisted.RootPath);
        Assert.Equal(target.CreatedAtUtc, persisted.CreatedAtUtc);
        Assert.Equal(refreshedAt, persisted.LastVerifiedAtUtc);
        Assert.Equal(["b.txt", "a.txt"], persisted.Files.Select(file => file.RelativePath));
    }

    [Fact]
    public void Run_Unreadables_WritesExactSortedDiagnosticsAndReturnsScanError()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "unreadable-root")).FullName;
        var fixture = CreateFixture("unreadable");
        fixture.Save(new IntegrityCatalog([Folder("target", root, null)]));
        var original = File.ReadAllBytes(fixture.CatalogPath);
        fixture.SetRunner(path => new FolderSnapshot(path, Created, [], ["z.txt", "a.txt"]));

        var exitCode = fixture.Runner.Run(["refresh", root]);

        Assert.Equal(ExitCodes.ScanError, exitCode);
        Assert.Equal(string.Empty, fixture.Output.ToString());
        Assert.Equal(
            "Folder refresh failed because one or more files could not be read." + Environment.NewLine
            + "Unreadable: a.txt" + Environment.NewLine
            + "Unreadable: z.txt" + Environment.NewLine,
            fixture.Error.ToString());
        Assert.Equal(original, File.ReadAllBytes(fixture.CatalogPath));
    }

    [Fact]
    public void Run_InvalidMissingEmptyOrUnregistered_ReturnsNotFoundWithoutScan()
    {
        var invalid = CreateFixture("invalid");
        var missing = CreateFixture("missing");
        var empty = CreateFixture("empty");
        empty.Save(IntegrityCatalog.Empty);
        var existing = CreateFixture("existing");
        existing.Save(new IntegrityCatalog([
            Folder("other", Path.Combine(tempDirectory, "other"), null)
        ]));
        var scans = 0;
        FolderSnapshot Scan(string _) { scans++; throw new InvalidOperationException("must not scan"); }
        invalid.SetRunner(Scan);
        missing.SetRunner(Scan);
        empty.SetRunner(Scan);
        existing.SetRunner(Scan);

        var invalidExit = invalid.Runner.Run(["refresh", new string((char)0, 1)]);
        var missingExit = missing.Runner.Run(["refresh", Path.Combine(tempDirectory, "requested")]);
        var emptyExit = empty.Runner.Run(["refresh", Path.Combine(tempDirectory, "requested")]);
        var existingExit = existing.Runner.Run(["refresh", Path.Combine(tempDirectory, "requested")]);

        Assert.Equal(ExitCodes.NotFound, invalidExit);
        Assert.Equal(ExitCodes.NotFound, missingExit);
        Assert.Equal(ExitCodes.NotFound, emptyExit);
        Assert.Equal(ExitCodes.NotFound, existingExit);
        Assert.Equal(0, scans);
        Assert.Equal(string.Empty, invalid.Output.ToString());
        Assert.Equal(string.Empty, missing.Output.ToString());
        Assert.False(File.Exists(missing.CatalogPath));
    }

    [Fact]
    public void Run_MalformedAmbiguousOrUnsafeCatalog_ReturnsCatalogErrorBeforeScan()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "catalog-root")).FullName;
        var malformed = CreateFixture("malformed");
        Directory.CreateDirectory(Path.GetDirectoryName(malformed.CatalogPath)!);
        File.WriteAllText(malformed.CatalogPath, "malformed");
        var ambiguous = CreateFixture("ambiguous");
        ambiguous.Save(new IntegrityCatalog([
            Folder("first", root, null),
            Folder("second", root + Path.DirectorySeparatorChar, null)
        ]));
        var unsafeFixture = new Fixture(Path.Combine(root, ".folderprint", "catalog.json"));
        unsafeFixture.Save(new IntegrityCatalog([Folder("target", root, null)]));
        var scans = 0;
        FolderSnapshot Scan(string _) { scans++; throw new InvalidOperationException("must not scan"); }
        malformed.SetRunner(Scan);
        ambiguous.SetRunner(Scan);
        unsafeFixture.SetRunner(Scan);

        var malformedExit = malformed.Runner.Run(["refresh", root]);
        var ambiguousExit = ambiguous.Runner.Run(["refresh", root]);
        var unsafeExit = unsafeFixture.Runner.Run(["refresh", root]);

        Assert.Equal(ExitCodes.CatalogError, malformedExit);
        Assert.Equal(ExitCodes.CatalogError, ambiguousExit);
        Assert.Equal(ExitCodes.CatalogError, unsafeExit);
        Assert.Equal(0, scans);
        Assert.Equal(string.Empty, malformed.Output.ToString());
        Assert.Equal(string.Empty, ambiguous.Output.ToString());
        Assert.Equal(string.Empty, unsafeFixture.Output.ToString());
    }

    [Fact]
    public void Run_ScanException_ReturnsScanErrorWithoutSuccess()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "scan-root")).FullName;
        var fixture = CreateFixture("scan");
        fixture.Save(new IntegrityCatalog([Folder("target", root, null)]));
        var original = File.ReadAllBytes(fixture.CatalogPath);
        fixture.SetRunner(_ => throw new IOException("scan broke"));

        var exitCode = fixture.Runner.Run(["refresh", root]);

        Assert.Equal(ExitCodes.ScanError, exitCode);
        Assert.Equal(string.Empty, fixture.Output.ToString());
        Assert.Contains("scan failed", fixture.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(original, File.ReadAllBytes(fixture.CatalogPath));
    }

    [Fact]
    public void Run_HashProviderFailure_ReturnsScanErrorAndPreservesCatalog()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "hash-root")).FullName;
        var fixture = CreateFixture("hash");
        fixture.Save(new IntegrityCatalog([Folder("target", root, null)]));
        var original = File.ReadAllBytes(fixture.CatalogPath);
        fixture.SetRunner(_ => throw new CryptographicException("hash provider failed"));

        var exitCode = fixture.Runner.Run(["refresh", root]);

        Assert.Equal(ExitCodes.ScanError, exitCode);
        Assert.Equal(string.Empty, fixture.Output.ToString());
        Assert.Contains("scan failed", fixture.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(original, File.ReadAllBytes(fixture.CatalogPath));
    }

    [Fact]
    public void Run_SaveFailure_ReturnsCatalogErrorWithoutSuccessOrTemporaryResidue()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "save-root")).FullName;
        var fixture = CreateFixture("save");
        fixture.Save(new IntegrityCatalog([Folder("target", root, null)]));
        var original = File.ReadAllBytes(fixture.CatalogPath);
        fixture.SetRunner(path => new FolderSnapshot(path, Created, [], []));
        using var catalogLock = new FileStream(
            fixture.CatalogPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var exitCode = fixture.Runner.Run(["refresh", root]);

        Assert.Equal(ExitCodes.CatalogError, exitCode);
        Assert.Equal(string.Empty, fixture.Output.ToString());
        Assert.Contains("catalog", fixture.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(original, File.ReadAllBytes(fixture.CatalogPath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(fixture.CatalogPath)!, "*.tmp"));
    }

    private Fixture CreateFixture(string name) =>
        new(Path.Combine(tempDirectory, name, "catalog.json"));

    private static RegisteredFolder Folder(string id, string rootPath, DateTimeOffset? verified) =>
        new(id, Path.GetFullPath(rootPath), Created, verified, [Fingerprint("old.txt", 'a')]);

    private static FileFingerprint Fingerprint(string relativePath, char hashCharacter) =>
        new(relativePath, new string(hashCharacter, 64), 1, Created);

    public void Dispose()
    {
        try
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class Fixture
    {
        public Fixture(string catalogPath)
        {
            CatalogPath = catalogPath;
            Store = new CatalogStore(catalogPath);
            Output = new StringWriter();
            Error = new StringWriter();
            SetRunner(_ => throw new InvalidOperationException("refresh scan was not configured"));
        }

        public string CatalogPath { get; }
        public CatalogStore Store { get; }
        public StringWriter Output { get; }
        public StringWriter Error { get; }
        public CliRunner Runner { get; private set; } = null!;

        public void Save(IntegrityCatalog catalog) => Assert.True(Store.Save(catalog).IsSuccess);

        public void SetRunner(
            Func<string, FolderSnapshot> refreshScan,
            Func<DateTimeOffset>? refreshClock = null)
        {
            Runner = new CliRunner(
                Store,
                Output,
                Error,
                refreshScan: refreshScan,
                refreshClock: refreshClock);
        }
    }
}
