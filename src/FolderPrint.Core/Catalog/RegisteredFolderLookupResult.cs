using FolderPrint.Core.Models;

namespace FolderPrint.Core.Catalog;

public sealed record RegisteredFolderLookupResult(
    RegisteredFolderLookupStatus Status,
    RegisteredFolder? RegisteredFolder,
    int RegisteredFolderIndex,
    string? NormalizedRootPath,
    string? ErrorMessage)
{
    public static RegisteredFolderLookupResult Success(
        RegisteredFolder registeredFolder,
        int registeredFolderIndex,
        string normalizedRootPath) =>
        new(
            RegisteredFolderLookupStatus.Success,
            registeredFolder,
            registeredFolderIndex,
            normalizedRootPath,
            null);

    public static RegisteredFolderLookupResult Failure(
        RegisteredFolderLookupStatus status,
        string errorMessage,
        string? normalizedRootPath = null) =>
        new(status, null, -1, normalizedRootPath, errorMessage);
}
