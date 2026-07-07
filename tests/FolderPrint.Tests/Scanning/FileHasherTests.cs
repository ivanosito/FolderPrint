using FolderPrint.Core.Scanning;

namespace FolderPrint.Tests.Scanning;

public sealed class FileHasherTests
{
    [Fact]
    public void ComputeSha256_FileContainsKnownValue_ReturnsLowercaseSha256()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"folderprint-{Guid.NewGuid():N}.txt");
        File.WriteAllText(filePath, "abc");

        try
        {
            var hasher = new FileHasher();
            var hash = hasher.ComputeSha256(filePath);

            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
            Assert.Equal(hash.ToLowerInvariant(), hash);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
