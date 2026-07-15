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
    public void Compare_SameHashAtDifferentPaths_ReturnsMovedOrRenamed()
    {
        var result = Compare(
            [Fingerprint("old-name.txt", "same-hash")],
            [Fingerprint("new-name.txt", "same-hash")]);

        var change = Assert.Single(result.Changes);
        Assert.Equal(FileChangeType.MovedOrRenamed, change.Type);
        Assert.Equal("old-name.txt", change.BaselineRelativePath);
        Assert.Equal("new-name.txt", change.CurrentRelativePath);
        Assert.Equal("same-hash", change.Sha256);
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

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    public void Compare_MultipleCandidates_ReturnsLosslessAmbiguity(int baselineCount, int currentCount)
    {
        var baseline = Enumerable.Range(1, baselineCount).Select(i => Fingerprint($"baseline-{i}.txt", "shared")).ToArray();
        var current = Enumerable.Range(1, currentCount).Select(i => Fingerprint($"current-{i}.txt", "shared")).ToArray();
        var result = Compare(baseline, current);
        Assert.Equal(baselineCount, result.Changes.Count(c => c.Type == FileChangeType.Missing));
        Assert.Equal(currentCount, result.Changes.Count(c => c.Type == FileChangeType.New));
        Assert.DoesNotContain(result.Changes, c => c.Type == FileChangeType.MovedOrRenamed);
        var ambiguity = Assert.Single(result.Changes, c => c.Type == FileChangeType.AmbiguousMovedOrRenamed);
        Assert.Null(ambiguity.BaselineRelativePath);
        Assert.Null(ambiguity.CurrentRelativePath);
        Assert.Equal("shared", ambiguity.Sha256);
        Assert.Equal($"Move/rename is ambiguous: {baselineCount} baseline candidates and {currentCount} current candidates share this hash.", ambiguity.Message);
    }

    [Fact]
    public void Compare_OneSidedRepeatedHash_ReturnsMissingWithoutAmbiguity()
    {
        var result = Compare([Fingerprint("b.txt", "shared"), Fingerprint("a.txt", "shared")], []);
        Assert.Equal(["a.txt", "b.txt"], result.Changes.Select(ChangePath));
        Assert.All(result.Changes, c => Assert.Equal(FileChangeType.Missing, c.Type));
        Assert.DoesNotContain(result.Changes, c => c.Type == FileChangeType.AmbiguousMovedOrRenamed);
    }

    [Fact]
    public void Compare_ShuffledMixedGroups_ReturnsDeterministicOrderingAndPreservesInputs()
    {
        var baseline = new List<FileFingerprint> { Fingerprint("z.txt", "old"), Fingerprint("old.txt", "move"), Fingerprint("b.txt", "amb"), Fingerprint("a.txt", "amb") };
        var current = new List<FileFingerprint> { Fingerprint("c.txt", "amb"), Fingerprint("new.txt", "move"), Fingerprint("y.txt", "new") };
        var first = Compare(baseline, current);
        var second = Compare(baseline.AsEnumerable().Reverse().ToArray(), current.AsEnumerable().Reverse().ToArray());
        Assert.Equal(first.Changes, second.Changes);
        Assert.Equal(["a.txt", "b.txt", "c.txt", "new.txt", "y.txt", "z.txt", null], first.Changes.Select(c => c.CurrentRelativePath ?? c.BaselineRelativePath));
        Assert.Equal(["z.txt", "old.txt", "b.txt", "a.txt"], baseline.Select(f => f.RelativePath));
        Assert.Equal(["c.txt", "new.txt", "y.txt"], current.Select(f => f.RelativePath));
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
