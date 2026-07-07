using System.Security.Cryptography;

namespace FolderPrint.Core.Scanning;

public sealed class FileHasher
{
    public string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
