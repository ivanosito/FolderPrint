using FolderPrint.Core.Scanning;

namespace FolderPrint.Tests.Scanning;

public sealed class FolderScannerTests
{
    [Fact]
    public void Scan_RootAndNestedFiles_ReturnsFingerprintsWithMetadata()
    {
        var root = CreateTempDirectory();
        try
        {
            var rootFile = Path.Combine(root, "root.txt");
            var nestedDirectory = Directory.CreateDirectory(Path.Combine(root, "nested")).FullName;
            var nestedFile = Path.Combine(nestedDirectory, "child.txt");
            File.WriteAllText(rootFile, "abc");
            File.WriteAllText(nestedFile, "nested content");
            var expectedModified = new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(rootFile, expectedModified);
            File.SetLastWriteTimeUtc(nestedFile, expectedModified);

            var scanStartedAt = DateTimeOffset.UtcNow;
            var snapshot = new FolderScanner(new FileHasher()).Scan(root);
            var scanFinishedAt = DateTimeOffset.UtcNow;

            Assert.Equal(root, snapshot.RootPath);
            Assert.Equal(TimeSpan.Zero, snapshot.ScannedAtUtc.Offset);
            Assert.InRange(snapshot.ScannedAtUtc, scanStartedAt, scanFinishedAt);
            Assert.Empty(snapshot.UnreadableFiles);
            Assert.Equal(2, snapshot.Files.Count);

            var rootFingerprint = Assert.Single(snapshot.Files, file => file.RelativePath == "root.txt");
            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", rootFingerprint.Sha256);
            Assert.Equal(3, rootFingerprint.Size);
            AssertTimestampClose(expectedModified, rootFingerprint.LastModifiedUtc.UtcDateTime);

            var expectedNestedPath = Path.Combine("nested", "child.txt");
            var nestedFingerprint = Assert.Single(snapshot.Files, file => file.RelativePath == expectedNestedPath);
            Assert.Equal("b509163964e822915ea7e822759ecae39dd696626e70b74b96de6ac7396415d0", nestedFingerprint.Sha256);
            Assert.Equal(14, nestedFingerprint.Size);
            AssertTimestampClose(expectedModified, nestedFingerprint.LastModifiedUtc.UtcDateTime);
            Assert.All(snapshot.Files, file => Assert.False(Path.IsPathRooted(file.RelativePath)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_EmptyFolder_ReturnsEmptySnapshot()
    {
        var root = CreateTempDirectory();
        try
        {
            var snapshot = new FolderScanner(new FileHasher()).Scan(root);
            Assert.Empty(snapshot.Files);
            Assert.Empty(snapshot.UnreadableFiles);
        }
        finally
        {
            Directory.Delete(root);
        }
    }

    [Fact]
    public void Scan_MissingFolder_ThrowsDirectoryNotFoundException()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"folderprint-missing-{Guid.NewGuid():N}");
        Assert.Throws<DirectoryNotFoundException>(() => new FolderScanner(new FileHasher()).Scan(missing));
    }

    [Fact]
    public void Scan_FilePath_ThrowsIOException()
    {
        var root = CreateTempDirectory();
        try
        {
            var file = Path.Combine(root, "file.txt");
            File.WriteAllText(file, "content");
            Assert.Throws<IOException>(() => new FolderScanner(new FileHasher()).Scan(file));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_LockedFile_ReportsUnreadableAndContinues()
    {
        var root = CreateTempDirectory();
        try
        {
            var lockedPath = Path.Combine(root, "locked.txt");
            File.WriteAllText(lockedPath, "locked");
            File.WriteAllText(Path.Combine(root, "readable.txt"), "readable");

            using var lockStream = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var snapshot = new FolderScanner(new FileHasher()).Scan(root);

            Assert.Contains("locked.txt", snapshot.UnreadableFiles);
            Assert.DoesNotContain(snapshot.Files, file => file.RelativePath == "locked.txt");
            Assert.Contains(snapshot.Files, file => file.RelativePath == "readable.txt");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"folderprint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void AssertTimestampClose(DateTime expected, DateTime actual)
    {
        Assert.InRange((actual - expected).Duration(), TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }
}
