using FolderPrint.Core.Catalog;
using FolderPrint.Core.Scanning;

namespace FolderPrint.Core.Registration;

public sealed class RegistrationService
{
    private readonly CatalogStore catalogStore;
    private readonly FolderScanner folderScanner;
    private readonly Func<string> idFactory;
    private readonly Func<DateTimeOffset> utcNow;

    public RegistrationService(
        CatalogStore catalogStore,
        FolderScanner folderScanner,
        Func<string>? idFactory = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        this.catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
        this.folderScanner = folderScanner ?? throw new ArgumentNullException(nameof(folderScanner));
        this.idFactory = idFactory ?? (() => Guid.NewGuid().ToString("N"));
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public RegistrationResult Register(string requestedRootPath)
    {
        string normalizedRootPath;
        try
        {
            normalizedRootPath = NormalizeRootPath(requestedRootPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return RegistrationResult.Failure(
                RegistrationStatus.InvalidRoot,
                null,
                $"Folder path is invalid: {ex.Message}");
        }

        var loadResult = catalogStore.Load();
        if (!loadResult.IsSuccess)
        {
            return RegistrationResult.Failure(
                RegistrationStatus.CatalogError,
                normalizedRootPath,
                loadResult.ErrorMessage ?? "Catalog could not be loaded.");
        }

        var catalog = loadResult.Catalog!;
        foreach (var registeredFolder in catalog.RegisteredFolders)
        {
            string registeredRootPath;
            try
            {
                registeredRootPath = NormalizeRootPath(registeredFolder.RootPath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return RegistrationResult.Failure(
                    RegistrationStatus.CatalogError,
                    normalizedRootPath,
                    $"Catalog contains an invalid registered folder path: {ex.Message}");
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(registeredRootPath, normalizedRootPath))
            {
                return RegistrationResult.Failure(
                    RegistrationStatus.AlreadyRegistered,
                    normalizedRootPath,
                    $"Folder is already registered: {normalizedRootPath}");
            }
        }

        if (!Directory.Exists(normalizedRootPath))
        {
            return RegistrationResult.Failure(
                RegistrationStatus.InvalidRoot,
                normalizedRootPath,
                $"Folder was not found or is not a directory: {normalizedRootPath}");
        }

        FolderPrint.Core.Models.FolderSnapshot snapshot;
        try
        {
            snapshot = folderScanner.Scan(normalizedRootPath);
        }
        catch (IOException ex)
        {
            return RegistrationResult.Failure(
                RegistrationStatus.ScanError,
                normalizedRootPath,
                $"Folder scan failed: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return RegistrationResult.Failure(
                RegistrationStatus.ScanError,
                normalizedRootPath,
                $"Folder scan failed: {ex.Message}");
        }

        if (snapshot.UnreadableFiles.Count > 0)
        {
            return RegistrationResult.Failure(
                RegistrationStatus.ScanError,
                normalizedRootPath,
                "Folder registration failed because one or more files could not be read.",
                snapshot.UnreadableFiles.ToArray());
        }

        var id = idFactory();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var createdAtUtc = utcNow().ToUniversalTime();
        var updatedCatalog = catalog.AddRegisteredFolder(id, snapshot, createdAtUtc);
        var saveResult = catalogStore.Save(updatedCatalog);
        if (!saveResult.IsSuccess)
        {
            return RegistrationResult.Failure(
                RegistrationStatus.CatalogError,
                normalizedRootPath,
                saveResult.ErrorMessage ?? "Catalog could not be saved.");
        }

        return RegistrationResult.Success(normalizedRootPath, snapshot.Files.Count);
    }

    public static string NormalizeRootPath(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
    }
}
