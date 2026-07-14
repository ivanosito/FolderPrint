using FolderPrint.Core.Models;

namespace FolderPrint.Core.Verification;

public sealed class VerificationService
{
    public VerificationResult Compare(RegisteredFolder baseline, FolderSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);

        var baselineFiles = baseline.Files
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();
        var currentFiles = current.Files
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();
        var changes = new List<FileChange>(baselineFiles.Length + currentFiles.Length);

        var baselineIndex = 0;
        var currentIndex = 0;

        while (baselineIndex < baselineFiles.Length || currentIndex < currentFiles.Length)
        {
            if (baselineIndex >= baselineFiles.Length)
            {
                changes.Add(CreateNew(currentFiles[currentIndex++]));
                continue;
            }

            if (currentIndex >= currentFiles.Length)
            {
                changes.Add(CreateMissing(baselineFiles[baselineIndex++]));
                continue;
            }

            var baselineFile = baselineFiles[baselineIndex];
            var currentFile = currentFiles[currentIndex];
            var pathComparison = StringComparer.Ordinal.Compare(
                baselineFile.RelativePath,
                currentFile.RelativePath);

            if (pathComparison < 0)
            {
                changes.Add(CreateMissing(baselineFile));
                baselineIndex++;
            }
            else if (pathComparison > 0)
            {
                changes.Add(CreateNew(currentFile));
                currentIndex++;
            }
            else
            {
                changes.Add(CreateSamePathChange(baselineFile, currentFile));
                baselineIndex++;
                currentIndex++;
            }
        }

        return new VerificationResult(
            baseline.RootPath,
            current.ScannedAtUtc,
            changes,
            [],
            []);
    }

    private static FileChange CreateSamePathChange(
        FileFingerprint baseline,
        FileFingerprint current)
    {
        var unchanged = StringComparer.Ordinal.Equals(baseline.Sha256, current.Sha256);
        return new FileChange(
            unchanged ? FileChangeType.Unchanged : FileChangeType.Modified,
            baseline.RelativePath,
            current.RelativePath,
            current.Sha256,
            unchanged ? "File is unchanged." : "File content was modified.");
    }

    private static FileChange CreateMissing(FileFingerprint baseline) =>
        new(
            FileChangeType.Missing,
            baseline.RelativePath,
            null,
            baseline.Sha256,
            "File is missing from the current snapshot.");

    private static FileChange CreateNew(FileFingerprint current) =>
        new(
            FileChangeType.New,
            null,
            current.RelativePath,
            current.Sha256,
            "File is new in the current snapshot.");
}