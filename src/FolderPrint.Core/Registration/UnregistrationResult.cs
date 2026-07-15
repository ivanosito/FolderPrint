namespace FolderPrint.Core.Registration;

public sealed record UnregistrationResult(
    UnregistrationStatus Status,
    string? RootPath,
    string? ErrorMessage)
{
    public static UnregistrationResult Success(string rootPath) =>
        new(UnregistrationStatus.Success, rootPath, null);

    public static UnregistrationResult Failure(
        UnregistrationStatus status,
        string? rootPath,
        string errorMessage) =>
        new(status, rootPath, errorMessage);
}
