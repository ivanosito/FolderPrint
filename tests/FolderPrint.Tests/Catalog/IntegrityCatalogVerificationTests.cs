using FolderPrint.Core.Catalog;
using FolderPrint.Core.Models;

namespace FolderPrint.Tests.Catalog;

public sealed class IntegrityCatalogVerificationTests
{
    [Fact]
    public void WithLastVerifiedAt_ValidIndex_ReplacesOnlyTimestampAndPreservesOrderAndBaseline()
    {
        var created = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);
        var verified = new DateTimeOffset(2026, 7, 15, 11, 0, 0, TimeSpan.Zero);
        var files = new[] { new FileFingerprint("file.txt", "hash", 4, created) };
        var first = new RegisteredFolder("first", "C:\\First", created, null, files);
        var second = new RegisteredFolder("second", "C:\\Second", created.AddMinutes(1), null, []);
        var original = new IntegrityCatalog([first, second]);

        var updated = original.WithLastVerifiedAt(0, verified);

        Assert.Null(original.RegisteredFolders[0].LastVerifiedAtUtc);
        Assert.Equal(["first", "second"], updated.RegisteredFolders.Select(folder => folder.Id));
        Assert.Equal(verified, updated.RegisteredFolders[0].LastVerifiedAtUtc);
        Assert.Equal(first.Id, updated.RegisteredFolders[0].Id);
        Assert.Equal(first.RootPath, updated.RegisteredFolders[0].RootPath);
        Assert.Equal(first.CreatedAtUtc, updated.RegisteredFolders[0].CreatedAtUtc);
        Assert.Same(files, updated.RegisteredFolders[0].Files);
        Assert.Same(second, updated.RegisteredFolders[1]);
    }
}
