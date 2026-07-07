namespace FolderPrint.Core.Models;

public enum FileChangeType
{
    Unchanged,
    Modified,
    Missing,
    New,
    MovedOrRenamed,
    Duplicate,
    Unreadable
}
