using FolderPrint.Core.Models;
using FolderPrint.Core.Registration;

namespace FolderPrint.Core.Catalog;

public static class RegisteredFolderLookup
{
    public static RegisteredFolderLookupResult Find(IntegrityCatalog catalog, string requestedRootPath)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        string normalizedRootPath;
        try
        {
            normalizedRootPath = RegistrationService.NormalizeRootPath(requestedRootPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return RegisteredFolderLookupResult.Failure(
                RegisteredFolderLookupStatus.InvalidRoot,
                $"Folder path is invalid: {ex.Message}");
        }

        if (catalog.RegisteredFolders is null)
        {
            return CatalogError(normalizedRootPath, "Catalog registered folders are missing.");
        }

        var matches = new List<(RegisteredFolder Folder, int Index)>();
        for (var index = 0; index < catalog.RegisteredFolders.Count; index++)
        {
            var registeredFolder = catalog.RegisteredFolders[index];
            var validationError = Validate(registeredFolder);
            if (validationError is not null)
            {
                return CatalogError(normalizedRootPath, validationError);
            }

            string registeredRootPath;
            try
            {
                registeredRootPath = RegistrationService.NormalizeRootPath(registeredFolder!.RootPath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return CatalogError(normalizedRootPath, $"Catalog contains an invalid registered folder path: {ex.Message}");
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(registeredRootPath, normalizedRootPath))
            {
                matches.Add((registeredFolder, index));
            }
        }

        return matches.Count switch
        {
            0 => RegisteredFolderLookupResult.Failure(
                RegisteredFolderLookupStatus.NotFound,
                $"Folder is not registered: {normalizedRootPath}",
                normalizedRootPath),
            1 => RegisteredFolderLookupResult.Success(matches[0].Folder, matches[0].Index, normalizedRootPath),
            _ => CatalogError(normalizedRootPath, $"Catalog contains multiple registrations for folder: {normalizedRootPath}")
        };
    }

    private static string? Validate(RegisteredFolder? folder)
    {
        if (folder is null)
        {
            return "Catalog contains a null registered folder entry.";
        }

        if (string.IsNullOrWhiteSpace(folder.Id) || string.IsNullOrWhiteSpace(folder.RootPath) || folder.Files is null)
        {
            return "Catalog contains an invalid registered folder entry.";
        }

        foreach (var file in folder.Files)
        {
            if (file is null || string.IsNullOrWhiteSpace(file.RelativePath) || string.IsNullOrWhiteSpace(file.Sha256))
            {
                return "Catalog contains an invalid file fingerprint entry.";
            }
        }

        return null;
    }

    private static RegisteredFolderLookupResult CatalogError(string normalizedRootPath, string message) =>
        RegisteredFolderLookupResult.Failure(RegisteredFolderLookupStatus.CatalogError, message, normalizedRootPath);
}
