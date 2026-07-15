using FolderPrint.Core.Models;

namespace FolderPrint.Core.Catalog;

public sealed record IntegrityCatalog(IReadOnlyList<RegisteredFolder> RegisteredFolders)
{
    public static IntegrityCatalog Empty { get; } = new(Array.Empty<RegisteredFolder>());

    public IntegrityCatalog AddRegisteredFolder(
        string id,
        FolderSnapshot snapshot,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(snapshot);

        var registeredFolder = new RegisteredFolder(
            id,
            snapshot.RootPath,
            createdAtUtc,
            null,
            snapshot.Files.ToArray());

        return new IntegrityCatalog([.. RegisteredFolders, registeredFolder]);
    }

    public IntegrityCatalog WithLastVerifiedAt(int registeredFolderIndex, DateTimeOffset verifiedAtUtc)
    {
        if (registeredFolderIndex < 0 || registeredFolderIndex >= RegisteredFolders.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(registeredFolderIndex));
        }

        var folders = RegisteredFolders.ToArray();
        folders[registeredFolderIndex] = folders[registeredFolderIndex] with
        {
            LastVerifiedAtUtc = verifiedAtUtc.ToUniversalTime()
        };

        return new IntegrityCatalog(folders);
    }
}
