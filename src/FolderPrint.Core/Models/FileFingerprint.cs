namespace FolderPrint.Core.Models;

public sealed record FileFingerprint(
    string RelativePath,
    string Sha256,
    long Size,
    DateTimeOffset LastModifiedUtc);
