using FolderPrint.Core.Catalog;
using FolderPrint.Core.Models;
using FolderPrint.Core.Scanning;

namespace FolderPrint.Core.Registration;

public sealed class RefreshService
{
    private readonly CatalogStore catalogStore;
    private readonly Func<string, FolderSnapshot> scanFolder;
    private readonly Func<DateTimeOffset> utcNow;
    private readonly Action? beforeSave;

    public RefreshService(
        CatalogStore catalogStore,
        FolderScanner folderScanner,
        Func<DateTimeOffset>? utcNow = null)
        : this(
            catalogStore,
            (folderScanner ?? throw new ArgumentNullException(nameof(folderScanner))).Scan,
            utcNow,
            null)
    {
    }

    public RefreshService(
        CatalogStore catalogStore,
        Func<string, FolderSnapshot> scanFolder,
        Func<DateTimeOffset>? utcNow = null)
        : this(catalogStore, scanFolder, utcNow, null)
    {
    }

    internal RefreshService(
        CatalogStore catalogStore,
        Func<string, FolderSnapshot> scanFolder,
        Func<DateTimeOffset>? utcNow,
        Action? beforeSave)
    {
        this.catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
        this.scanFolder = scanFolder ?? throw new ArgumentNullException(nameof(scanFolder));
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        this.beforeSave = beforeSave;
    }

    public RefreshResult Refresh(string requestedRootPath)
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
            return RefreshResult.Failure(
                RefreshStatus.InvalidRoot,
                null,
                $"Folder path is invalid: {ex.Message}");
        }

        var loadResult = catalogStore.Load();
        if (!loadResult.IsSuccess)
        {
            return RefreshResult.Failure(
                RefreshStatus.CatalogError,
                normalizedRootPath,
                loadResult.ErrorMessage ?? "Catalog could not be loaded.");
        }

        var lookup = RegisteredFolderLookup.Find(loadResult.Catalog!, normalizedRootPath);
        if (lookup.Status != RegisteredFolderLookupStatus.Success)
        {
            return RefreshResult.Failure(
                lookup.Status switch
                {
                    RegisteredFolderLookupStatus.InvalidRoot => RefreshStatus.InvalidRoot,
                    RegisteredFolderLookupStatus.NotFound => RefreshStatus.NotFound,
                    RegisteredFolderLookupStatus.CatalogError => RefreshStatus.CatalogError,
                    _ => RefreshStatus.CatalogError
                },
                lookup.NormalizedRootPath,
                lookup.ErrorMessage ?? "Folder could not be refreshed.");
        }

        string normalizedCatalogPath;
        try
        {
            normalizedCatalogPath = Path.GetFullPath(catalogStore.CatalogPath);
        }
        catch (Exception ex) when (
            ex is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return RefreshResult.Failure(
                RefreshStatus.CatalogError,
                lookup.RegisteredFolder!.RootPath,
                $"Catalog path is invalid: {ex.Message}");
        }

        if (RegistrationService.IsPathInsideRoot(normalizedCatalogPath, normalizedRootPath))
        {
            return RefreshResult.Failure(
                RefreshStatus.CatalogError,
                lookup.RegisteredFolder!.RootPath,
                "The folder cannot be refreshed because it contains the active FolderPrint catalog.");
        }

        var rootValidation = ValidateRoot(normalizedRootPath);
        if (rootValidation is not null)
        {
            return rootValidation;
        }

        FolderSnapshot snapshot;
        try
        {
            snapshot = scanFolder(normalizedRootPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ScanFailure(normalizedRootPath, ex);
        }

        if (snapshot.UnreadableFiles.Count > 0)
        {
            return RefreshResult.Failure(
                RefreshStatus.ScanError,
                lookup.RegisteredFolder!.RootPath,
                "Folder refresh failed because one or more files could not be read.",
                snapshot.UnreadableFiles.OrderBy(path => path, StringComparer.Ordinal).ToArray());
        }

        var refreshedAtUtc = utcNow().ToUniversalTime();
        var updatedCatalog = loadResult.Catalog!.WithRefreshedBaseline(
            lookup.RegisteredFolderIndex,
            snapshot,
            refreshedAtUtc);
        beforeSave?.Invoke();
        var saveResult = catalogStore.SaveIfUnchanged(updatedCatalog, loadResult.Version);
        if (!saveResult.IsSuccess)
        {
            return RefreshResult.Failure(
                RefreshStatus.CatalogError,
                lookup.RegisteredFolder!.RootPath,
                saveResult.ErrorMessage ?? "Catalog could not be saved.");
        }

        return RefreshResult.Success(lookup.RegisteredFolder!.RootPath, snapshot.Files.Count);
    }

    private static RefreshResult? ValidateRoot(string normalizedRootPath)
    {
        try
        {
            var attributes = File.GetAttributes(normalizedRootPath);
            return (attributes & FileAttributes.Directory) == 0
                ? RootNotFound(normalizedRootPath)
                : null;
        }
        catch (FileNotFoundException)
        {
            return RootNotFound(normalizedRootPath);
        }
        catch (DirectoryNotFoundException)
        {
            return RootNotFound(normalizedRootPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ScanFailure(normalizedRootPath, ex);
        }
    }

    private static RefreshResult RootNotFound(string normalizedRootPath) =>
        RefreshResult.Failure(
            RefreshStatus.NotFound,
            normalizedRootPath,
            $"Folder was not found or is not a directory: {normalizedRootPath}");

    private static RefreshResult ScanFailure(string normalizedRootPath, Exception exception) =>
        RefreshResult.Failure(
            RefreshStatus.ScanError,
            normalizedRootPath,
            $"Folder scan failed: {exception.Message}");
}
