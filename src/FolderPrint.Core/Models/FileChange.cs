namespace FolderPrint.Core.Models;

public sealed record FileChange(
    FileChangeType Type,
    string? BaselineRelativePath,
    string? CurrentRelativePath,
    string? Sha256,
    string Message);
