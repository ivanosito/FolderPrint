using FolderPrint.Core.Catalog;
using FolderPrint.Core.Models;
using FolderPrint.Core.Registration;

namespace FolderPrint.Tests.Registration;

public sealed class UnregistrationServiceTests : IDisposable
{
    private static readonly DateTimeOffset Created = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly string tempDirectory = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), $"FolderPrint-Unregister-Core-{Guid.NewGuid():N}"))
        .FullName;

    [Fact]
    public void Unregister_ValidAlias_RemovesOnlyMatchAndPreservesSurvivors()
    {
        var catalogPath = Path.Combine(tempDirectory, "state", "catalog.json");
        var store = new CatalogStore(catalogPath);
        var first = Folder("first", Path.Combine(tempDirectory, "first"), Created.AddHours(1));
        var target = Folder("target", Path.Combine(tempDirectory, "target"), Created.AddHours(2));
        var last = Folder("last", Path.Combine(tempDirectory, "last"), null);
        Assert.True(store.Save(new IntegrityCatalog([first, target, last])).IsSuccess);

        var result = new UnregistrationService(store).Unregister(
            target.RootPath.ToUpperInvariant() + Path.DirectorySeparatorChar);

        Assert.Equal(UnregistrationStatus.Success, result.Status);
        Assert.Equal(target.RootPath, result.RootPath);
        Assert.Null(result.ErrorMessage);
        var survivors = store.Load().Catalog!.RegisteredFolders;
        Assert.Equal(["first", "last"], survivors.Select(folder => folder.Id));
        AssertFolder(first, survivors[0]);
        AssertFolder(last, survivors[1]);
    }

    [Fact]
    public void Unregister_OnlyEntry_PersistsValidEmptyCatalog()
    {
        var catalogPath = Path.Combine(tempDirectory, "catalog.json");
        var store = new CatalogStore(catalogPath);
        var target = Folder("only", Path.Combine(tempDirectory, "missing"), null);
        Assert.True(store.Save(new IntegrityCatalog([target])).IsSuccess);

        var result = new UnregistrationService(store).Unregister(target.RootPath);

        Assert.Equal(UnregistrationStatus.Success, result.Status);
        Assert.Empty(store.Load().Catalog!.RegisteredFolders);
        Assert.True(File.Exists(catalogPath));
    }

    [Fact]
    public void Unregister_MissingCatalogOrNoMatch_ReturnsNotFoundWithoutCreatingOrChangingCatalog()
    {
        var missingPath = Path.Combine(tempDirectory, "missing-state", "catalog.json");
        var missingStore = new CatalogStore(missingPath);
        var existingPath = Path.Combine(tempDirectory, "existing.json");
        var existingStore = new CatalogStore(existingPath);
        Assert.True(existingStore.Save(new IntegrityCatalog([
            Folder("other", Path.Combine(tempDirectory, "other"), null)
        ])).IsSuccess);
        var original = File.ReadAllBytes(existingPath);

        var missing = new UnregistrationService(missingStore).Unregister(Path.Combine(tempDirectory, "target"));
        var notFound = new UnregistrationService(existingStore).Unregister(Path.Combine(tempDirectory, "target"));

        Assert.Equal(UnregistrationStatus.NotFound, missing.Status);
        Assert.Equal(UnregistrationStatus.NotFound, notFound.Status);
        Assert.False(File.Exists(missingPath));
        Assert.Equal(original, File.ReadAllBytes(existingPath));
    }

    [Fact]
    public void Unregister_InvalidRequestedPath_ReturnsInvalidRootWithoutCatalogCreation()
    {
        var catalogPath = Path.Combine(tempDirectory, "state", "catalog.json");

        var result = new UnregistrationService(new CatalogStore(catalogPath))
            .Unregister(new string((char)0, 1));

        Assert.Equal(UnregistrationStatus.InvalidRoot, result.Status);
        Assert.False(File.Exists(catalogPath));
    }

    [Fact]
    public void Unregister_MalformedOrAmbiguousCatalog_ReturnsCatalogErrorAndPreservesBytes()
    {
        var malformedPath = Path.Combine(tempDirectory, "malformed.json");
        File.WriteAllText(malformedPath, "malformed");
        var malformedBytes = File.ReadAllBytes(malformedPath);
        var duplicatePath = Path.Combine(tempDirectory, "duplicate.json");
        var duplicateStore = new CatalogStore(duplicatePath);
        var root = Path.GetFullPath(Path.Combine(tempDirectory, "duplicate-root"));
        Assert.True(duplicateStore.Save(new IntegrityCatalog([
            Folder("first", root, null),
            Folder("second", root + Path.DirectorySeparatorChar, null)
        ])).IsSuccess);
        var duplicateBytes = File.ReadAllBytes(duplicatePath);

        var malformed = new UnregistrationService(new CatalogStore(malformedPath)).Unregister(root);
        var duplicate = new UnregistrationService(duplicateStore).Unregister(root);

        Assert.Equal(UnregistrationStatus.CatalogError, malformed.Status);
        Assert.Equal(UnregistrationStatus.CatalogError, duplicate.Status);
        Assert.Equal(malformedBytes, File.ReadAllBytes(malformedPath));
        Assert.Equal(duplicateBytes, File.ReadAllBytes(duplicatePath));
    }

    [Fact]
    public void Unregister_PhysicalRootIsFile_DoesNotReadOrModifyTarget()
    {
        var targetPath = Path.Combine(tempDirectory, "target-file");
        File.WriteAllText(targetPath, "unchanged");
        var targetBytes = File.ReadAllBytes(targetPath);
        var lastWrite = File.GetLastWriteTimeUtc(targetPath);
        var catalogPath = Path.Combine(tempDirectory, "state", "catalog.json");
        var store = new CatalogStore(catalogPath);
        Assert.True(store.Save(new IntegrityCatalog([Folder("target", targetPath, null)])).IsSuccess);

        var result = new UnregistrationService(store).Unregister(targetPath);

        Assert.Equal(UnregistrationStatus.Success, result.Status);
        Assert.Equal(targetBytes, File.ReadAllBytes(targetPath));
        Assert.Equal(lastWrite, File.GetLastWriteTimeUtc(targetPath));
    }

    [Fact]
    public void Unregister_CatalogChangesBeforeGuardedSave_ReturnsCatalogErrorAndPreservesConcurrentChange()
    {
        var catalogPath = Path.Combine(tempDirectory, "state", "catalog.json");
        var store = new CatalogStore(catalogPath);
        var target = Folder("target", Path.Combine(tempDirectory, "target"), null);
        var concurrent = Folder("concurrent", Path.Combine(tempDirectory, "concurrent"), null);
        Assert.True(store.Save(new IntegrityCatalog([target])).IsSuccess);
        var service = new UnregistrationService(
            store,
            () => Assert.True(store.Save(new IntegrityCatalog([target, concurrent])).IsSuccess));

        var result = service.Unregister(target.RootPath);

        Assert.Equal(UnregistrationStatus.CatalogError, result.Status);
        Assert.Contains("changed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["target", "concurrent"], store.Load().Catalog!.RegisteredFolders.Select(folder => folder.Id));
    }

    [Fact]
    public void Unregister_PrefixSiblingAndNestedRequests_DoNotMatchRegisteredRoot()
    {
        var catalogPath = Path.Combine(tempDirectory, "lookalike.json");
        var store = new CatalogStore(catalogPath);
        var registeredRoot = Path.Combine(tempDirectory, "Data");
        Assert.True(store.Save(new IntegrityCatalog([Folder("target", registeredRoot, null)])).IsSuccess);
        var originalBytes = File.ReadAllBytes(catalogPath);
        var service = new UnregistrationService(store);
        var requests = new[]
        {
            Path.Combine(tempDirectory, "Database"),
            Path.Combine(tempDirectory, "Data-other"),
            Path.Combine(registeredRoot, "child")
        };

        foreach (var request in requests)
        {
            var result = service.Unregister(request);

            Assert.Equal(UnregistrationStatus.NotFound, result.Status);
            Assert.Equal(originalBytes, File.ReadAllBytes(catalogPath));
        }
    }

    [Fact]
    public void Unregister_InvalidUnrelatedEntry_ReturnsCatalogErrorWithoutPartialRemoval()
    {
        var catalogPath = Path.Combine(tempDirectory, "invalid-unrelated.json");
        var store = new CatalogStore(catalogPath);
        var target = Folder("target", Path.Combine(tempDirectory, "target"), null);
        var invalid = Folder("invalid", Path.Combine(tempDirectory, "invalid"), null) with { Id = string.Empty };
        Assert.True(store.Save(new IntegrityCatalog([target, invalid])).IsSuccess);
        var originalBytes = File.ReadAllBytes(catalogPath);

        var result = new UnregistrationService(store).Unregister(target.RootPath);

        Assert.Equal(UnregistrationStatus.CatalogError, result.Status);
        Assert.Equal(originalBytes, File.ReadAllBytes(catalogPath));
    }

    [Fact]
    public void Unregister_ConcurrentRegistrationCommits_FirstFailsAndPreservesBothEntries()
    {
        var catalogPath = Path.Combine(tempDirectory, "cross-writer.json");
        var store = new CatalogStore(catalogPath);
        var target = Folder("target", Path.Combine(tempDirectory, "target"), null);
        Assert.True(store.Save(new IntegrityCatalog([target])).IsSuccess);
        var newRoot = Directory.CreateDirectory(Path.Combine(tempDirectory, "new-root")).FullName;
        var registration = new RegistrationService(
            store,
            root => new FolderSnapshot(root, Created, [], []),
            () => "concurrent-registration",
            () => Created);
        var service = new UnregistrationService(
            store,
            () => Assert.Equal(RegistrationStatus.Success, registration.Register(newRoot).Status));

        var result = service.Unregister(target.RootPath);

        Assert.Equal(UnregistrationStatus.CatalogError, result.Status);
        Assert.Equal(
            ["target", "concurrent-registration"],
            store.Load().Catalog!.RegisteredFolders.Select(folder => folder.Id));
    }

    [Fact]
    public void Unregister_ConcurrentVerificationTimestampCommits_FirstFailsAndPreservesTimestamp()
    {
        var catalogPath = Path.Combine(tempDirectory, "verify-writer.json");
        var store = new CatalogStore(catalogPath);
        var target = Folder("target", Path.Combine(tempDirectory, "target"), null);
        Assert.True(store.Save(new IntegrityCatalog([target])).IsSuccess);
        var verifiedAt = Created.AddHours(3);
        var service = new UnregistrationService(
            store,
            () =>
            {
                var loaded = store.Load();
                var verified = loaded.Catalog!.WithLastVerifiedAt(0, verifiedAt);
                Assert.True(store.SaveIfUnchanged(verified, loaded.Version).IsSuccess);
            });

        var result = service.Unregister(target.RootPath);

        Assert.Equal(UnregistrationStatus.CatalogError, result.Status);
        Assert.Equal(verifiedAt, Assert.Single(store.Load().Catalog!.RegisteredFolders).LastVerifiedAtUtc);
    }

    private static RegisteredFolder Folder(string id, string rootPath, DateTimeOffset? verified) =>
        new(
            id,
            Path.GetFullPath(rootPath),
            Created,
            verified,
            [new FileFingerprint("file.txt", new string('a', 64), 42, Created)]);

    private static void AssertFolder(RegisteredFolder expected, RegisteredFolder actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.RootPath, actual.RootPath);
        Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
        Assert.Equal(expected.LastVerifiedAtUtc, actual.LastVerifiedAtUtc);
        Assert.Equal(expected.Files, actual.Files);
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
}
