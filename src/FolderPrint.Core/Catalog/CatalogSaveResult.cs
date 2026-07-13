namespace FolderPrint.Core.Catalog;

public sealed class CatalogSaveResult
{
    private CatalogSaveResult(bool isSuccess, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public string? ErrorMessage { get; }

    public static CatalogSaveResult Success() => new(true, null);

    public static CatalogSaveResult CatalogError(string message) => new(false, message);
}
