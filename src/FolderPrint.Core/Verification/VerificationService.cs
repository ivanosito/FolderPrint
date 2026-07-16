using FolderPrint.Core.Models;

namespace FolderPrint.Core.Verification;

public sealed class VerificationService
{
    private static readonly DuplicateFinder DuplicateFinder = new();

    public VerificationResult Compare(RegisteredFolder baseline, FolderSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);

        var baselineFiles = baseline.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
        var currentFiles = current.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
        var samePathChanges = new List<FileChange>();
        var unmatchedBaseline = new List<FileFingerprint>();
        var unmatchedCurrent = new List<FileFingerprint>();

        var baselineIndex = 0;
        var currentIndex = 0;
        while (baselineIndex < baselineFiles.Length || currentIndex < currentFiles.Length)
        {
            if (baselineIndex >= baselineFiles.Length)
            {
                unmatchedCurrent.Add(currentFiles[currentIndex++]);
                continue;
            }

            if (currentIndex >= currentFiles.Length)
            {
                unmatchedBaseline.Add(baselineFiles[baselineIndex++]);
                continue;
            }

            var baselineFile = baselineFiles[baselineIndex];
            var currentFile = currentFiles[currentIndex];
            var pathComparison = StringComparer.Ordinal.Compare(baselineFile.RelativePath, currentFile.RelativePath);

            if (pathComparison < 0)
            {
                unmatchedBaseline.Add(baselineFile);
                baselineIndex++;
            }
            else if (pathComparison > 0)
            {
                unmatchedCurrent.Add(currentFile);
                currentIndex++;
            }
            else
            {
                samePathChanges.Add(CreateSamePathChange(baselineFile, currentFile));
                baselineIndex++;
                currentIndex++;
            }
        }

        var changes = samePathChanges.Concat(ReconcileUnmatched(unmatchedBaseline, unmatchedCurrent))
            .OrderBy(change => change.BaselineRelativePath is null && change.CurrentRelativePath is null)
            .ThenBy(EffectivePath, StringComparer.Ordinal)
            .ThenBy(change => change.Sha256, StringComparer.Ordinal)
            .ThenBy(change => change.Type)
            .ThenBy(change => change.BaselineRelativePath, StringComparer.Ordinal)
            .ThenBy(change => change.CurrentRelativePath, StringComparer.Ordinal)
            .ToArray();

        var duplicateGroups = DuplicateFinder.Find(current with { Files = currentFiles });
        var unreadableFiles = current.UnreadableFiles.OrderBy(path => path, StringComparer.Ordinal).ToArray();

        return new VerificationResult(baseline.RootPath, current.ScannedAtUtc, changes, duplicateGroups, unreadableFiles);
    }

    private static IEnumerable<FileChange> ReconcileUnmatched(
        IReadOnlyList<FileFingerprint> baselineFiles,
        IReadOnlyList<FileFingerprint> currentFiles)
    {
        var baselineByHash = baselineFiles.GroupBy(file => file.Sha256, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var currentByHash = currentFiles.GroupBy(file => file.Sha256, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var hashes = baselineByHash.Keys.Concat(currentByHash.Keys).Distinct(StringComparer.Ordinal).OrderBy(hash => hash, StringComparer.Ordinal);

        foreach (var hash in hashes)
        {
            baselineByHash.TryGetValue(hash, out var baselineCandidates);
            currentByHash.TryGetValue(hash, out var currentCandidates);
            baselineCandidates ??= [];
            currentCandidates ??= [];

            if (baselineCandidates.Length == 1 && currentCandidates.Length == 1)
            {
                yield return CreateMovedOrRenamed(baselineCandidates[0], currentCandidates[0]);
                continue;
            }

            foreach (var file in baselineCandidates)
            {
                yield return CreateMissing(file);
            }

            foreach (var file in currentCandidates)
            {
                yield return CreateNew(file);
            }

            if (baselineCandidates.Length > 0 && currentCandidates.Length > 0)
            {
                yield return CreateAmbiguous(hash, baselineCandidates.Length, currentCandidates.Length);
            }
        }
    }

    private static string? EffectivePath(FileChange change) =>
        change.CurrentRelativePath ?? change.BaselineRelativePath;

    private static FileChange CreateSamePathChange(FileFingerprint baseline, FileFingerprint current)
    {
        var unchanged = StringComparer.Ordinal.Equals(baseline.Sha256, current.Sha256);
        return new FileChange(
            unchanged ? FileChangeType.Unchanged : FileChangeType.Modified,
            baseline.RelativePath,
            current.RelativePath,
            current.Sha256,
            unchanged ? "File is unchanged." : "File content was modified.");
    }

    private static FileChange CreateMovedOrRenamed(FileFingerprint baseline, FileFingerprint current) =>
        new(FileChangeType.MovedOrRenamed, baseline.RelativePath, current.RelativePath, current.Sha256, "File was moved or renamed.");

    private static FileChange CreateAmbiguous(string hash, int baselineCount, int currentCount) =>
        new(
            FileChangeType.AmbiguousMovedOrRenamed,
            null,
            null,
            hash,
            $"Move/rename is ambiguous: {baselineCount} baseline candidates and {currentCount} current candidates share this hash.");

    private static FileChange CreateMissing(FileFingerprint baseline) =>
        new(FileChangeType.Missing, baseline.RelativePath, null, baseline.Sha256, "File is missing from the current snapshot.");

    private static FileChange CreateNew(FileFingerprint current) =>
        new(FileChangeType.New, null, current.RelativePath, current.Sha256, "File is new in the current snapshot.");
}
