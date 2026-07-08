namespace FolderPrint.Core.Catalog;

public sealed class CatalogLoadResult
{
    private CatalogLoadResult(IntegrityCatalog? catalog, string? errorMessage)
    {
        Catalog = catalog;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => Catalog is not null;

    public IntegrityCatalog? Catalog { get; }

    public string? ErrorMessage { get; }

    public static CatalogLoadResult Success(IntegrityCatalog catalog) => new(catalog, null);

    public static CatalogLoadResult CatalogError(string message) => new(null, message);
}
