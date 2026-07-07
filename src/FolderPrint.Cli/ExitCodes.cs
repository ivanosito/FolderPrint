namespace FolderPrint.Cli;

public static class ExitCodes
{
    public const int Success = 0;
    public const int DifferencesFound = 1;
    public const int UsageError = 2;
    public const int NotFound = 3;
    public const int CatalogError = 4;
    public const int ScanError = 5;
    public const int UnexpectedError = 10;
}