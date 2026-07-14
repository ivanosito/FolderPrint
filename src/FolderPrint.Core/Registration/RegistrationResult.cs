namespace FolderPrint.Core.Registration;

public sealed record RegistrationResult(
    RegistrationStatus Status,
    string? RootPath,
    int FileCount,
    IReadOnlyList<string> UnreadableFiles,
    string? ErrorMessage)
{
    public static RegistrationResult Success(string rootPath, int fileCount) =>
        new(RegistrationStatus.Success, rootPath, fileCount, Array.Empty<string>(), null);

    public static RegistrationResult Failure(
        RegistrationStatus status,
        string? rootPath,
        string errorMessage,
        IReadOnlyList<string>? unreadableFiles = null) =>
        new(status, rootPath, 0, unreadableFiles ?? Array.Empty<string>(), errorMessage);
}
