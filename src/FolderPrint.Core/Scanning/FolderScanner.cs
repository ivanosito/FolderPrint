using FolderPrint.Core.Models;

namespace FolderPrint.Core.Scanning;

public sealed class FolderScanner
{
    private readonly FileHasher _fileHasher;

    public FolderScanner(FileHasher fileHasher)
    {
        _fileHasher = fileHasher ?? throw new ArgumentNullException(nameof(fileHasher));
    }

    public FolderSnapshot Scan(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        if (File.Exists(rootPath))
        {
            throw new IOException($"The scan root is not a directory: {rootPath}");
        }

        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"The scan root does not exist: {rootPath}");
        }

        var scannedAtUtc = DateTimeOffset.UtcNow;
        var files = new List<FileFingerprint>();
        var unreadableFiles = new List<string>();

        foreach (var filePath in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(rootPath, filePath);

            try
            {
                var sha256 = _fileHasher.ComputeSha256(filePath);
                var fileInfo = new FileInfo(filePath);
                files.Add(new FileFingerprint(
                    relativePath,
                    sha256,
                    fileInfo.Length,
                    new DateTimeOffset(fileInfo.LastWriteTimeUtc)));
            }
            catch (IOException)
            {
                unreadableFiles.Add(relativePath);
            }
            catch (UnauthorizedAccessException)
            {
                unreadableFiles.Add(relativePath);
            }
        }

        files.Sort((left, right) => StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath));
        unreadableFiles.Sort(StringComparer.Ordinal);

        return new FolderSnapshot(rootPath, scannedAtUtc, files, unreadableFiles);
    }
}
