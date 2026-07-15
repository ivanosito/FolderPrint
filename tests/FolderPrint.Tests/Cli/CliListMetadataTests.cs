using FolderPrint.Cli;
using FolderPrint.Core.Catalog;
using FolderPrint.Core.Models;

namespace FolderPrint.Tests.Cli;

public sealed class CliListMetadataTests
{
    private static readonly DateTimeOffset Created = new(2026, 7, 15, 8, 30, 0, TimeSpan.FromHours(-5));
    private static readonly DateTimeOffset Verified = new(2026, 7, 15, 15, 45, 0, TimeSpan.FromHours(2));
    private const string ValidSha256 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void Run_ListValidCatalog_PrintsDeterministicMetadataAndPreservesCatalogAndTargetBytes()
    {
        using var fixture = new ListFixture();
        var targetRoot = Directory.CreateDirectory(Path.Combine(fixture.TempDirectory, "tracked")).FullName;
        var targetPath = Path.Combine(targetRoot, "file.txt");
        File.WriteAllText(targetPath, "unchanged target");
        var catalog = new IntegrityCatalog([
            Folder("z-id", targetRoot, Verified, [Fingerprint("file.txt")]),
            Folder("a-id", Path.Combine(fixture.TempDirectory, "A-missing"), null, [])
        ]);
        fixture.Save(catalog);
        var catalogBytes = File.ReadAllBytes(fixture.CatalogPath);
        var targetBytes = File.ReadAllBytes(targetPath);
        var metadataBefore = fixture.Store.Load().Catalog!.RegisteredFolders;

        var exitCode = fixture.RunnerWithThrowingVerification().Run(["list"]);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(string.Empty, fixture.Error.ToString());
        Assert.Equal(catalogBytes, File.ReadAllBytes(fixture.CatalogPath));
        Assert.Equal(targetBytes, File.ReadAllBytes(targetPath));
        AssertMetadataEqual(metadataBefore, fixture.Store.Load().Catalog!.RegisteredFolders);
        Assert.Equal(
        [
            "Registered folders:",
            "Id: a-id",
            $"Path: {Path.Combine(fixture.TempDirectory, "A-missing")}",
            "Registered: 2026-07-15T13:30:00.0000000+00:00",
            "Last verified: Never",
            "Baseline files: 0",
            string.Empty,
            "Id: z-id",
            $"Path: {targetRoot}",
            "Registered: 2026-07-15T13:30:00.0000000+00:00",
            "Last verified: 2026-07-15T13:45:00.0000000+00:00",
            "Baseline files: 1"
        ], ReadLines(fixture.Output));
    }

    [Fact]
    public void Run_ListMissingOrExistingEmptyCatalog_PreservesExactMessageAndDoesNotCreateOrRewrite()
    {
        using var missing = new ListFixture();
        using var empty = new ListFixture();
        empty.Save(IntegrityCatalog.Empty);
        var emptyBytes = File.ReadAllBytes(empty.CatalogPath);

        var missingExit = missing.RunnerWithThrowingVerification().Run(["list"]);
        var emptyExit = empty.RunnerWithThrowingVerification().Run(["list"]);

        Assert.Equal(ExitCodes.Success, missingExit);
        Assert.Equal(ExitCodes.Success, emptyExit);
        Assert.Equal("No folders are registered." + Environment.NewLine, missing.Output.ToString());
        Assert.Equal("No folders are registered." + Environment.NewLine, empty.Output.ToString());
        Assert.False(File.Exists(missing.CatalogPath));
        Assert.Equal(emptyBytes, File.ReadAllBytes(empty.CatalogPath));
    }

    [Fact]
    public void Run_ListMalformedNestedFingerprint_ReturnsCatalogErrorWithoutOutputOrMutation()
    {
        using var fixture = new ListFixture();
        fixture.Save(new IntegrityCatalog([
            Folder("folder", Path.Combine(fixture.TempDirectory, "missing"), null, [null!])
        ]));
        var originalBytes = File.ReadAllBytes(fixture.CatalogPath);

        var exitCode = fixture.RunnerWithThrowingVerification().Run(["list"]);

        Assert.Equal(ExitCodes.CatalogError, exitCode);
        Assert.Equal(string.Empty, fixture.Output.ToString());
        Assert.Contains("catalog", fixture.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalBytes, File.ReadAllBytes(fixture.CatalogPath));
    }

    [Fact]
    public void Run_ListDuplicateNormalizedRoots_ReturnsCatalogErrorWithoutPartialOutputOrMutation()
    {
        using var fixture = new ListFixture();
        var root = Path.GetFullPath(Path.Combine(fixture.TempDirectory, "missing"));
        fixture.Save(new IntegrityCatalog([
            Folder("folder-1", root, null, []),
            Folder("folder-2", root + Path.DirectorySeparatorChar, null, [])
        ]));
        var originalBytes = File.ReadAllBytes(fixture.CatalogPath);

        var exitCode = fixture.RunnerWithThrowingVerification().Run(["list"]);

        Assert.Equal(ExitCodes.CatalogError, exitCode);
        Assert.Equal(string.Empty, fixture.Output.ToString());
        Assert.Contains("multiple registrations", fixture.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalBytes, File.ReadAllBytes(fixture.CatalogPath));
    }

    private static RegisteredFolder Folder(
        string id,
        string rootPath,
        DateTimeOffset? verified,
        IReadOnlyList<FileFingerprint> files) =>
        new(id, rootPath, Created, verified, files);

    private static FileFingerprint Fingerprint(string path) =>
        new(path, ValidSha256, 1, Created);

    private static string[] ReadLines(StringWriter writer) =>
        writer.ToString().Split(Environment.NewLine, StringSplitOptions.None)[..^1];

    private static void AssertMetadataEqual(
        IReadOnlyList<RegisteredFolder> expected,
        IReadOnlyList<RegisteredFolder> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index].Id, actual[index].Id);
            Assert.Equal(expected[index].RootPath, actual[index].RootPath);
            Assert.Equal(expected[index].CreatedAtUtc, actual[index].CreatedAtUtc);
            Assert.Equal(expected[index].LastVerifiedAtUtc, actual[index].LastVerifiedAtUtc);
            Assert.Equal(expected[index].Files, actual[index].Files);
        }
    }

    private sealed class ListFixture : IDisposable
    {
        public ListFixture()
        {
            TempDirectory = Directory.CreateDirectory(
                Path.Combine(Path.GetTempPath(), $"FolderPrint-List-{Guid.NewGuid():N}"))
                .FullName;
            CatalogPath = Path.Combine(TempDirectory, "state", "catalog.json");
            Store = new CatalogStore(CatalogPath);
            Output = new StringWriter();
            Error = new StringWriter();
        }

        public string TempDirectory { get; }
        public string CatalogPath { get; }
        public CatalogStore Store { get; }
        public StringWriter Output { get; }
        public StringWriter Error { get; }

        public void Save(IntegrityCatalog catalog) =>
            Assert.True(Store.Save(catalog).IsSuccess);

        public CliRunner RunnerWithThrowingVerification() =>
            new(
                Store,
                Output,
                Error,
                _ => throw new InvalidOperationException("List must not scan."),
                (_, _) => throw new InvalidOperationException("List must not verify."));

        public void Dispose()
        {
            Output.Dispose();
            Error.Dispose();
            try { Directory.Delete(TempDirectory, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
