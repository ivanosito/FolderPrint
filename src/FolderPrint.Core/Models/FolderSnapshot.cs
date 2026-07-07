namespace FolderPrint.Core.Models;

public sealed record FolderSnapshot(
    string RootPath,
    DateTimeOffset ScannedAtUtc,
    IReadOnlyList<FileFingerprint> Files,
    IReadOnlyList<string> UnreadableFiles);
