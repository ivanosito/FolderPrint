using FolderPrint.Core.Models;

namespace FolderPrint.Core.Catalog;

public sealed record IntegrityCatalog(IReadOnlyList<RegisteredFolder> RegisteredFolders)
{
    public static IntegrityCatalog Empty { get; } = new(Array.Empty<RegisteredFolder>());
}
