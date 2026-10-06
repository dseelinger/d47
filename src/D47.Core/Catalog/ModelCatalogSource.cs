namespace D47.Core.Catalog;

/// <summary>Holds the model catalog in use, which may be replaced while the app runs.</summary>
public sealed class ModelCatalogSource(ModelCatalog initial)
{
    private volatile ModelCatalog _current = initial ?? throw new ArgumentNullException(nameof(initial));

    /// <summary>The app's source, starting from the embedded catalog.</summary>
    public static ModelCatalogSource Shared { get; } = new(ModelCatalog.Embedded);

    public ModelCatalog Current => _current;

    public void Replace(ModelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _current = catalog;
    }
}
