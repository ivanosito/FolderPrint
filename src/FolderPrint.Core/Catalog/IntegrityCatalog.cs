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

    public IntegrityCatalog WithRefreshedBaseline(
        int registeredFolderIndex,
        FolderSnapshot snapshot,
        DateTimeOffset refreshedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (registeredFolderIndex < 0 || registeredFolderIndex >= RegisteredFolders.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(registeredFolderIndex));
        }

        var folders = RegisteredFolders.ToArray();
        folders[registeredFolderIndex] = folders[registeredFolderIndex] with
        {
            LastVerifiedAtUtc = refreshedAtUtc.ToUniversalTime(),
            Files = snapshot.Files.ToArray()
        };

        return new IntegrityCatalog(folders);
    }

    public IntegrityCatalog RemoveRegisteredFolderAt(int registeredFolderIndex)
    {
        if (registeredFolderIndex < 0 || registeredFolderIndex >= RegisteredFolders.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(registeredFolderIndex));
        }

        var folders = new RegisteredFolder[RegisteredFolders.Count - 1];
        for (int sourceIndex = 0, destinationIndex = 0;
             sourceIndex < RegisteredFolders.Count;
             sourceIndex++)
        {
            if (sourceIndex == registeredFolderIndex)
            {
                continue;
            }

            folders[destinationIndex++] = RegisteredFolders[sourceIndex];
        }

        return new IntegrityCatalog(folders);
    }
}
