namespace FolderPrint.Core.Models;

public sealed record RegisteredFolder(
    string Id,
    string RootPath,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastVerifiedAtUtc,
    IReadOnlyList<FileFingerprint> Files);
