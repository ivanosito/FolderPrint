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
}
