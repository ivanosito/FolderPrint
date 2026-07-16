using System.Globalization;
using FolderPrint.Core.Models;

namespace FolderPrint.Core.Reporting;

public static class ReportFormatter
{
    private static readonly FileChangeType[] ChangeTypeOrder =
    [
        FileChangeType.Unchanged,
        FileChangeType.Modified,
        FileChangeType.Missing,
        FileChangeType.New,
        FileChangeType.MovedOrRenamed,
        FileChangeType.AmbiguousMovedOrRenamed
    ];

    private static readonly IComparer<IReadOnlyList<string>> PathSequenceComparer =
        Comparer<IReadOnlyList<string>>.Create(ComparePathSequences);

    public static IReadOnlyList<string> FormatRegisteredFolders(IReadOnlyList<RegisteredFolder> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);

        var orderedFolders = folders
            .OrderBy(folder => folder.RootPath, StringComparer.Ordinal)
            .ThenBy(folder => folder.Id, StringComparer.Ordinal)
            .ToArray();
        var lines = new List<string> { "Registered folders:" };

        for (var index = 0; index < orderedFolders.Length; index++)
        {
            var folder = orderedFolders[index];
            if (index > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add($"Id: {folder.Id}");
            lines.Add($"Path: {folder.RootPath}");
            lines.Add($"Registered: {FormatTimestamp(folder.CreatedAtUtc)}");
            lines.Add($"Last verified: {(folder.LastVerifiedAtUtc is null ? "Never" : FormatTimestamp(folder.LastVerifiedAtUtc.Value))}");
            lines.Add($"Baseline files: {folder.Files.Count.ToString(CultureInfo.InvariantCulture)}");
        }

        return lines.ToArray();
    }

    public static IReadOnlyList<string> FormatDuplicates(
        string rootPath,
        IReadOnlyList<IReadOnlyList<string>> duplicateGroups)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(duplicateGroups);

        var lines = new List<string> { $"Duplicates: {rootPath}" };
        if (duplicateGroups.Count == 0)
        {
            lines.Add("No duplicates found.");
            return lines.ToArray();
        }

        lines.Add($"Duplicate groups: {duplicateGroups.Count.ToString(CultureInfo.InvariantCulture)}");
        lines.Add("[Duplicate Groups]");
        for (var index = 0; index < duplicateGroups.Count; index++)
        {
            lines.Add($"Group {(index + 1).ToString(CultureInfo.InvariantCulture)}:");
            lines.AddRange(duplicateGroups[index].Select(path => $"  {path}"));
        }

        return lines.ToArray();
    }

    public static IReadOnlyList<string> FormatVerification(VerificationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var changes = result.Changes.ToArray();
        var duplicateGroups = result.DuplicateGroups
            .Select(group => (IReadOnlyList<string>)group.OrderBy(path => path, StringComparer.Ordinal).ToArray())
            .OrderBy(group => group, PathSequenceComparer)
            .ToArray();
        var unreadableFiles = result.UnreadableFiles.OrderBy(path => path, StringComparer.Ordinal).ToArray();

        var lines = new List<string>
        {
            $"Verification: {result.RootPath}",
            result.HasDifferences ? "Result: Differences found" : "Result: Clean",
            "Summary:"
        };

        foreach (var type in ChangeTypeOrder)
        {
            lines.Add($"{SummaryLabel(type)}: {changes.Count(change => change.Type == type)}");
        }

        lines.Add($"Duplicate groups: {duplicateGroups.Length}");
        lines.Add($"Unreadable: {unreadableFiles.Length}");

        foreach (var type in ChangeTypeOrder)
        {
            var categoryChanges = changes.Where(change => change.Type == type)
                .OrderBy(EffectivePath, StringComparer.Ordinal)
                .ThenBy(change => change.Sha256, StringComparer.Ordinal)
                .ThenBy(change => change.BaselineRelativePath, StringComparer.Ordinal)
                .ThenBy(change => change.CurrentRelativePath, StringComparer.Ordinal)
                .ThenBy(change => change.Message, StringComparer.Ordinal)
                .ToArray();

            if (categoryChanges.Length == 0)
            {
                continue;
            }

            lines.Add($"[{SectionLabel(type)}]");
            lines.AddRange(categoryChanges.Select(FormatChange));
        }

        if (duplicateGroups.Length > 0)
        {
            lines.Add("[Duplicate Groups]");
            for (var index = 0; index < duplicateGroups.Length; index++)
            {
                lines.Add($"Group {index + 1}:");
                lines.AddRange(duplicateGroups[index].Select(path => $"  {path}"));
            }
        }

        if (unreadableFiles.Length > 0)
        {
            lines.Add("[Unreadable]");
            lines.AddRange(unreadableFiles);
        }

        return lines.ToArray();
    }

    private static string FormatChange(FileChange change) => change.Type switch
    {
        FileChangeType.MovedOrRenamed => $"{change.BaselineRelativePath} -> {change.CurrentRelativePath}",
        FileChangeType.AmbiguousMovedOrRenamed => $"{change.Sha256} | {change.Message}",
        FileChangeType.Missing => change.BaselineRelativePath ?? string.Empty,
        _ => change.CurrentRelativePath ?? change.BaselineRelativePath ?? string.Empty
    };

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static string? EffectivePath(FileChange change) =>
        change.CurrentRelativePath ?? change.BaselineRelativePath;

    private static string SummaryLabel(FileChangeType type) => type switch
    {
        FileChangeType.MovedOrRenamed => "Moved/Renamed",
        FileChangeType.AmbiguousMovedOrRenamed => "Ambiguous moved/renamed",
        _ => type.ToString()
    };

    private static string SectionLabel(FileChangeType type) => type switch
    {
        FileChangeType.MovedOrRenamed => "Moved/Renamed",
        FileChangeType.AmbiguousMovedOrRenamed => "Ambiguous Moved/Renamed",
        _ => type.ToString()
    };

    private static int ComparePathSequences(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        for (var index = 0; index < Math.Min(left.Count, right.Count); index++)
        {
            var comparison = StringComparer.Ordinal.Compare(left[index], right[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return left.Count.CompareTo(right.Count);
    }
}
