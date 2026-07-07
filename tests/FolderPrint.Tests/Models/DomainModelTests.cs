using FolderPrint.Core.Models;

namespace FolderPrint.Tests.Models;

public sealed class DomainModelTests
{
    [Fact]
    public void FileFingerprint_WhenConstructed_ExposesRequiredFields()
    {
        var modified = new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
        var fingerprint = new FileFingerprint("docs/example.txt", "abc123", 1234, modified);

        Assert.Equal("docs/example.txt", fingerprint.RelativePath);
        Assert.Equal("abc123", fingerprint.Sha256);
        Assert.Equal(1234, fingerprint.Size);
        Assert.Equal(modified, fingerprint.LastModifiedUtc);
    }

    [Fact]
    public void RegisteredFolder_WhenConstructed_ExposesRequiredFields()
    {
        var created = new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
        var verified = created.AddHours(1);
        var files = new[] { new FileFingerprint("a.txt", "hash", 1, created) };

        var folder = new RegisteredFolder("folder-1", "C:\\Data", created, verified, files);

        Assert.Equal("folder-1", folder.Id);
        Assert.Equal("C:\\Data", folder.RootPath);
        Assert.Equal(created, folder.CreatedAtUtc);
        Assert.Equal(verified, folder.LastVerifiedAtUtc);
        Assert.Same(files, folder.Files);
    }

    [Fact]
    public void FolderSnapshot_WhenConstructed_ExposesFilesAndUnreadableFiles()
    {
        var scanned = new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
        var files = new[] { new FileFingerprint("a.txt", "hash", 1, scanned) };
        var unreadable = new[] { "locked.txt" };

        var snapshot = new FolderSnapshot("C:\\Data", scanned, files, unreadable);

        Assert.Equal("C:\\Data", snapshot.RootPath);
        Assert.Equal(scanned, snapshot.ScannedAtUtc);
        Assert.Same(files, snapshot.Files);
        Assert.Same(unreadable, snapshot.UnreadableFiles);
    }

    [Fact]
    public void FileChange_WhenConstructed_ExposesRequiredFields()
    {
        var change = new FileChange(
            FileChangeType.MovedOrRenamed,
            "old.txt",
            "new.txt",
            "hash",
            "File moved or renamed.");

        Assert.Equal(FileChangeType.MovedOrRenamed, change.Type);
        Assert.Equal("old.txt", change.BaselineRelativePath);
        Assert.Equal("new.txt", change.CurrentRelativePath);
        Assert.Equal("hash", change.Sha256);
        Assert.Equal("File moved or renamed.", change.Message);
    }

    [Fact]
    public void VerificationResult_WhenNoFindings_HasNoDifferences()
    {
        var verified = new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
        var result = new VerificationResult("C:\\Data", verified, [], [], []);

        Assert.Equal("C:\\Data", result.RootPath);
        Assert.Equal(verified, result.VerifiedAtUtc);
        Assert.Empty(result.Changes);
        Assert.Empty(result.DuplicateGroups);
        Assert.Empty(result.UnreadableFiles);
        Assert.False(result.HasDifferences);
    }

    [Fact]
    public void VerificationResult_WhenFindingsExist_HasDifferences()
    {
        var verified = new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
        var changes = new[] { new FileChange(FileChangeType.Modified, "a.txt", "a.txt", "hash", "Modified") };
        var result = new VerificationResult("C:\\Data", verified, changes, [], []);

        Assert.True(result.HasDifferences);
    }

    [Fact]
    public void FileChangeType_WhenInspected_ContainsV1Values()
    {
        var names = Enum.GetNames<FileChangeType>();

        Assert.Contains(nameof(FileChangeType.Unchanged), names);
        Assert.Contains(nameof(FileChangeType.Modified), names);
        Assert.Contains(nameof(FileChangeType.Missing), names);
        Assert.Contains(nameof(FileChangeType.New), names);
        Assert.Contains(nameof(FileChangeType.MovedOrRenamed), names);
        Assert.Contains(nameof(FileChangeType.Duplicate), names);
        Assert.Contains(nameof(FileChangeType.Unreadable), names);
    }
}
