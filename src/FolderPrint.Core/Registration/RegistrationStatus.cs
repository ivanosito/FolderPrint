namespace FolderPrint.Core.Registration;

public enum RegistrationStatus
{
    Success,
    InvalidRoot,
    AlreadyRegistered,
    CatalogInsideRoot,
    CatalogError,
    ScanError
}
