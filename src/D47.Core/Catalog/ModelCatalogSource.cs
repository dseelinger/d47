namespace D47.Core.Catalog;

/// <summary>One model a provider listed for the stored key.</summary>
/// <param name="Created">When the provider released it, or null where its list does not say.</param>
public sealed record ListedModel(string Id, string? Name = null, DateTimeOffset? Created = null)
{
    /// <summary>Whether the provider says the model takes adaptive thinking, or null where it does not say.</summary>
    public bool? AdaptiveThinking { get; init; }
}

/// <summary>
/// Holds the model catalog in use, which may be replaced while the app runs, and the models each
/// provider listed for the stored key.
/// </summary>
public sealed class ModelCatalogSource(ModelCatalog initial)
{
    private volatile ModelCatalog _current = initial ?? throw new ArgumentNullException(nameof(initial));

    private volatile Dictionary<string, IReadOnlyList<ListedModel>> _listed = new(StringComparer.Ordinal);

    private volatile Dictionary<string, IReadOnlyList<ListedModel>> _listedSpeech = new(StringComparer.Ordinal);

    private readonly Lock _listing = new();

    /// <summary>The app's source, starting from the embedded catalog.</summary>
    public static ModelCatalogSource Shared { get; } = new(ModelCatalog.Embedded);

    public ModelCatalog Current => _current;

    /// <summary>Raised after <see cref="Replace"/>, on the thread that called it.</summary>
    public event Action<ModelCatalog>? Replaced;

    public void Replace(ModelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _current = catalog;
        Replaced?.Invoke(catalog);
    }

    /// <summary>Records what a language model provider listed; an empty list forgets it.</summary>
    public void List(string providerId, IReadOnlyList<ListedModel> models)
    {
        lock (_listing)
        {
            _listed = With(_listed, providerId, models);
        }
    }

    /// <summary>Records what a speech provider listed; an empty list forgets it.</summary>
    public void ListSpeech(string providerId, IReadOnlyList<ListedModel> models)
    {
        lock (_listing)
        {
            _listedSpeech = With(_listedSpeech, providerId, models);
        }
    }

    /// <summary>
    /// The listed models the catalog does not name and that were released after the newest listed model
    /// it does name, in the provider's order.
    /// </summary>
    public IReadOnlyList<ListedModel> NewFor(string providerId)
    {
        var catalog = _current;

        return New(_listed.GetValueOrDefault(providerId), id => catalog.Knows(providerId, id));
    }

    /// <summary>The listed speech models the catalog does not name, in the provider's order.</summary>
    public IReadOnlyList<ListedModel> NewSpeechFor(string providerId)
    {
        var catalog = _current;

        return New(_listedSpeech.GetValueOrDefault(providerId), id => catalog.SpeechModelFor(providerId, id) is not null);
    }

    /// <summary>
    /// The catalog's traits for a model it names; for a new listed model, the unknown-model traits with
    /// thinking as the provider listed it.
    /// </summary>
    public ModelTraits TraitsFor(string providerId, string model)
    {
        var catalog = _current;

        if (catalog.Knows(providerId, model))
        {
            return catalog.TraitsFor(providerId, model);
        }

        return NewFor(providerId).FirstOrDefault(listed => listed.Id == model) is { AdaptiveThinking: { } adaptive }
            ? ModelTraits.Unknown with { LegacyThinking = !adaptive }
            : ModelTraits.Unknown;
    }

    private static Dictionary<string, IReadOnlyList<ListedModel>> With(
        Dictionary<string, IReadOnlyList<ListedModel>> held,
        string providerId,
        IReadOnlyList<ListedModel> models)
    {
        ArgumentNullException.ThrowIfNull(models);

        var next = new Dictionary<string, IReadOnlyList<ListedModel>>(held, StringComparer.Ordinal);

        if (models.Count == 0)
        {
            next.Remove(providerId);
        }
        else
        {
            next[providerId] = [.. models];
        }

        return next;
    }

    private static IReadOnlyList<ListedModel> New(IReadOnlyList<ListedModel>? listed, Func<string, bool> known)
    {
        if (listed is null)
        {
            return [];
        }

        // A model released before one the catalog names was left out of the catalog on purpose.
        var newestKnown = listed.Where(model => known(model.Id)).Max(model => model.Created);

        return
        [
            .. listed.Where(model =>
                !known(model.Id)
                && (newestKnown is null || model.Created is null || model.Created > newestKnown)),
        ];
    }
}
