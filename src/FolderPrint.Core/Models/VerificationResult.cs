namespace FolderPrint.Core.Models;

public sealed record VerificationResult(
    string RootPath,
    DateTimeOffset VerifiedAtUtc,
    IReadOnlyList<FileChange> Changes,
    IReadOnlyList<IReadOnlyList<string>> DuplicateGroups,
    IReadOnlyList<string> UnreadableFiles)
{
    public bool HasDifferences => Changes.Count > 0 || DuplicateGroups.Count > 0 || UnreadableFiles.Count > 0;
}
