using FolderPrint.Core.Models;
using FolderPrint.Core.Reporting;

namespace FolderPrint.Tests.Reporting;

public sealed class ReportFormatterTests
{
    private static readonly DateTimeOffset Verified = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FormatVerification_CleanResult_ReturnsStableSummary()
    {
        var result = Result([
            Change(FileChangeType.Unchanged, "same.txt", "same.txt", "same")
        ]);

        var lines = ReportFormatter.FormatVerification(result);

        Assert.Equal("Verification: C:\\Data", lines[0]);
        Assert.Equal("Result: Clean", lines[1]);
        Assert.Contains("Unchanged: 1", lines);
        Assert.Contains("Modified: 0", lines);
        Assert.Contains("Duplicate groups: 0", lines);
        Assert.Contains("Unreadable: 0", lines);
        Assert.Contains("[Unchanged]", lines);
        Assert.Contains("same.txt", lines);
    }

    [Fact]
    public void FormatVerification_ShuffledMixedResult_UsesFixedCategoryAndOrdinalOrdering()
    {
        var result = Result(
            [
                Change(FileChangeType.AmbiguousMovedOrRenamed, null, null, "amb", "Ambiguous."),
                Change(FileChangeType.New, null, "z-new.txt", "new"),
                Change(FileChangeType.MovedOrRenamed, "old.txt", "moved.txt", "move"),
                Change(FileChangeType.Modified, "z-mod.txt", "z-mod.txt", "modified"),
                Change(FileChangeType.Missing, "a-missing.txt", null, "missing"),
                Change(FileChangeType.Unchanged, "b.txt", "b.txt", "same"),
                Change(FileChangeType.Modified, "a-mod.txt", "a-mod.txt", "modified-2")
            ],
            [["z-duplicate.txt", "a-duplicate.txt"], ["only-path.txt"]],
            ["z.locked", "a.locked", "a.locked"]);

        var lines = ReportFormatter.FormatVerification(result);

        Assert.Equal("Result: Differences found", lines[1]);
        Assert.True(IndexOf(lines, "[Unchanged]") < IndexOf(lines, "[Modified]"));
        Assert.True(IndexOf(lines, "[Modified]") < IndexOf(lines, "[Missing]"));
        Assert.True(IndexOf(lines, "[Missing]") < IndexOf(lines, "[New]"));
        Assert.True(IndexOf(lines, "[New]") < IndexOf(lines, "[Moved/Renamed]"));
        Assert.True(IndexOf(lines, "[Moved/Renamed]") < IndexOf(lines, "[Ambiguous Moved/Renamed]"));
        Assert.True(IndexOf(lines, "[Ambiguous Moved/Renamed]") < IndexOf(lines, "[Duplicate Groups]"));
        Assert.True(IndexOf(lines, "[Duplicate Groups]") < IndexOf(lines, "[Unreadable]"));
        Assert.True(IndexOf(lines, "a-mod.txt") < IndexOf(lines, "z-mod.txt"));
        Assert.Contains("old.txt -> moved.txt", lines);
        Assert.Contains("amb | Ambiguous.", lines);
        Assert.Equal(2, lines.Count(line => line == "a.locked"));
        Assert.Contains("Duplicate groups: 2", lines);
    }

    [Fact]
    public void FormatVerification_EqualHashPathlessAmbiguities_UsesMessageAsFinalTieBreaker()
    {
        var first = Result(
        [
            Change(FileChangeType.AmbiguousMovedOrRenamed, null, null, "same-hash", "z-message"),
            Change(FileChangeType.AmbiguousMovedOrRenamed, null, null, "same-hash", "a-message")
        ]);
        var reversed = Result(first.Changes.Reverse().ToArray());

        var firstLines = ReportFormatter.FormatVerification(first);
        var reversedLines = ReportFormatter.FormatVerification(reversed);

        Assert.Equal(firstLines, reversedLines);
        Assert.True(IndexOf(firstLines, "same-hash | a-message") < IndexOf(firstLines, "same-hash | z-message"));
    }

    [Fact]
    public void FormatVerification_DuplicateGroups_PreservesNestedBoundariesAndSinglePathGroup()
    {
        var result = Result([], [["z.txt", "a.txt"], ["same.txt"]], []);

        var lines = ReportFormatter.FormatVerification(result);

        Assert.Contains("Group 1:", lines);
        Assert.Contains("Group 2:", lines);
        Assert.True(IndexOf(lines, "  a.txt") < IndexOf(lines, "  z.txt"));
        Assert.Contains("  same.txt", lines);
    }

    [Fact]
    public void FormatDuplicates_NoGroups_ReturnsMaterializedExplicitMessage()
    {
        var groups = new List<IReadOnlyList<string>>();

        var lines = ReportFormatter.FormatDuplicates("C:\\Data", groups);
        groups.Add(["later-a.txt", "later-b.txt"]);

        Assert.Equal(["Duplicates: C:\\Data", "No duplicates found."], lines);
    }

    [Fact]
    public void FormatDuplicates_Groups_PreservesSuppliedOrderAndMaterializesLines()
    {
        var first = new List<string> { "z.txt", "a.txt" };
        var second = new List<string> { "nested/c.txt", "nested/a.txt", "nested/b.txt" };
        var groups = new List<IReadOnlyList<string>> { first, second };

        var lines = ReportFormatter.FormatDuplicates("C:\\Data", groups);
        first.Clear();
        second.Reverse();
        groups.Clear();

        Assert.Equal(
            [
                "Duplicates: C:\\Data",
                "Duplicate groups: 2",
                "[Duplicate Groups]",
                "Group 1:",
                "  z.txt",
                "  a.txt",
                "Group 2:",
                "  nested/c.txt",
                "  nested/a.txt",
                "  nested/b.txt"
            ],
            lines);
    }

    private static VerificationResult Result(
        IReadOnlyList<FileChange> changes,
        IReadOnlyList<IReadOnlyList<string>>? duplicates = null,
        IReadOnlyList<string>? unreadable = null) =>
        new("C:\\Data", Verified, changes, duplicates ?? [], unreadable ?? []);

    private static FileChange Change(
        FileChangeType type,
        string? baseline,
        string? current,
        string? hash,
        string? message = null) =>
        new(type, baseline, current, hash, message ?? type.ToString());

    private static int IndexOf(IReadOnlyList<string> lines, string value) =>
        Enumerable.Range(0, lines.Count).Single(index => lines[index] == value);
}
