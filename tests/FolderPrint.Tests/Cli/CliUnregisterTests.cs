using FolderPrint.Cli;
using FolderPrint.Core.Catalog;
using FolderPrint.Core.Models;

namespace FolderPrint.Tests.Cli;

public sealed class CliUnregisterTests : IDisposable
{
    private static readonly DateTimeOffset Created = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly string tempDirectory = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), $"FolderPrint-Unregister-Cli-{Guid.NewGuid():N}"))
        .FullName;

    [Fact]
    public void Run_ValidAlias_RemovesOnlyMatchReportsStoredRootAndDoesNotTouchTarget()
    {
        var targetRoot = Directory.CreateDirectory(Path.Combine(tempDirectory, "target")).FullName;
        var targetFile = Path.Combine(targetRoot, "file.txt");
        File.WriteAllText(targetFile, "unchanged");
        var targetBytes = File.ReadAllBytes(targetFile);
        var targetWrite = File.GetLastWriteTimeUtc(targetFile);
        var fixture = CreateFixture();
        var first = Folder("first", Path.Combine(tempDirectory, "first"), Created.AddHours(1));
        var target = Folder("target", targetRoot, Created.AddHours(2));
        var last = Folder("last", Path.Combine(tempDirectory, "last"), null);
        fixture.Save(new IntegrityCatalog([first, target, last]));

        var exitCode = fixture.Runner.Run([
            "unregister",
            target.RootPath.ToUpperInvariant() + Path.DirectorySeparatorChar
        ]);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal($"Unregistered folder: {target.RootPath}{Environment.NewLine}", fixture.Output.ToString());
        Assert.Equal(string.Empty, fixture.Error.ToString());
        Assert.Equal(["first", "last"], fixture.Store.Load().Catalog!.RegisteredFolders.Select(folder => folder.Id));
        Assert.Equal(targetBytes, File.ReadAllBytes(targetFile));
        Assert.Equal(targetWrite, File.GetLastWriteTimeUtc(targetFile));
    }

    [Fact]
    public void Run_MissingOrEmptyCatalog_ReturnsNotFoundWithoutCreatingOrRewriting()
    {
        var missing = CreateFixture("missing-state");
        var empty = CreateFixture("empty-state");
        empty.Save(IntegrityCatalog.Empty);
        var emptyBytes = File.ReadAllBytes(empty.CatalogPath);
        var requested = Path.Combine(tempDirectory, "not-registered");

        var missingExit = missing.Runner.Run(["unregister", requested]);
        var emptyExit = empty.Runner.Run(["unregister", requested]);

        Assert.Equal(ExitCodes.NotFound, missingExit);
        Assert.Equal(ExitCodes.NotFound, emptyExit);
        Assert.Equal(string.Empty, missing.Output.ToString());
        Assert.Equal(string.Empty, empty.Output.ToString());
        Assert.Contains("not registered", missing.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not registered", empty.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(missing.CatalogPath));
        Assert.Equal(emptyBytes, File.ReadAllBytes(empty.CatalogPath));
    }

    [Fact]
    public void Run_InvalidRequestedPath_ReturnsNotFoundWithoutCatalogCreation()
    {
        var fixture = CreateFixture();

        var exitCode = fixture.Runner.Run(["unregister", new string((char)0, 1)]);

        Assert.Equal(ExitCodes.NotFound, exitCode);
        Assert.Equal(string.Empty, fixture.Output.ToString());
        Assert.Contains("invalid", fixture.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(fixture.CatalogPath));
    }

    [Fact]
    public void Run_MalformedOrAmbiguousCatalog_ReturnsCatalogErrorAndPreservesBytes()
    {
        var malformed = CreateFixture("malformed-state");
        Directory.CreateDirectory(Path.GetDirectoryName(malformed.CatalogPath)!);
        File.WriteAllText(malformed.CatalogPath, "malformed");
        var malformedBytes = File.ReadAllBytes(malformed.CatalogPath);
        var ambiguous = CreateFixture("ambiguous-state");
        var root = Path.GetFullPath(Path.Combine(tempDirectory, "duplicate"));
        ambiguous.Save(new IntegrityCatalog([
            Folder("first", root, null),
            Folder("second", root + Path.DirectorySeparatorChar, null)
        ]));
        var ambiguousBytes = File.ReadAllBytes(ambiguous.CatalogPath);

        var malformedExit = malformed.Runner.Run(["unregister", root]);
        var ambiguousExit = ambiguous.Runner.Run(["unregister", root]);

        Assert.Equal(ExitCodes.CatalogError, malformedExit);
        Assert.Equal(ExitCodes.CatalogError, ambiguousExit);
        Assert.Equal(string.Empty, malformed.Output.ToString());
        Assert.Equal(string.Empty, ambiguous.Output.ToString());
        Assert.Equal(malformedBytes, File.ReadAllBytes(malformed.CatalogPath));
        Assert.Equal(ambiguousBytes, File.ReadAllBytes(ambiguous.CatalogPath));
    }

    [Fact]
    public void Run_MissingPhysicalRootOrFileRoot_UnregistersWithoutScanOrVerify()
    {
        var missing = CreateFixture("missing-root-state");
        var missingRoot = Path.Combine(tempDirectory, "physically-missing");
        missing.Save(new IntegrityCatalog([Folder("missing", missingRoot, null)]));
        var file = CreateFixture("file-root-state");
        var fileRoot = Path.Combine(tempDirectory, "root-file");
        File.WriteAllText(fileRoot, "unchanged");
        var fileBytes = File.ReadAllBytes(fileRoot);
        file.Save(new IntegrityCatalog([Folder("file", fileRoot, null)]));

        var missingExit = missing.Runner.Run(["unregister", missingRoot]);
        var fileExit = file.Runner.Run(["unregister", fileRoot]);

        Assert.Equal(ExitCodes.Success, missingExit);
        Assert.Equal(ExitCodes.Success, fileExit);
        Assert.Empty(missing.Store.Load().Catalog!.RegisteredFolders);
        Assert.Empty(file.Store.Load().Catalog!.RegisteredFolders);
        Assert.Equal(fileBytes, File.ReadAllBytes(fileRoot));
    }

    [Fact]
    public void Run_SaveFailure_ReturnsCatalogErrorWithoutSuccessOrMutation()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var fixture = CreateFixture();
        var target = Folder("target", Path.Combine(tempDirectory, "target"), null);
        fixture.Save(new IntegrityCatalog([target]));
        var originalBytes = File.ReadAllBytes(fixture.CatalogPath);
        using var catalogLock = new FileStream(
            fixture.CatalogPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var exitCode = fixture.Runner.Run(["unregister", target.RootPath]);

        Assert.Equal(ExitCodes.CatalogError, exitCode);
        Assert.Equal(string.Empty, fixture.Output.ToString());
        Assert.Contains("catalog", fixture.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalBytes, File.ReadAllBytes(fixture.CatalogPath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(fixture.CatalogPath)!, "*.tmp"));
    }

    [Fact]
    public void Run_AfterUnregister_ListAndRegisterContinueToWork()
    {
        var fixture = CreateFixture();
        var targetRoot = Directory.CreateDirectory(Path.Combine(tempDirectory, "reregister")).FullName;
        fixture.Save(new IntegrityCatalog([Folder("old", targetRoot, null)]));

        Assert.Equal(ExitCodes.Success, fixture.Runner.Run(["unregister", targetRoot]));
        fixture.Output.GetStringBuilder().Clear();
        Assert.Equal(ExitCodes.Success, fixture.Runner.Run(["list"]));
        Assert.Equal($"No folders are registered.{Environment.NewLine}", fixture.Output.ToString());
        fixture.Output.GetStringBuilder().Clear();
        Assert.Equal(ExitCodes.Success, fixture.Runner.Run(["register", targetRoot]));
        Assert.Single(fixture.Store.Load().Catalog!.RegisteredFolders);
    }

    [Fact]
    public void Run_AfterUnregister_SurvivorListsAndVerifiesWithMetadataAndBaselinePreserved()
    {
        var fixture = CreateFixture("survivor-state");
        var targetRoot = Path.Combine(tempDirectory, "remove-me");
        var survivorRoot = Directory.CreateDirectory(Path.Combine(tempDirectory, "survivor")).FullName;
        var survivor = Folder("survivor-id", survivorRoot, Created.AddHours(1));
        fixture.Save(new IntegrityCatalog([
            Folder("target-id", targetRoot, null),
            survivor
        ]));

        Assert.Equal(ExitCodes.Success, fixture.Runner.Run(["unregister", targetRoot]));
        fixture.Output.GetStringBuilder().Clear();
        Assert.Equal(ExitCodes.Success, fixture.Runner.Run(["list"]));
        Assert.Contains("Id: survivor-id", fixture.Output.ToString());
        Assert.Contains($"Path: {survivorRoot}", fixture.Output.ToString());
        Assert.Contains(
            $"Last verified: {Created.AddHours(1).ToUniversalTime():O}",
            fixture.Output.ToString());

        fixture.Output.GetStringBuilder().Clear();
        var scannedAt = Created.AddHours(2);
        var verifyRunner = new CliRunner(
            fixture.Store,
            fixture.Output,
            fixture.Error,
            root => new FolderSnapshot(root, scannedAt, [], []));

        Assert.Equal(ExitCodes.Success, verifyRunner.Run(["verify", survivorRoot]));
        var persisted = Assert.Single(fixture.Store.Load().Catalog!.RegisteredFolders);
        Assert.Equal("survivor-id", persisted.Id);
        Assert.Equal(survivorRoot, persisted.RootPath);
        Assert.Equal(Created, persisted.CreatedAtUtc);
        Assert.Equal(scannedAt, persisted.LastVerifiedAtUtc);
        Assert.Empty(persisted.Files);
    }

    private Fixture CreateFixture(string stateName = "state") =>
        new(Path.Combine(tempDirectory, stateName, "catalog.json"));

    private static RegisteredFolder Folder(string id, string rootPath, DateTimeOffset? verified) =>
        new(id, Path.GetFullPath(rootPath), Created, verified, []);

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
            Runner = new CliRunner(
                Store,
                Output,
                Error,
                _ => throw new InvalidOperationException("Unregister must not scan or verify."),
                (_, _) => throw new InvalidOperationException("Unregister must not verify."));
        }

        public string CatalogPath { get; }
        public CatalogStore Store { get; }
        public StringWriter Output { get; }
        public StringWriter Error { get; }
        public CliRunner Runner { get; }

        public void Save(IntegrityCatalog catalog) => Assert.True(Store.Save(catalog).IsSuccess);
    }
}
