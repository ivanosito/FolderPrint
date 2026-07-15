namespace FolderPrint.Core.Catalog;

public sealed class CatalogLoadResult
{
    private CatalogLoadResult(IntegrityCatalog? catalog, string? version, string? errorMessage)
    {
        Catalog = catalog;
        Version = version;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => Catalog is not null;

    public IntegrityCatalog? Catalog { get; }

    public string? Version { get; }

    public string? ErrorMessage { get; }

    public static CatalogLoadResult Success(IntegrityCatalog catalog, string? version = null) => new(catalog, version, null);

    public static CatalogLoadResult CatalogError(string message) => new(null, null, message);
}
