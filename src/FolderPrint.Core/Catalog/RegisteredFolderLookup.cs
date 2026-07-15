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

        var relativePaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in folder.Files)
        {
            if (file is null || !IsSafeRelativePath(file.RelativePath) || !IsSha256(file.Sha256))
            {
                return "Catalog contains an invalid file fingerprint entry.";
            }

            if (!relativePaths.Add(file.RelativePath))
            {
                return "Catalog contains duplicate file fingerprint paths.";
            }
        }

        return null;
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool IsSafeRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || Path.IsPathRooted(value)
            || value[0] is '/' or '\\'
            || (value.Length >= 2 && char.IsAsciiLetter(value[0]) && value[1] == ':'))
        {
            return false;
        }

        var segments = value.Split(['/', '\\']);
        return segments.All(segment => segment.Length > 0 && segment is not "." and not "..");
    }

    private static RegisteredFolderLookupResult CatalogError(string normalizedRootPath, string message) =>
        RegisteredFolderLookupResult.Failure(RegisteredFolderLookupStatus.CatalogError, message, normalizedRootPath);
}
