using System.Security.Cryptography;
using FolderPrint.Cli;
using FolderPrint.Core.Catalog;
using FolderPrint.Core.Models;

namespace FolderPrint.Tests.Cli;

public sealed class CliDuplicatesTests : IDisposable
{
    private static readonly DateTimeOffset Scanned = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
    private readonly string tempDirectory = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), $"FolderPrint-Duplicates-{Guid.NewGuid():N}"))
        .FullName;

    [Fact]
    public void Run_RealScanner_PrintsDuplicatesAndPreservesTarget()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "real-root")).FullName;
        var nested = Directory.CreateDirectory(Path.Combine(root, "nested")).FullName;
        Directory.CreateDirectory(Path.Combine(root, "empty", "nested-empty"));
        var first = Write(root, "z.txt", "same");
        Write(nested, "a.txt", "same");
        Write(root, "m.txt", "same");
        Write(root, "b.txt", "other");
        Write(root, "c.txt", "other");
        Write(root, "unique.txt", "unique");
        var writeTime = new DateTime(2026, 7, 16, 9, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(first, writeTime);
        var before = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new FileState(
                path, File.ReadAllBytes(path), File.GetLastWriteTimeUtc(path), File.GetAttributes(path)))
            .ToArray();
        var directoriesBefore = CaptureDirectoryTree(root);
        var catalogPath = Path.Combine(tempDirectory, "missing-state", "catalog.json");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliRunner(new CatalogStore(catalogPath), output, error)
            .Run(["duplicates", root]);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(
            $"Duplicates: {root}{Environment.NewLine}Duplicate groups: 2{Environment.NewLine}"
            + $"[Duplicate Groups]{Environment.NewLine}Group 1:{Environment.NewLine}"
            + $"  b.txt{Environment.NewLine}  c.txt{Environment.NewLine}Group 2:{Environment.NewLine}"
            + $"  m.txt{Environment.NewLine}  {Path.Combine("nested", "a.txt")}{Environment.NewLine}"
            + $"  z.txt{Environment.NewLine}",
            output.ToString());
        Assert.Equal(string.Empty, error.ToString());
        Assert.False(File.Exists(catalogPath));
        Assert.Equal(before.Select(state => state.Path), Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal));
        foreach (var state in before)
        {
            Assert.Equal(state.Content, File.ReadAllBytes(state.Path));
            Assert.Equal(state.LastWriteUtc, File.GetLastWriteTimeUtc(state.Path));
            Assert.Equal(state.Attributes, File.GetAttributes(state.Path));
        }
        Assert.Equal(directoriesBefore, CaptureDirectoryTree(root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_EmptyOrSingletonFolder_PrintsNoDuplicates(bool createSingletons)
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, $"none-{createSingletons}")).FullName;
        if (createSingletons)
        {
            Write(root, "a.txt", "a");
            Write(root, "b.txt", "b");
        }
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new CliRunner(
            new CatalogStore(Path.Combine(tempDirectory, Guid.NewGuid().ToString("N"), "catalog.json")),
            output,
            error);

        var exitCode = runner.Run(["duplicates", root]);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal($"Duplicates: {root}{Environment.NewLine}No duplicates found.{Environment.NewLine}", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_ValidFolder_ScansAndFindsExactlyOnceWithSameSnapshot()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "seams-root")).FullName;
        var snapshot = Snapshot(root, [Fingerprint("a.txt", 'a'), Fingerprint("b.txt", 'a')]);
        FolderSnapshot? received = null;
        var scans = 0;
        var finds = 0;
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new CliRunner(
            new CatalogStore(Path.Combine(tempDirectory, "state-c", "catalog.json")),
            output,
            error,
            duplicateScan: path => { scans++; Assert.Equal(root, path); return snapshot; },
            duplicateFind: current => { finds++; received = current; return [["a.txt", "b.txt"]]; });

        var exitCode = runner.Run(["duplicates", root + Path.DirectorySeparatorChar]);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(1, scans);
        Assert.Equal(1, finds);
        Assert.Same(snapshot, received);
        Assert.Contains("Duplicate groups: 1", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_InvalidMissingOrFileRoot_ReturnsNotFoundWithoutScanOrFind()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "validation-root")).FullName;
        var missing = Path.Combine(tempDirectory, "missing");
        var file = Write(root, "file.txt", "content");
        var requests = new[]
        {
            (Path: "\0", Error: "Folder path is invalid." + Environment.NewLine),
            (Path: missing, Error: $"Folder was not found or is not a directory: {missing}{Environment.NewLine}"),
            (Path: file, Error: $"Folder was not found or is not a directory: {file}{Environment.NewLine}")
        };
        var scans = 0;
        var finds = 0;
        foreach (var requested in requests)
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var runner = new CliRunner(
                new CatalogStore(Path.Combine(tempDirectory, Guid.NewGuid().ToString("N"), "catalog.json")),
                output,
                error,
                duplicateScan: _ => { scans++; throw new InvalidOperationException(); },
                duplicateFind: _ => { finds++; return []; });

            Assert.Equal(ExitCodes.NotFound, runner.Run(["duplicates", requested.Path]));
            Assert.Equal(string.Empty, output.ToString());
            Assert.Equal(requested.Error, error.ToString());
        }
        Assert.Equal(0, scans);
        Assert.Equal(0, finds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_UnreadableOnlyOrMixedSnapshot_SortsDiagnosticsSkipsFinderAndPreservesCatalog(
        bool includeReadableFiles)
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, $"unreadable-root-{includeReadableFiles}")).FullName;
        var catalogPath = Path.Combine(tempDirectory, "sentinel", "catalog.json");
        Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)!);
        File.WriteAllText(catalogPath, "not-json-sentinel");
        var original = File.ReadAllBytes(catalogPath);
        var finds = 0;
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new CliRunner(
            new CatalogStore(catalogPath),
            output,
            error,
            duplicateScan: _ => Snapshot(
                root,
                includeReadableFiles ? [Fingerprint("a.txt", 'a'), Fingerprint("b.txt", 'a')] : [],
                ["z.locked", "a.locked"]),
            duplicateFind: _ => { finds++; return [["a.txt", "b.txt"]]; });

        var exitCode = runner.Run(["duplicates", root]);

        Assert.Equal(ExitCodes.ScanError, exitCode);
        Assert.Equal(0, finds);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(
            "Folder duplicate scan failed because one or more files could not be read." + Environment.NewLine
            + "Unreadable: a.locked" + Environment.NewLine
            + "Unreadable: z.locked" + Environment.NewLine,
            error.ToString());
        Assert.Equal(original, File.ReadAllBytes(catalogPath));
    }

    [Theory]
    [InlineData("io")]
    [InlineData("unauthorized")]
    [InlineData("crypto")]
    [InlineData("child-missing")]
    [InlineData("file-missing")]
    public void Run_ReliableScanFailure_ReturnsExactScanError(string failure)
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, $"failure-{failure}")).FullName;
        Directory.CreateDirectory(Path.Combine(root, "empty"));
        var directoriesBefore = CaptureDirectoryTree(root);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new CliRunner(
            new CatalogStore(Path.Combine(tempDirectory, $"state-{failure}", "catalog.json")),
            output,
            error,
            duplicateScan: _ => throw failure switch
            {
                "io" => new IOException("environment detail"),
                "unauthorized" => new UnauthorizedAccessException("environment detail"),
                "crypto" => new CryptographicException("environment detail"),
                "child-missing" => new DirectoryNotFoundException("child disappeared"),
                _ => new FileNotFoundException("child disappeared")
            });

        var exitCode = runner.Run(["duplicates", root]);

        Assert.Equal(ExitCodes.ScanError, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal("Folder scan failed." + Environment.NewLine, error.ToString());
        Assert.Equal(directoriesBefore, CaptureDirectoryTree(root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_RootDisappearsDuringScan_ReturnsNotFound(bool throwFileNotFound)
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, $"disappearing-root-{throwFileNotFound}")).FullName;
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new CliRunner(
            new CatalogStore(Path.Combine(tempDirectory, "state-d", "catalog.json")),
            output,
            error,
            duplicateScan: _ =>
            {
                Directory.Delete(root);
                if (throwFileNotFound)
                {
                    throw new FileNotFoundException();
                }

                throw new DirectoryNotFoundException();
            });

        var exitCode = runner.Run(["duplicates", root]);

        Assert.Equal(ExitCodes.NotFound, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal($"Folder was not found or is not a directory: {root}{Environment.NewLine}", error.ToString());
    }

    [Theory]
    [InlineData("scanner")]
    [InlineData("finder")]
    [InlineData("formatter")]
    public void Run_UnexpectedPipelineFailure_SanitizesDetails(string failure)
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, $"unexpected-root-{failure}")).FullName;
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new CliRunner(
            new CatalogStore(Path.Combine(tempDirectory, "unexpected", "catalog.json")),
            output,
            error,
            duplicateScan: _ => failure == "scanner"
                ? throw new InvalidOperationException("sensitive detail")
                : Snapshot(root, []),
            duplicateFind: _ => failure switch
            {
                "finder" => throw new InvalidOperationException("sensitive detail"),
                "formatter" => [null!],
                _ => []
            });

        Assert.Equal(ExitCodes.UnexpectedError, runner.Run(["duplicates", root]));
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal("Unexpected error." + Environment.NewLine, error.ToString());
        Assert.DoesNotContain("sensitive detail", error.ToString());
    }

    [Fact]
    public void Run_MalformedInaccessibleAndRegisteredCatalogs_DoNotAffectResults()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "catalog-root")).FullName;
        Write(root, "a.txt", "same");
        Write(root, "b.txt", "same");
        var malformedPath = Path.Combine(tempDirectory, "malformed", "catalog.json");
        Directory.CreateDirectory(Path.GetDirectoryName(malformedPath)!);
        File.WriteAllText(malformedPath, "malformed");
        var malformedBytes = File.ReadAllBytes(malformedPath);
        var blockingFile = Write(tempDirectory, "catalog-parent-file", "blocking");
        var inaccessiblePath = Path.Combine(blockingFile, "catalog.json");
        var registeredPath = Path.Combine(tempDirectory, "registered", "catalog.json");
        var registeredStore = new CatalogStore(registeredPath);
        Assert.True(registeredStore.Save(new IntegrityCatalog([
            new RegisteredFolder("id", root, Scanned, null, [])
        ])).IsSuccess);
        var registeredBytes = File.ReadAllBytes(registeredPath);

        var outputs = new List<string>();
        foreach (var store in new[] { new CatalogStore(malformedPath), new CatalogStore(inaccessiblePath), registeredStore })
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var exitCode = new CliRunner(store, output, error).Run(["duplicates", root]);
            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            outputs.Add(output.ToString());
        }

        Assert.All(outputs, value => Assert.Equal(outputs[0], value));
        Assert.Equal(malformedBytes, File.ReadAllBytes(malformedPath));
        Assert.Equal(registeredBytes, File.ReadAllBytes(registeredPath));
        Assert.False(File.Exists(inaccessiblePath));
    }

    [Fact]
    public void Run_CatalogFileInsideTarget_IsScannedNormallyAndNotMutated()
    {
        var root = Directory.CreateDirectory(Path.Combine(tempDirectory, "inside-root")).FullName;
        var catalogPath = Path.Combine(root, "state", "catalog.json");
        Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)!);
        File.WriteAllText(catalogPath, "same-content");
        Write(root, "copy.txt", "same-content");
        var original = File.ReadAllBytes(catalogPath);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliRunner(new CatalogStore(catalogPath), output, error)
            .Run(["duplicates", root]);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("  copy.txt", output.ToString());
        Assert.Contains($"  {Path.Combine("state", "catalog.json")}", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
        Assert.Equal(original, File.ReadAllBytes(catalogPath));
    }

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

    private static FolderSnapshot Snapshot(
        string root,
        IReadOnlyList<FileFingerprint> files,
        IReadOnlyList<string>? unreadable = null) =>
        new(root, Scanned, files, unreadable ?? []);

    private static FileFingerprint Fingerprint(string path, char hashCharacter) =>
        new(path, new string(hashCharacter, 64), 1, Scanned);

    private static string Write(string directory, string name, string content)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static DirectoryState[] CaptureDirectoryTree(string root) =>
        Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .Prepend(root)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new DirectoryState(path, Directory.GetLastWriteTimeUtc(path), File.GetAttributes(path)))
            .ToArray();

    private sealed record FileState(
        string Path,
        byte[] Content,
        DateTime LastWriteUtc,
        FileAttributes Attributes);

    private sealed record DirectoryState(
        string Path,
        DateTime LastWriteUtc,
        FileAttributes Attributes);
}
