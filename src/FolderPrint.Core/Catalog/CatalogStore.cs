using System.Text.Json;

namespace FolderPrint.Core.Catalog;

public sealed class CatalogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string catalogPath;

    public CatalogStore(string catalogPath)
    {
        this.catalogPath = catalogPath;
    }

    public string CatalogPath => catalogPath;

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

    public CatalogSaveResult Save(IntegrityCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (File.Exists(catalogPath))
        {
            var existingCatalog = Load();
            if (!existingCatalog.IsSuccess)
            {
                return CatalogSaveResult.CatalogError(
                    $"Catalog could not be written because the existing catalog is invalid or unreadable: {existingCatalog.ErrorMessage}");
            }
        }

        string? temporaryPath = null;

        try
        {
            var directory = Path.GetDirectoryName(catalogPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            temporaryPath = $"{catalogPath}.{Guid.NewGuid():N}.tmp";
            using (var stream = File.Create(temporaryPath))
            {
                JsonSerializer.Serialize(stream, catalog, JsonOptions);
            }

            File.Move(temporaryPath, catalogPath, overwrite: true);
            temporaryPath = null;
            return CatalogSaveResult.Success();
        }
        catch (JsonException ex)
        {
            return CatalogSaveResult.CatalogError($"Catalog could not be serialized: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            return CatalogSaveResult.CatalogError($"Catalog could not be serialized: {ex.Message}");
        }
        catch (IOException ex)
        {
            return CatalogSaveResult.CatalogError($"Catalog could not be written: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return CatalogSaveResult.CatalogError($"Catalog could not be written: {ex.Message}");
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
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
