using System.Security.Cryptography;
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
            var bytes = File.ReadAllBytes(catalogPath);
            var catalog = JsonSerializer.Deserialize<IntegrityCatalog>(bytes, JsonOptions);
            if (catalog?.RegisteredFolders is null)
            {
                return CatalogLoadResult.CatalogError("Catalog JSON does not contain a registered folders collection.");
            }

            return CatalogLoadResult.Success(catalog, ComputeVersion(bytes));
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

    public CatalogSaveResult SaveIfUnchanged(IntegrityCatalog catalog, string? expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var current = Load();
        if (!current.IsSuccess)
        {
            return CatalogSaveResult.CatalogError(
                $"Catalog could not be written because the existing catalog is invalid or unreadable: {current.ErrorMessage}");
        }

        if (!StringComparer.Ordinal.Equals(current.Version, expectedVersion))
        {
            return CatalogSaveResult.CatalogError("Catalog changed during verification; no verification timestamp was saved.");
        }

        return Save(catalog);
    }

    private static string ComputeVersion(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));
}
