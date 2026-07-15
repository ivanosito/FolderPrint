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

        var catalogValidation = CatalogValidator.Validate(catalog);
        if (!catalogValidation.IsSuccess)
        {
            return CatalogError(normalizedRootPath, catalogValidation.ErrorMessage!);
        }

        var matches = new List<(RegisteredFolder Folder, int Index)>();
        for (var index = 0; index < catalog.RegisteredFolders.Count; index++)
        {
            var registeredFolder = catalog.RegisteredFolders[index];
            var registeredRootPath = RegistrationService.NormalizeRootPath(registeredFolder.RootPath);

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

    private static RegisteredFolderLookupResult CatalogError(string normalizedRootPath, string message) =>
        RegisteredFolderLookupResult.Failure(RegisteredFolderLookupStatus.CatalogError, message, normalizedRootPath);
}
