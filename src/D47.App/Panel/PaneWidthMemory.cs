using D47.Core.Configuration;

namespace D47.App.Panel;

/// <summary>Remembers where the Commander dragged the rule between two panes (Phase 55).</summary>
public sealed class PaneWidthMemory(ViewStateStore store)
{
    private ViewState? _cached;

    /// <summary>
    /// Each pane's share of the strip at this pane count, or null for equal panes — which is both the
    /// untouched default and what a stored value that cannot be trusted falls back to.
    /// </summary>
    public IReadOnlyList<double>? Remembered(int panes)
    {
        _cached ??= store.Load();
        return _cached.SharesFor(panes);
    }

    /// <summary>Records a drag.</summary>
    public void Remember(int panes, IReadOnlyList<double> shares)
    {
        var next = store.Load().With(panes, shares);

        store.Save(next);
        _cached = next;
    }

    /// <summary>The Search form's remembered width in pixels, or null for the default.</summary>
    public double? SearchFormWidth
    {
        get
        {
            _cached ??= store.Load();
            return _cached.SearchFormWidth;
        }
    }

    /// <summary>Records where the Search form was left.</summary>
    public void RememberSearchForm(double width)
    {
        if (!double.IsFinite(width) || width <= 0)
        {
            return;
        }

        var next = store.Load() with { SearchFormWidth = width };

        store.Save(next);
        _cached = next;
    }
}
