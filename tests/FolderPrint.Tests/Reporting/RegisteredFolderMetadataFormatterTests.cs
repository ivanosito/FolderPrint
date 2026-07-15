using FolderPrint.Core.Models;
using FolderPrint.Core.Reporting;

namespace FolderPrint.Tests.Reporting;

public sealed class RegisteredFolderMetadataFormatterTests
{
    private static readonly DateTimeOffset Created = new(2026, 7, 15, 8, 30, 0, TimeSpan.FromHours(-5));
    private static readonly DateTimeOffset Verified = new(2026, 7, 15, 15, 45, 0, TimeSpan.FromHours(2));
    private const string ValidSha256 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void FormatRegisteredFolders_AllMetadata_RendersExactInvariantUtcBlock()
    {
        var folder = Folder("folder-1", "stored/root", Verified,
        [
            Fingerprint("a.txt"),
            Fingerprint("b.txt")
        ]);

        var lines = ReportFormatter.FormatRegisteredFolders([folder]);

        Assert.Equal(
        [
            "Registered folders:",
            "Id: folder-1",
            "Path: stored/root",
            "Registered: 2026-07-15T13:30:00.0000000+00:00",
            "Last verified: 2026-07-15T13:45:00.0000000+00:00",
            "Baseline files: 2"
        ], lines);
    }

    [Fact]
    public void FormatRegisteredFolders_EmptyBaselineAndNoVerification_RendersNeverAndZero()
    {
        var lines = ReportFormatter.FormatRegisteredFolders([Folder("empty", "missing/root", null, [])]);

        Assert.Equal("Last verified: Never", lines[4]);
        Assert.Equal("Baseline files: 0", lines[5]);
    }

    [Fact]
    public void FormatRegisteredFolders_ShuffledInput_UsesRootThenIdOrdinalOrderingWithoutMutation()
    {
        var first = Folder("z-id", "same-root", null, []);
        var second = Folder("a-id", "same-root", null, []);
        var third = Folder("third", "Z-root", null, []);
        RegisteredFolder[] shuffled = [first, third, second];
        var originalOrder = shuffled.ToArray();

        var shuffledLines = ReportFormatter.FormatRegisteredFolders(shuffled);
        var reorderedLines = ReportFormatter.FormatRegisteredFolders([third, second, first]);

        Assert.Equal(shuffledLines, reorderedLines);
        Assert.Equal(originalOrder, shuffled);
        Assert.True(IndexOf(shuffledLines, "Id: third") < IndexOf(shuffledLines, "Id: a-id"));
        Assert.True(IndexOf(shuffledLines, "Id: a-id") < IndexOf(shuffledLines, "Id: z-id"));
        Assert.Equal(2, shuffledLines.Count(line => line.Length == 0));
    }

    private static RegisteredFolder Folder(
        string id,
        string rootPath,
        DateTimeOffset? verified,
        IReadOnlyList<FileFingerprint> files) =>
        new(id, rootPath, Created, verified, files);

    private static FileFingerprint Fingerprint(string path) =>
        new(path, ValidSha256, 1, Created);

    private static int IndexOf(IReadOnlyList<string> lines, string value) =>
        Enumerable.Range(0, lines.Count).Single(index => lines[index] == value);
}
