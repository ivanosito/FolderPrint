using System.Text.Json;

namespace FolderPrint.Core.Catalog;

public sealed class CatalogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string catalogPath;

    public CatalogStore(string catalogPath)
    {
        this.catalogPath = catalogPath;
    }

    public CatalogLoadResult Load()
    {
        if (!File.Exists(catalogPath))
        {
            return CatalogLoadResult.Success(IntegrityCatalog.Empty);
        }

        try
        {
            using var stream = File.OpenRead(catalogPath);
            var catalog = JsonSerializer.Deserialize<IntegrityCatalog>(stream, JsonOptions);
            return CatalogLoadResult.Success(Normalize(catalog));
        }
        catch (JsonException ex)
        {
            return CatalogLoadResult.CatalogError($"Catalog JSON is invalid: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            return CatalogLoadResult.CatalogError($"Catalog JSON is invalid: {ex.Message}");
        }
        catch (IOException ex)
        {
            return CatalogLoadResult.CatalogError($"Catalog could not be read: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return CatalogLoadResult.CatalogError($"Catalog could not be read: {ex.Message}");
        }
    }

    private static IntegrityCatalog Normalize(IntegrityCatalog? catalog)
    {
        if (catalog is null || catalog.RegisteredFolders is null)
        {
            return IntegrityCatalog.Empty;
        }

        return catalog;
    }
}
