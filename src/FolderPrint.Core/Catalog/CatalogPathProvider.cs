namespace FolderPrint.Core.Catalog;

public sealed class CatalogPathProvider
{
    public string GetCatalogPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "FolderPrint", "catalog.json");
    }
}
