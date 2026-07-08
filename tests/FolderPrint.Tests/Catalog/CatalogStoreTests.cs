using FolderPrint.Core.Catalog;

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
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var catalogPath = Path.Combine(directory, "catalog.json");
        File.WriteAllText(catalogPath, "{ this is not valid json");
        var store = new CatalogStore(catalogPath);

        var result = store.Load();

        Assert.False(result.IsSuccess);
        Assert.Null(result.Catalog);
        Assert.Contains("catalog", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("{ this is not valid json", File.ReadAllText(catalogPath));
    }
}
