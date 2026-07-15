namespace FolderPrint.Core.Catalog;

public sealed record CatalogValidationResult(bool IsSuccess, string? ErrorMessage)
{
    public static CatalogValidationResult Success() => new(true, null);

    public static CatalogValidationResult CatalogError(string message) => new(false, message);
}
