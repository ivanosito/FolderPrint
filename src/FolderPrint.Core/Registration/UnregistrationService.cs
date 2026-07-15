using FolderPrint.Core.Catalog;

namespace FolderPrint.Core.Registration;

public sealed class UnregistrationService
{
    private readonly CatalogStore catalogStore;
    private readonly Action? beforeSave;

    public UnregistrationService(CatalogStore catalogStore)
        : this(catalogStore, null)
    {
    }

    internal UnregistrationService(CatalogStore catalogStore, Action? beforeSave)
    {
        this.catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
        this.beforeSave = beforeSave;
    }

    public UnregistrationResult Unregister(string requestedRootPath)
    {
        string normalizedRootPath;
        try
        {
            normalizedRootPath = RegistrationService.NormalizeRootPath(requestedRootPath);
        }
        catch (Exception ex) when (
            ex is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return UnregistrationResult.Failure(
                UnregistrationStatus.InvalidRoot,
                null,
                $"Folder path is invalid: {ex.Message}");
        }

        var loadResult = catalogStore.Load();
        if (!loadResult.IsSuccess)
        {
            return UnregistrationResult.Failure(
                UnregistrationStatus.CatalogError,
                normalizedRootPath,
                loadResult.ErrorMessage ?? "Catalog could not be loaded.");
        }

        var lookup = RegisteredFolderLookup.Find(loadResult.Catalog!, normalizedRootPath);
        if (lookup.Status != RegisteredFolderLookupStatus.Success)
        {
            return UnregistrationResult.Failure(
                lookup.Status switch
                {
                    RegisteredFolderLookupStatus.InvalidRoot => UnregistrationStatus.InvalidRoot,
                    RegisteredFolderLookupStatus.NotFound => UnregistrationStatus.NotFound,
                    RegisteredFolderLookupStatus.CatalogError => UnregistrationStatus.CatalogError,
                    _ => UnregistrationStatus.CatalogError
                },
                lookup.NormalizedRootPath,
                lookup.ErrorMessage ?? "Folder could not be unregistered.");
        }

        var updatedCatalog = loadResult.Catalog!.RemoveRegisteredFolderAt(
            lookup.RegisteredFolderIndex);
        beforeSave?.Invoke();
        var saveResult = catalogStore.SaveIfUnchanged(updatedCatalog, loadResult.Version);
        if (!saveResult.IsSuccess)
        {
            return UnregistrationResult.Failure(
                UnregistrationStatus.CatalogError,
                lookup.RegisteredFolder!.RootPath,
                saveResult.ErrorMessage ?? "Catalog could not be saved.");
        }

        return UnregistrationResult.Success(lookup.RegisteredFolder!.RootPath);
    }
}
