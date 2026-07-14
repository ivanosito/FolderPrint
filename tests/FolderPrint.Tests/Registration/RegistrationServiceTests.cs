using FolderPrint.Core.Catalog;
using FolderPrint.Core.Registration;
using FolderPrint.Core.Scanning;

namespace FolderPrint.Tests.Registration;

public sealed class RegistrationServiceTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), $"FolderPrint-{Guid.NewGuid():N}");

    public RegistrationServiceTests() => Directory.CreateDirectory(tempDirectory);

    [Fact]
    public void Register_ReadableNestedFolder_PersistsCompleteStableBaseline()
    {
        var root = CreateDirectory("root");
        var nested = Directory.CreateDirectory(Path.Combine(root, "nested")).FullName;
        File.WriteAllText(Path.Combine(root, "alpha.txt"), "alpha");
        File.WriteAllText(Path.Combine(nested, "beta.txt"), "beta");
        var catalogPath = Path.Combine(tempDirectory, "state", "catalog.json");
        var createdAt = new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(catalogPath, () => "stable-id", () => createdAt);

        var result = service.Register(Path.Combine(root, "."));

        Assert.Equal(RegistrationStatus.Success, result.Status);
        Assert.Equal(2, result.FileCount);
        var firstLoad = new CatalogStore(catalogPath).Load();
        var registered = Assert.Single(firstLoad.Catalog!.RegisteredFolders);
        Assert.Equal("stable-id", registered.Id);
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), registered.RootPath);
        Assert.Equal(createdAt, registered.CreatedAtUtc);
        Assert.Null(registered.LastVerifiedAtUtc);
        Assert.Equal(new[] { "alpha.txt", Path.Combine("nested", "beta.txt") }, registered.Files.Select(file => file.RelativePath));
        Assert.All(registered.Files, file =>
        {
            Assert.Matches("^[0-9a-f]{64}$", file.Sha256);
            Assert.True(file.Size > 0);
            Assert.Equal(TimeSpan.Zero, file.LastModifiedUtc.Offset);
        });
        var secondLoad = new CatalogStore(catalogPath).Load();
        var reloaded = Assert.Single(secondLoad.Catalog!.RegisteredFolders);
        Assert.Equal(registered.Id, reloaded.Id);
        Assert.Equal(registered.RootPath, reloaded.RootPath);
        Assert.Equal(registered.CreatedAtUtc, reloaded.CreatedAtUtc);
        Assert.Equal(registered.Files, reloaded.Files);
    }

    [Fact]
    public void Register_EmptyFolder_PersistsZeroFileBaseline()
    {
        var root = CreateDirectory("empty");
        var catalogPath = Path.Combine(tempDirectory, "catalog.json");

        var result = CreateService(catalogPath).Register(root);

        Assert.Equal(RegistrationStatus.Success, result.Status);
        Assert.Equal(0, result.FileCount);
        Assert.Empty(Assert.Single(new CatalogStore(catalogPath).Load().Catalog!.RegisteredFolders).Files);
    }

    [Fact]
    public void Register_MissingOrFileRoot_ReturnsInvalidRootWithoutCatalogMutation()
    {
        var catalogPath = Path.Combine(tempDirectory, "catalog.json");
        File.WriteAllText(catalogPath, "{\"registeredFolders\":[]}");
        var original = File.ReadAllBytes(catalogPath);
        var filePath = Path.Combine(tempDirectory, "file.txt");
        File.WriteAllText(filePath, "content");
        var service = CreateService(catalogPath);

        Assert.Equal(RegistrationStatus.InvalidRoot, service.Register(Path.Combine(tempDirectory, "missing")).Status);
        Assert.Equal(RegistrationStatus.InvalidRoot, service.Register(filePath).Status);
        Assert.Equal(original, File.ReadAllBytes(catalogPath));
    }

    [Fact]
    public void Register_EquivalentDuplicateRoot_ReturnsAlreadyRegisteredAndPreservesBytes()
    {
        var root = CreateDirectory("duplicate");
        var catalogPath = Path.Combine(tempDirectory, "catalog.json");
        var service = CreateService(catalogPath);
        Assert.Equal(RegistrationStatus.Success, service.Register(root).Status);
        var original = File.ReadAllBytes(catalogPath);

        var result = service.Register(root.ToUpperInvariant() + Path.DirectorySeparatorChar);

        Assert.Equal(RegistrationStatus.AlreadyRegistered, result.Status);
        Assert.Equal(original, File.ReadAllBytes(catalogPath));
        Assert.Single(new CatalogStore(catalogPath).Load().Catalog!.RegisteredFolders);
    }

    [Fact]
    public void Register_MalformedCatalog_ReturnsCatalogErrorAndPreservesBytes()
    {
        var root = CreateDirectory("root-malformed");
        var catalogPath = Path.Combine(tempDirectory, "catalog.json");
        File.WriteAllText(catalogPath, "not-json");
        var original = File.ReadAllBytes(catalogPath);

        var result = CreateService(catalogPath).Register(root);

        Assert.Equal(RegistrationStatus.CatalogError, result.Status);
        Assert.Equal(original, File.ReadAllBytes(catalogPath));
    }

    [Fact]
    public void Register_SaveFailure_ReturnsCatalogErrorWithoutSuccess()
    {
        var root = CreateDirectory("root-save");
        var blockingFile = Path.Combine(tempDirectory, "blocking-file");
        File.WriteAllText(blockingFile, "content");

        var result = CreateService(Path.Combine(blockingFile, "catalog.json")).Register(root);

        Assert.Equal(RegistrationStatus.CatalogError, result.Status);
    }

    [Fact]
    public void Register_UnreadableFile_ReturnsScanErrorAndDoesNotCreateCatalog()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = CreateDirectory("locked");
        var lockedPath = Path.Combine(root, "locked.txt");
        File.WriteAllText(lockedPath, "content");
        var catalogPath = Path.Combine(tempDirectory, "locked-catalog.json");
        using var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = CreateService(catalogPath).Register(root);

        Assert.Equal(RegistrationStatus.ScanError, result.Status);
        Assert.Contains("locked.txt", result.UnreadableFiles);
        Assert.False(File.Exists(catalogPath));
    }

    private RegistrationService CreateService(string catalogPath, Func<string>? idFactory = null, Func<DateTimeOffset>? clock = null) =>
        new(new CatalogStore(catalogPath), new FolderScanner(new FileHasher()), idFactory, clock);

    private string CreateDirectory(string name) => Directory.CreateDirectory(Path.Combine(tempDirectory, name)).FullName;

    public void Dispose()
    {
        try { Directory.Delete(tempDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
