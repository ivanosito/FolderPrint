using FolderPrint.Core.Catalog;
using FolderPrint.Core.Models;

namespace FolderPrint.Tests.Catalog;

public sealed class IntegrityCatalogTests
{
    [Fact]
    public void AddRegisteredFolder_WithSnapshot_AppendsBaselineWithoutMutatingSourceCatalog()
    {
        var modified = new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
        var files = new[] { new FileFingerprint(Path.Combine("nested", "file.txt"), "abc123", 42, modified) };
        var snapshot = new FolderSnapshot("C:\\Data", modified.AddMinutes(1), files, ["locked.txt"]);
        var created = modified.AddMinutes(2);
        var original = IntegrityCatalog.Empty;

        var updated = original.AddRegisteredFolder("folder-1", snapshot, created);

        Assert.Empty(original.RegisteredFolders);
        var registered = Assert.Single(updated.RegisteredFolders);
        Assert.Equal("folder-1", registered.Id);
        Assert.Equal(snapshot.RootPath, registered.RootPath);
        Assert.Equal(created, registered.CreatedAtUtc);
        Assert.Null(registered.LastVerifiedAtUtc);
        Assert.NotSame(files, registered.Files);

        files[0] = new FileFingerprint("changed.txt", "changed", 1, modified);
        Assert.Equal("nested\\file.txt", registered.Files[0].RelativePath);
    }

    [Fact]
    public void AddRegisteredFolder_WithEmptySnapshot_PreservesEmptyFiles()
    {
        var timestamp = new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
        var snapshot = new FolderSnapshot("C:\\Empty", timestamp, [], []);

        var catalog = IntegrityCatalog.Empty.AddRegisteredFolder("empty", snapshot, timestamp);

        Assert.Empty(Assert.Single(catalog.RegisteredFolders).Files);
    }

    [Theory]
    [InlineData(0, "second", "third")]
    [InlineData(1, "first", "third")]
    [InlineData(2, "first", "second")]
    public void RemoveRegisteredFolderAt_FirstMiddleOrLast_PreservesSurvivorsAndSource(
        int index,
        string expectedFirst,
        string expectedSecond)
    {
        var folders = new[]
        {
            Folder("first", "A"),
            Folder("second", "B"),
            Folder("third", "C")
        };
        var source = new IntegrityCatalog(folders);

        var result = source.RemoveRegisteredFolderAt(index);

        Assert.Equal(
            ["first", "second", "third"],
            source.RegisteredFolders.Select(folder => folder.Id));
        Assert.NotSame(source.RegisteredFolders, result.RegisteredFolders);
        Assert.Equal(
            [expectedFirst, expectedSecond],
            result.RegisteredFolders.Select(folder => folder.Id));
        Assert.All(result.RegisteredFolders, survivor => Assert.Same(folders.Single(folder => folder.Id == survivor.Id), survivor));
    }

    [Fact]
    public void RemoveRegisteredFolderAt_OnlyEntry_ReturnsIndependentEmptyCatalog()
    {
        var folder = Folder("only", "Only");
        var source = new IntegrityCatalog([folder]);

        var result = source.RemoveRegisteredFolderAt(0);

        Assert.Same(folder, Assert.Single(source.RegisteredFolders));
        Assert.Empty(result.RegisteredFolders);
        Assert.NotSame(source.RegisteredFolders, result.RegisteredFolders);
    }

    [Fact]
    public void RemoveRegisteredFolderAt_DuplicateIds_RemovesOnlySelectedIndex()
    {
        var first = Folder("duplicate", "First");
        var second = Folder("duplicate", "Second");
        var source = new IntegrityCatalog([first, second]);

        var result = source.RemoveRegisteredFolderAt(1);

        Assert.Same(first, Assert.Single(result.RegisteredFolders));
        Assert.Equal([first, second], source.RegisteredFolders);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void RemoveRegisteredFolderAt_InvalidIndex_ThrowsWithoutMutation(int index)
    {
        var folder = Folder("only", "Only");
        var source = new IntegrityCatalog([folder]);

        Assert.Throws<ArgumentOutOfRangeException>(() => source.RemoveRegisteredFolderAt(index));
        Assert.Same(folder, Assert.Single(source.RegisteredFolders));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void WithRefreshedBaseline_FirstMiddleOrLast_ReplacesOnlySelectedIndex(int index)
    {
        var folders = new[]
        {
            Folder("first", "First"),
            Folder("second", "Second"),
            Folder("third", "Third")
        };
        var source = new IntegrityCatalog(folders);
        var scannedAt = new DateTimeOffset(2026, 7, 15, 13, 0, 0, TimeSpan.Zero);
        var files = new[]
        {
            new FileFingerprint("new.txt", new string('b', 64), 7, scannedAt)
        };
        var snapshot = new FolderSnapshot("ignored-snapshot-root", scannedAt, files, []);
        var refreshedAt = new DateTimeOffset(2026, 7, 15, 8, 30, 0, TimeSpan.FromHours(-5));

        var result = source.WithRefreshedBaseline(index, snapshot, refreshedAt);

        Assert.NotSame(source.RegisteredFolders, result.RegisteredFolders);
        Assert.Equal(folders, source.RegisteredFolders);
        for (var current = 0; current < folders.Length; current++)
        {
            if (current != index)
            {
                Assert.Same(folders[current], result.RegisteredFolders[current]);
            }
        }

        var refreshed = result.RegisteredFolders[index];
        Assert.NotSame(folders[index], refreshed);
        Assert.Equal(folders[index].Id, refreshed.Id);
        Assert.Equal(folders[index].RootPath, refreshed.RootPath);
        Assert.Equal(folders[index].CreatedAtUtc, refreshed.CreatedAtUtc);
        Assert.Equal(refreshedAt.ToUniversalTime(), refreshed.LastVerifiedAtUtc);
        Assert.Equal(files, refreshed.Files);
        Assert.NotSame(files, refreshed.Files);

        files[0] = new FileFingerprint("mutated.txt", new string('c', 64), 9, scannedAt);
        Assert.Equal("new.txt", refreshed.Files[0].RelativePath);
        Assert.NotEqual(refreshed.Files, folders[index].Files);
    }

    [Fact]
    public void WithRefreshedBaseline_EmptySnapshotAndDuplicateIds_UpdatesOnlySelectedIndex()
    {
        var first = Folder("duplicate", "First");
        var second = Folder("duplicate", "Second");
        var source = new IntegrityCatalog([first, second]);
        var refreshedAt = new DateTimeOffset(2026, 7, 15, 14, 0, 0, TimeSpan.Zero);
        var snapshot = new FolderSnapshot(second.RootPath, refreshedAt, [], []);

        var result = source.WithRefreshedBaseline(1, snapshot, refreshedAt);

        Assert.Same(first, result.RegisteredFolders[0]);
        Assert.Empty(result.RegisteredFolders[1].Files);
        Assert.Equal(second.Id, result.RegisteredFolders[1].Id);
        Assert.Single(second.Files);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void WithRefreshedBaseline_InvalidIndex_ThrowsWithoutMutation(int index)
    {
        var folder = Folder("only", "Only");
        var source = new IntegrityCatalog([folder]);
        var timestamp = new DateTimeOffset(2026, 7, 15, 14, 0, 0, TimeSpan.Zero);
        var snapshot = new FolderSnapshot(folder.RootPath, timestamp, [], []);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => source.WithRefreshedBaseline(index, snapshot, timestamp));
        Assert.Same(folder, Assert.Single(source.RegisteredFolders));
    }

    private static RegisteredFolder Folder(string id, string rootSuffix)
    {
        var timestamp = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        return new RegisteredFolder(
            id,
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), rootSuffix)),
            timestamp,
            timestamp.AddMinutes(1),
            [new FileFingerprint("file.txt", new string('a', 64), 42, timestamp)]);
    }
}
