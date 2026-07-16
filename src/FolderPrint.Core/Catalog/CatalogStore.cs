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
    private readonly Action? beforeReplace;

    public CatalogStore(string catalogPath)
        : this(catalogPath, null)
    {
    }

    internal CatalogStore(string catalogPath, Action? beforeReplace)
    {
        this.catalogPath = catalogPath;
        this.beforeReplace = beforeReplace;
    }

    public string CatalogPath => catalogPath;

    public CatalogLoadResult Load()
    {
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
        catch (FileNotFoundException)
        {
            return CatalogLoadResult.Success(IntegrityCatalog.Empty);
        }
        catch (DirectoryNotFoundException)
        {
            return CatalogLoadResult.Success(IntegrityCatalog.Empty);
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

        return WithMutationLock(() =>
        {
            var existingCatalog = Load();
            if (!existingCatalog.IsSuccess)
            {
                return CatalogSaveResult.CatalogError(
                    $"Catalog could not be written because the existing catalog is invalid or unreadable: {existingCatalog.ErrorMessage}");
            }

            return WriteCatalog(catalog, null, verifyExpectedVersion: false);
        });
    }

    public CatalogSaveResult SaveIfUnchanged(IntegrityCatalog catalog, string? expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return WithMutationLock(() =>
        {
            var current = Load();
            if (!current.IsSuccess)
            {
                return CatalogSaveResult.CatalogError(
                    $"Catalog could not be written because the existing catalog is invalid or unreadable: {current.ErrorMessage}");
            }

            if (!StringComparer.Ordinal.Equals(current.Version, expectedVersion))
            {
                return CatalogSaveResult.CatalogError(
                    "Catalog changed during the operation; the catalog was not updated.");
            }

            return WriteCatalog(catalog, expectedVersion, verifyExpectedVersion: true);
        });
    }

    private CatalogSaveResult WriteCatalog(
        IntegrityCatalog catalog,
        string? expectedVersion,
        bool verifyExpectedVersion)
    {
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
                stream.Flush(flushToDisk: true);
            }

            beforeReplace?.Invoke();
            if (verifyExpectedVersion)
            {
                var finalState = Load();
                if (!finalState.IsSuccess)
                {
                    return CatalogSaveResult.CatalogError(
                        $"Catalog could not be written because the existing catalog is invalid or unreadable: {finalState.ErrorMessage}");
                }

                if (!StringComparer.Ordinal.Equals(finalState.Version, expectedVersion))
                {
                    return CatalogSaveResult.CatalogError(
                        "Catalog changed during the operation; the catalog was not updated.");
                }
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

    private CatalogSaveResult WithMutationLock(Func<CatalogSaveResult> operation)
    {
        FileStream? mutationLock = null;

        try
        {
            var directory = Path.GetDirectoryName(catalogPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                mutationLock = new FileStream(
                    catalogPath + ".lock",
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return CatalogSaveResult.CatalogError(
                    $"Catalog could not be written because it is being modified or its mutation lock could not be acquired: {ex.Message}");
            }

            return operation();
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return CatalogSaveResult.CatalogError($"Catalog could not be written: {ex.Message}");
        }
        finally
        {
            mutationLock?.Dispose();
        }
    }

    private static string ComputeVersion(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));
}
