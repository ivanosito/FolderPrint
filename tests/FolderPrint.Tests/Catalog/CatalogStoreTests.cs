using System.Text.Json;
using FolderPrint.Core.Catalog;
using FolderPrint.Core.Models;

namespace FolderPrint.Tests.Catalog;

public sealed class CatalogStoreTests
{
    [Fact]
    public void Load_WhenCatalogFileDoesNotExist_ReturnsEmptyCatalog()
    {
        var catalogPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "catalog.json");
        var store = new CatalogStore(catalogPath);

        var result = store.Load();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Catalog);
        Assert.Empty(result.Catalog.RegisteredFolders);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Load_WhenCatalogJsonIsMalformed_ReturnsCatalogErrorAndDoesNotOverwriteFile()
    {
        var directory = CreateTempDirectory();
        try
        {
            var catalogPath = Path.Combine(directory, "catalog.json");
            File.WriteAllText(catalogPath, "{ this is not valid json");
            var store = new CatalogStore(catalogPath);

            var result = store.Load();

            Assert.False(result.IsSuccess);
            Assert.Null(result.Catalog);
            Assert.Contains("catalog", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("{ this is not valid json", File.ReadAllText(catalogPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"registeredFolders\":null}")]
    public void Load_WhenRequiredCatalogStructureIsNullOrMissing_ReturnsCatalogError(string json)
    {
        var directory = CreateTempDirectory();
        try
        {
            var catalogPath = Path.Combine(directory, "catalog.json");
            File.WriteAllText(catalogPath, json);

            var result = new CatalogStore(catalogPath).Load();

            Assert.False(result.IsSuccess);
            Assert.Null(result.Catalog);
            Assert.Contains("catalog", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(json, File.ReadAllText(catalogPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_WhenCatalogPathIsDirectory_ReturnsCatalogError()
    {
        var root = CreateTempDirectory();
        try
        {
            var catalogPath = Directory.CreateDirectory(Path.Combine(root, "catalog.json")).FullName;

            var result = new CatalogStore(catalogPath).Load();

            Assert.False(result.IsSuccess);
            Assert.Contains("read", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Save_WhenParentAndCatalogAreMissing_CreatesValidCatalog()
    {
        var root = CreateTempDirectory();
        try
        {
            var catalogPath = Path.Combine(root, "FolderPrint", "catalog.json");
            var store = new CatalogStore(catalogPath);

            var result = store.Save(IntegrityCatalog.Empty);

            Assert.True(result.IsSuccess);
            Assert.Null(result.ErrorMessage);
            Assert.True(File.Exists(catalogPath));
            Assert.True(store.Load().IsSuccess);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveThenLoad_WithCompleteBaseline_RoundTripsAllFields()
    {
        var root = CreateTempDirectory();
        try
        {
            var catalogPath = Path.Combine(root, "catalog.json");
            var created = new DateTimeOffset(2026, 7, 13, 10, 11, 12, TimeSpan.Zero);
            var verified = created.AddHours(1);
            var modified = created.AddMinutes(-5);
            var files = new[]
            {
                new FileFingerprint(Path.Combine("nested", "file.txt"), "abcdef012345", 123, modified)
            };
            var catalog = new IntegrityCatalog([
                new RegisteredFolder("folder-1", "C:\\Data", created, verified, files),
                new RegisteredFolder("folder-2", "C:\\Empty", created.AddDays(1), null, [])
            ]);
            var store = new CatalogStore(catalogPath);

            var saveResult = store.Save(catalog);
            var loadResult = new CatalogStore(catalogPath).Load();

            Assert.True(saveResult.IsSuccess);
            Assert.True(loadResult.IsSuccess);
            Assert.NotNull(loadResult.Catalog);
            Assert.Equal(2, loadResult.Catalog.RegisteredFolders.Count);

            var loadedFolder = loadResult.Catalog.RegisteredFolders[0];
            Assert.Equal("folder-1", loadedFolder.Id);
            Assert.Equal("C:\\Data", loadedFolder.RootPath);
            Assert.Equal(created, loadedFolder.CreatedAtUtc);
            Assert.Equal(verified, loadedFolder.LastVerifiedAtUtc);
            var loadedFile = Assert.Single(loadedFolder.Files);
            Assert.Equal(files[0], loadedFile);

            var loadedEmptyFolder = loadResult.Catalog.RegisteredFolders[1];
            Assert.Equal("folder-2", loadedEmptyFolder.Id);
            Assert.Equal("C:\\Empty", loadedEmptyFolder.RootPath);
            Assert.Equal(created.AddDays(1), loadedEmptyFolder.CreatedAtUtc);
            Assert.Null(loadedEmptyFolder.LastVerifiedAtUtc);
            Assert.Empty(loadedEmptyFolder.Files);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Save_WritesRequiredCamelCaseSchema()
    {
        var root = CreateTempDirectory();
        try
        {
            var catalogPath = Path.Combine(root, "catalog.json");
            var timestamp = new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
            var fingerprint = new FileFingerprint("file.txt", "abc", 3, timestamp);
            var catalog = new IntegrityCatalog([new RegisteredFolder("id", "root", timestamp, null, [fingerprint])]);

            Assert.True(new CatalogStore(catalogPath).Save(catalog).IsSuccess);

            using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
            var rootElement = document.RootElement;
            AssertPropertyNames(rootElement, "registeredFolders");
            var folder = rootElement.GetProperty("registeredFolders")[0];
            AssertPropertyNames(folder, "id", "rootPath", "createdAtUtc", "lastVerifiedAtUtc", "files");
            var file = folder.GetProperty("files")[0];
            AssertPropertyNames(file, "relativePath", "sha256", "size", "lastModifiedUtc");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Save_WhenExistingCatalogIsMalformed_ReturnsFailureAndPreservesBytes()
    {
        var root = CreateTempDirectory();
        try
        {
            var catalogPath = Path.Combine(root, "catalog.json");
            var originalBytes = new byte[] { 0x7B, 0x20, 0xFF, 0x20, 0x7D };
            File.WriteAllBytes(catalogPath, originalBytes);
            var store = new CatalogStore(catalogPath);

            var result = store.Save(IntegrityCatalog.Empty);

            Assert.False(result.IsSuccess);
            Assert.Contains("catalog", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(originalBytes, File.ReadAllBytes(catalogPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Save_WhenExistingCatalogCannotBeReplaced_PreservesOriginalCatalog()
    {
        var root = CreateTempDirectory();
        try
        {
            var catalogPath = Path.Combine(root, "catalog.json");
            var store = new CatalogStore(catalogPath);
            var original = new IntegrityCatalog([new RegisteredFolder("original", "C:\\Original", DateTimeOffset.UtcNow, null, [])]);
            Assert.True(store.Save(original).IsSuccess);
            var originalBytes = File.ReadAllBytes(catalogPath);

            using var lockStream = new FileStream(catalogPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var replacement = new IntegrityCatalog([new RegisteredFolder("replacement", "C:\\Replacement", DateTimeOffset.UtcNow, null, [])]);

            var result = store.Save(replacement);

            Assert.False(result.IsSuccess);
            Assert.Equal(originalBytes, File.ReadAllBytes(catalogPath));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
    [Fact]
    public void Save_WhenParentPathIsAFile_ReturnsTypedFailure()
    {
        var root = CreateTempDirectory();
        try
        {
            var parentFile = Path.Combine(root, "not-a-directory");
            File.WriteAllText(parentFile, "content");
            var store = new CatalogStore(Path.Combine(parentFile, "catalog.json"));

            var result = store.Save(IntegrityCatalog.Empty);

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("written", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveIfUnchanged_MatchingVersion_ReplacesCatalog()
    {
        var root = CreateTempDirectory();
        try
        {
            var catalogPath = Path.Combine(root, "catalog.json");
            var store = new CatalogStore(catalogPath);
            Assert.True(store.Save(Catalog("original")).IsSuccess);
            var loaded = store.Load();

            var result = store.SaveIfUnchanged(Catalog("replacement"), loaded.Version);

            Assert.True(result.IsSuccess);
            Assert.Equal("replacement", Assert.Single(store.Load().Catalog!.RegisteredFolders).Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveIfUnchanged_StaleVersion_PreservesConcurrentCatalogWithNeutralError()
    {
        var root = CreateTempDirectory();
        try
        {
            var catalogPath = Path.Combine(root, "catalog.json");
            var store = new CatalogStore(catalogPath);
            Assert.True(store.Save(Catalog("original")).IsSuccess);
            var staleVersion = store.Load().Version;
            Assert.True(store.Save(Catalog("concurrent")).IsSuccess);
            var concurrentBytes = File.ReadAllBytes(catalogPath);

            var result = store.SaveIfUnchanged(Catalog("replacement"), staleVersion);

            Assert.False(result.IsSuccess);
            Assert.Contains("changed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("verification", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(concurrentBytes, File.ReadAllBytes(catalogPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveIfUnchanged_CatalogAppearsAfterMissingLoad_PreservesCreatedCatalog()
    {
        var root = CreateTempDirectory();
        try
        {
            var catalogPath = Path.Combine(root, "catalog.json");
            var store = new CatalogStore(catalogPath);
            var missingVersion = store.Load().Version;
            Assert.Null(missingVersion);
            Assert.True(store.Save(Catalog("concurrent")).IsSuccess);
            var concurrentBytes = File.ReadAllBytes(catalogPath);

            var result = store.SaveIfUnchanged(Catalog("replacement"), missingVersion);

            Assert.False(result.IsSuccess);
            Assert.Equal(concurrentBytes, File.ReadAllBytes(catalogPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveIfUnchanged_CompetingWriterDuringFinalReplace_CannotOverwriteGuardedMutation()
    {
        var root = CreateTempDirectory();
        try
        {
            var catalogPath = Path.Combine(root, "catalog.json");
            var initialStore = new CatalogStore(catalogPath);
            Assert.True(initialStore.Save(Catalog("original")).IsSuccess);
            var version = initialStore.Load().Version;
            using var atReplace = new ManualResetEventSlim();
            using var continueReplace = new ManualResetEventSlim();
            var guardedStore = new CatalogStore(
                catalogPath,
                () =>
                {
                    atReplace.Set();
                    Assert.True(continueReplace.Wait(TimeSpan.FromSeconds(10)));
                });

            var guardedTask = Task.Run(() => guardedStore.SaveIfUnchanged(Catalog("guarded"), version));
            Assert.True(atReplace.Wait(TimeSpan.FromSeconds(10)));
            var competingTask = Task.Run(() => new CatalogStore(catalogPath).Save(Catalog("competing")));
            var competingResult = await competingTask;
            continueReplace.Set();
            var guardedResult = await guardedTask;

            Assert.False(competingResult.IsSuccess);
            Assert.Contains("modified", competingResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.True(guardedResult.IsSuccess);
            Assert.Equal("guarded", Assert.Single(initialStore.Load().Catalog!.RegisteredFolders).Id);
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveIfUnchanged_ExternalEditAtReplaceBoundary_ReturnsConflictAndPreservesExternalBytes()
    {
        var root = CreateTempDirectory();
        try
        {
            var catalogPath = Path.Combine(root, "catalog.json");
            var initialStore = new CatalogStore(catalogPath);
            Assert.True(initialStore.Save(Catalog("original")).IsSuccess);
            var version = initialStore.Load().Version;
            var externalBytes = System.Text.Encoding.UTF8.GetBytes("""{"registeredFolders":[]}""");
            var guardedStore = new CatalogStore(
                catalogPath,
                () => File.WriteAllBytes(catalogPath, externalBytes));

            var result = guardedStore.SaveIfUnchanged(Catalog("replacement"), version);

            Assert.False(result.IsSuccess);
            Assert.Contains("changed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(externalBytes, File.ReadAllBytes(catalogPath));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveIfUnchanged_WhenFilesystemMutationLockIsHeld_ReturnsFailureAndPreservesCatalog()
    {
        var root = CreateTempDirectory();
        try
        {
            var catalogPath = Path.Combine(root, "catalog.json");
            var store = new CatalogStore(catalogPath);
            Assert.True(store.Save(Catalog("original")).IsSuccess);
            var version = store.Load().Version;
            var originalBytes = File.ReadAllBytes(catalogPath);
            using var mutationLock = new FileStream(
                catalogPath + ".lock",
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            var result = new CatalogStore(catalogPath).SaveIfUnchanged(Catalog("replacement"), version);

            Assert.False(result.IsSuccess);
            Assert.Contains("modified", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(originalBytes, File.ReadAllBytes(catalogPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveIfUnchanged_RealAndDirectoryAliasPaths_ShareMutationLock()
    {
        var root = CreateTempDirectory();
        try
        {
            var realDirectory = Directory.CreateDirectory(Path.Combine(root, "real")).FullName;
            var aliasDirectory = Path.Combine(root, "alias");
            try
            {
                Directory.CreateSymbolicLink(aliasDirectory, realDirectory);
            }
            catch (Exception ex) when (
                ex is IOException
                or UnauthorizedAccessException
                or PlatformNotSupportedException)
            {
                return;
            }

            var realPath = Path.Combine(realDirectory, "catalog.json");
            var aliasPath = Path.Combine(aliasDirectory, "catalog.json");
            var initialStore = new CatalogStore(realPath);
            Assert.True(initialStore.Save(Catalog("original")).IsSuccess);
            var version = initialStore.Load().Version;
            using var atReplace = new ManualResetEventSlim();
            using var continueReplace = new ManualResetEventSlim();
            var guardedStore = new CatalogStore(
                realPath,
                () =>
                {
                    atReplace.Set();
                    Assert.True(continueReplace.Wait(TimeSpan.FromSeconds(10)));
                });

            var guardedTask = Task.Run(() => guardedStore.SaveIfUnchanged(Catalog("guarded"), version));
            Assert.True(atReplace.Wait(TimeSpan.FromSeconds(10)));
            var aliasResult = await Task.Run(() => new CatalogStore(aliasPath).Save(Catalog("alias")));
            continueReplace.Set();
            var guardedResult = await guardedTask;

            Assert.False(aliasResult.IsSuccess);
            Assert.True(guardedResult.IsSuccess);
            Assert.Equal("guarded", Assert.Single(initialStore.Load().Catalog!.RegisteredFolders).Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static IntegrityCatalog Catalog(string id) =>
        new([
            new RegisteredFolder(
                id,
                Path.GetFullPath(Path.Combine(Path.GetTempPath(), id)),
                new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero),
                null,
                [])
        ]);

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"folderprint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void AssertPropertyNames(JsonElement element, params string[] expected)
    {
        Assert.Equal(expected.Order(), element.EnumerateObject().Select(property => property.Name).Order());
    }
}
