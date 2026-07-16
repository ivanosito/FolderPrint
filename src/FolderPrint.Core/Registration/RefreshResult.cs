namespace FolderPrint.Core.Registration;

public sealed record RefreshResult(
    RefreshStatus Status,
    string? RootPath,
    int FileCount,
    IReadOnlyList<string> UnreadableFiles,
    string? ErrorMessage)
{
    public static RefreshResult Success(string rootPath, int fileCount) =>
        new(RefreshStatus.Success, rootPath, fileCount, Array.Empty<string>(), null);

    public static RefreshResult Failure(
        RefreshStatus status,
        string? rootPath,
        string errorMessage,
        IReadOnlyList<string>? unreadableFiles = null) =>
        new(status, rootPath, 0, unreadableFiles ?? Array.Empty<string>(), errorMessage);
}
