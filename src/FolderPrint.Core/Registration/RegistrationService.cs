using FolderPrint.Core.Catalog;
using FolderPrint.Core.Models;
using FolderPrint.Core.Scanning;

namespace FolderPrint.Core.Registration;

public sealed class RegistrationService
{
    private readonly CatalogStore catalogStore;
    private readonly Func<string, FolderSnapshot> scanFolder;
    private readonly Func<string> idFactory;
    private readonly Func<DateTimeOffset> utcNow;

    public RegistrationService(
        CatalogStore catalogStore,
        FolderScanner folderScanner,
        Func<string>? idFactory = null,
        Func<DateTimeOffset>? utcNow = null)
        : this(catalogStore, (folderScanner ?? throw new ArgumentNullException(nameof(folderScanner))).Scan, idFactory, utcNow)
    {
    }

    public RegistrationService(
        CatalogStore catalogStore,
        Func<string, FolderSnapshot> scanFolder,
        Func<string>? idFactory = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        this.catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
        this.scanFolder = scanFolder ?? throw new ArgumentNullException(nameof(scanFolder));
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
            if (registeredFolder is null)
            {
                return RegistrationResult.Failure(
                    RegistrationStatus.CatalogError,
                    normalizedRootPath,
                    "Catalog contains a null registered folder entry.");
            }

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

        string normalizedCatalogPath;
        try
        {
            normalizedCatalogPath = Path.GetFullPath(catalogStore.CatalogPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return RegistrationResult.Failure(
                RegistrationStatus.CatalogError,
                normalizedRootPath,
                $"Catalog path is invalid: {ex.Message}");
        }

        if (IsPathInsideRoot(normalizedCatalogPath, normalizedRootPath))
        {
            return RegistrationResult.Failure(
                RegistrationStatus.CatalogInsideRoot,
                normalizedRootPath,
                "The folder cannot be registered because it contains the active FolderPrint catalog.");
        }

        try
        {
            var attributes = File.GetAttributes(normalizedRootPath);
            if ((attributes & FileAttributes.Directory) == 0)
            {
                return RegistrationResult.Failure(
                    RegistrationStatus.InvalidRoot,
                    normalizedRootPath,
                    $"Folder was not found or is not a directory: {normalizedRootPath}");
            }
        }
        catch (FileNotFoundException)
        {
            return InvalidRoot(normalizedRootPath);
        }
        catch (DirectoryNotFoundException)
        {
            return InvalidRoot(normalizedRootPath);
        }
        catch (IOException ex)
        {
            return ScanFailure(normalizedRootPath, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ScanFailure(normalizedRootPath, ex);
        }

        FolderSnapshot snapshot;
        try
        {
            snapshot = scanFolder(normalizedRootPath);
        }
        catch (IOException ex)
        {
            return ScanFailure(normalizedRootPath, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ScanFailure(normalizedRootPath, ex);
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
        var saveResult = catalogStore.SaveIfUnchanged(updatedCatalog, loadResult.Version);
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

    internal static bool IsPathInsideRoot(string path, string rootPath)
    {
        var rootWithSeparator = Path.EndsInDirectorySeparator(rootPath)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        return path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static RegistrationResult InvalidRoot(string normalizedRootPath) =>
        RegistrationResult.Failure(
            RegistrationStatus.InvalidRoot,
            normalizedRootPath,
            $"Folder was not found or is not a directory: {normalizedRootPath}");

    private static RegistrationResult ScanFailure(string normalizedRootPath, Exception exception) =>
        RegistrationResult.Failure(
            RegistrationStatus.ScanError,
            normalizedRootPath,
            $"Folder scan failed: {exception.Message}");
}
