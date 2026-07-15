using FolderPrint.Core.Models;
using FolderPrint.Core.Registration;

namespace FolderPrint.Core.Catalog;

public static class CatalogValidator
{
    public static CatalogValidationResult Validate(IntegrityCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (catalog.RegisteredFolders is null)
        {
            return Error("Catalog registered folders are missing.");
        }

        var normalizedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in catalog.RegisteredFolders)
        {
            var validationError = ValidateFolder(folder);
            if (validationError is not null)
            {
                return Error(validationError);
            }

            string normalizedRoot;
            try
            {
                normalizedRoot = RegistrationService.NormalizeRootPath(folder!.RootPath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return Error($"Catalog contains an invalid registered folder path: {ex.Message}");
            }

            if (!normalizedRoots.Add(normalizedRoot))
            {
                return Error($"Catalog contains multiple registrations for folder: {normalizedRoot}");
            }
        }

        return CatalogValidationResult.Success();
    }

    private static string? ValidateFolder(RegisteredFolder? folder)
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

    private static CatalogValidationResult Error(string message) =>
        CatalogValidationResult.CatalogError(message);
}
