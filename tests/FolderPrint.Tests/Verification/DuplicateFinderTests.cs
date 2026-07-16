using FolderPrint.Core.Models;
using FolderPrint.Core.Verification;

namespace FolderPrint.Tests.Verification;

public sealed class DuplicateFinderTests
{
    private static readonly DateTimeOffset ScannedAt = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Find_NullSnapshot_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new DuplicateFinder().Find(null!));

    [Fact]
    public void Find_EmptyOrSingletonHashes_ReturnsNoGroups()
    {
        var finder = new DuplicateFinder();

        Assert.Empty(finder.Find(Snapshot([])));
        Assert.Empty(finder.Find(Snapshot([
            Fingerprint("a.txt", "hash-a"),
            Fingerprint("b.txt", "hash-b")
        ])));
    }

    [Fact]
    public void Find_RepeatedHashes_ReturnsCompleteDeterministicallyOrderedGroups()
    {
        var files = new[]
        {
            Fingerprint("z.txt", "hash-1"),
            Fingerprint("c.txt", "hash-2"),
            Fingerprint("a.txt", "hash-1"),
            Fingerprint("b.txt", "hash-2"),
            Fingerprint("d.txt", "hash-2"),
            Fingerprint("single.txt", "hash-3")
        };

        var first = new DuplicateFinder().Find(Snapshot(files));
        var second = new DuplicateFinder().Find(Snapshot(files.Reverse().ToArray()));

        Assert.Collection(
            first,
            group => Assert.Equal(["a.txt", "z.txt"], group),
            group => Assert.Equal(["b.txt", "c.txt", "d.txt"], group));
        Assert.Equal(first.Select(GroupArray), second.Select(GroupArray));
    }

    [Fact]
    public void Find_OrdinalPathsAndCompleteSequences_ControlOrdering()
    {
        var groups = new DuplicateFinder().Find(Snapshot([
            Fingerprint("a.txt", "long"), Fingerprint("b.txt", "long"), Fingerprint("c.txt", "long"),
            Fingerprint("a.txt", "short"), Fingerprint("b.txt", "short"),
            Fingerprint("A.txt", "upper"), Fingerprint("z.txt", "upper")
        ]));

        Assert.Collection(
            groups,
            group => Assert.Equal(["A.txt", "z.txt"], group),
            group => Assert.Equal(["a.txt", "b.txt"], group),
            group => Assert.Equal(["a.txt", "b.txt", "c.txt"], group));
    }

    [Fact]
    public void Find_HashEqualityIsOrdinalAndMetadataDoesNotAffectMembership()
    {
        var groups = new DuplicateFinder().Find(Snapshot([
            Fingerprint("first.txt", "same", 1, ScannedAt),
            Fingerprint("second.txt", "same", 99, ScannedAt.AddDays(1)),
            Fingerprint("upper.txt", "HASH"),
            Fingerprint("lower.txt", "hash")
        ]));

        Assert.Equal(["first.txt", "second.txt"], Assert.Single(groups));
    }

    [Fact]
    public void Find_UnreadablesAreExcludedEvenWhenPathMatchesReadableFile()
    {
        var groups = new DuplicateFinder().Find(Snapshot(
            [Fingerprint("same.txt", "only-readable")],
            ["same.txt", "locked.txt"]));

        Assert.Empty(groups);
    }

    [Fact]
    public void Find_RepeatedFingerprintAtSamePath_QualifiesAndProjectsPathOnce()
    {
        var groups = new DuplicateFinder().Find(Snapshot([
            Fingerprint("same.txt", "shared"),
            Fingerprint("same.txt", "shared")
        ]));

        Assert.Equal(["same.txt"], Assert.Single(groups));
    }

    [Fact]
    public void Find_MaterializesResultsWithoutMutatingOrRetainingInputs()
    {
        var files = new List<FileFingerprint>
        {
            Fingerprint("z.txt", "shared"),
            Fingerprint("a.txt", "shared")
        };
        var unreadables = new List<string> { "locked.txt" };

        var groups = new DuplicateFinder().Find(Snapshot(files, unreadables));

        Assert.Equal(["z.txt", "a.txt"], files.Select(file => file.RelativePath));
        Assert.Equal(["locked.txt"], unreadables);
        files.Clear();
        unreadables.Clear();
        Assert.Equal(["a.txt", "z.txt"], Assert.Single(groups));
    }

    private static string[] GroupArray(IReadOnlyList<string> group) => group.ToArray();

    private static FolderSnapshot Snapshot(
        IReadOnlyList<FileFingerprint> files,
        IReadOnlyList<string>? unreadables = null) =>
        new("C:\\Data", ScannedAt, files, unreadables ?? []);

    private static FileFingerprint Fingerprint(
        string path,
        string hash,
        long size = 1,
        DateTimeOffset? modified = null) =>
        new(path, hash, size, modified ?? ScannedAt);
}
