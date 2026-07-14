using FolderPrint.Core.Models;
using FolderPrint.Core.Verification;

namespace FolderPrint.Tests.Verification;

public sealed class VerificationServiceTests
{
    private static readonly DateTimeOffset BaselineTime = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ScanTime = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Compare_SamePathAndHash_ReturnsUnchanged()
    {
        var result = Compare(
            [Fingerprint("same.txt", "aaa", 10, BaselineTime)],
            [Fingerprint("same.txt", "aaa", 20, ScanTime)]);

        var change = Assert.Single(result.Changes);
        Assert.Equal(FileChangeType.Unchanged, change.Type);
        Assert.Equal("same.txt", change.BaselineRelativePath);
        Assert.Equal("same.txt", change.CurrentRelativePath);
        Assert.Equal("aaa", change.Sha256);
        Assert.False(result.HasDifferences);
    }

    [Theory]
    [InlineData(20, 0)]
    [InlineData(10, 1)]
    [InlineData(20, 1)]
    public void Compare_SamePathAndHashWithDifferentMetadata_RemainsUnchanged(long currentSize, int additionalDays)
    {
        var result = Compare(
            [Fingerprint("same.txt", "aaa", 10, BaselineTime)],
            [Fingerprint("same.txt", "aaa", currentSize, BaselineTime.AddDays(additionalDays))]);

        Assert.Equal(FileChangeType.Unchanged, Assert.Single(result.Changes).Type);
        Assert.False(result.HasDifferences);
    }

    [Fact]
    public void Compare_SamePathAndDifferentHash_ReturnsModified()
    {
        var result = Compare(
            [Fingerprint("changed.txt", "old")],
            [Fingerprint("changed.txt", "new")]);

        var change = Assert.Single(result.Changes);
        Assert.Equal(FileChangeType.Modified, change.Type);
        Assert.Equal("changed.txt", change.BaselineRelativePath);
        Assert.Equal("changed.txt", change.CurrentRelativePath);
        Assert.Equal("new", change.Sha256);
        Assert.True(result.HasDifferences);
    }

    [Fact]
    public void Compare_BaselineOnlyPath_ReturnsMissing()
    {
        var result = Compare([Fingerprint("missing.txt", "old")], []);

        var change = Assert.Single(result.Changes);
        Assert.Equal(FileChangeType.Missing, change.Type);
        Assert.Equal("missing.txt", change.BaselineRelativePath);
        Assert.Null(change.CurrentRelativePath);
        Assert.Equal("old", change.Sha256);
        Assert.True(result.HasDifferences);
    }

    [Fact]
    public void Compare_CurrentOnlyPath_ReturnsNew()
    {
        var result = Compare([], [Fingerprint("new.txt", "new")]);

        var change = Assert.Single(result.Changes);
        Assert.Equal(FileChangeType.New, change.Type);
        Assert.Null(change.BaselineRelativePath);
        Assert.Equal("new.txt", change.CurrentRelativePath);
        Assert.Equal("new", change.Sha256);
        Assert.True(result.HasDifferences);
    }

    [Fact]
    public void Compare_SameHashAtDifferentPaths_ReturnsMissingAndNewOnly()
    {
        var result = Compare(
            [Fingerprint("old-name.txt", "same-hash")],
            [Fingerprint("new-name.txt", "same-hash")]);

        Assert.Equal([FileChangeType.New, FileChangeType.Missing], result.Changes.Select(change => change.Type));
        Assert.DoesNotContain(result.Changes, change => change.Type == FileChangeType.MovedOrRenamed);
        Assert.Empty(result.DuplicateGroups);
    }

    [Fact]
    public void Compare_MixedUnorderedInputs_ReturnsCompleteOrdinalOrdering()
    {
        var baselineFiles = new List<FileFingerprint>
        {
            Fingerprint("z-missing.txt", "missing"),
            Fingerprint("m-modified.txt", "old"),
            Fingerprint("a-unchanged.txt", "same")
        };
        var currentFiles = new List<FileFingerprint>
        {
            Fingerprint("n-new.txt", "new"),
            Fingerprint("a-unchanged.txt", "same"),
            Fingerprint("m-modified.txt", "current")
        };

        var result = Compare(baselineFiles, currentFiles);

        Assert.Equal(
            ["a-unchanged.txt", "m-modified.txt", "n-new.txt", "z-missing.txt"],
            result.Changes.Select(ChangePath));
        Assert.Equal(
            [FileChangeType.Unchanged, FileChangeType.Modified, FileChangeType.New, FileChangeType.Missing],
            result.Changes.Select(change => change.Type));
        Assert.Equal(["z-missing.txt", "m-modified.txt", "a-unchanged.txt"], baselineFiles.Select(file => file.RelativePath));
        Assert.Equal(["n-new.txt", "a-unchanged.txt", "m-modified.txt"], currentFiles.Select(file => file.RelativePath));
    }

    [Fact]
    public void Compare_EmptyInputs_ReturnsCleanResultWithSnapshotMetadata()
    {
        var baseline = Baseline([]);
        var snapshot = Snapshot([]);

        var result = new VerificationService().Compare(baseline, snapshot);

        Assert.Equal(baseline.RootPath, result.RootPath);
        Assert.Equal(snapshot.ScannedAtUtc, result.VerifiedAtUtc);
        Assert.Empty(result.Changes);
        Assert.Empty(result.DuplicateGroups);
        Assert.Empty(result.UnreadableFiles);
        Assert.False(result.HasDifferences);
    }

    [Fact]
    public void Compare_UnreadablePathsAndRepeatedHashes_DoesNotProduceLaterStoryFindings()
    {
        var baseline = Baseline([Fingerprint("a.txt", "shared")]);
        var snapshot = Snapshot(
            [Fingerprint("a.txt", "shared"), Fingerprint("b.txt", "shared")],
            ["locked.txt"]);

        var result = new VerificationService().Compare(baseline, snapshot);

        Assert.Equal([FileChangeType.Unchanged, FileChangeType.New], result.Changes.Select(change => change.Type));
        Assert.Empty(result.DuplicateGroups);
        Assert.Empty(result.UnreadableFiles);
        Assert.DoesNotContain(result.Changes, change => change.Type is FileChangeType.Duplicate or FileChangeType.Unreadable);
    }

    private static VerificationResult Compare(
        IReadOnlyList<FileFingerprint> baselineFiles,
        IReadOnlyList<FileFingerprint> currentFiles) =>
        new VerificationService().Compare(Baseline(baselineFiles), Snapshot(currentFiles));

    private static RegisteredFolder Baseline(IReadOnlyList<FileFingerprint> files) =>
        new("folder-1", "C:\\Data", BaselineTime, null, files);

    private static FolderSnapshot Snapshot(
        IReadOnlyList<FileFingerprint> files,
        IReadOnlyList<string>? unreadableFiles = null) =>
        new("C:\\Data", ScanTime, files, unreadableFiles ?? []);

    private static FileFingerprint Fingerprint(
        string path,
        string hash,
        long size = 1,
        DateTimeOffset? modified = null) =>
        new(path, hash, size, modified ?? BaselineTime);

    private static string ChangePath(FileChange change) =>
        change.CurrentRelativePath ?? change.BaselineRelativePath!;
}