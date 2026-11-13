using D47.Core.Configuration;

namespace D47.App.Panel;

/// <summary>Remembers where the Commander dragged the rule between two panes (Phase 55).</summary>
/// <param name="headset">Whether this is the headset panel's memory, which is stored apart from the desktop window's.</param>
public sealed class PaneWidthMemory(ViewStateStore store, bool headset = false)
{
    private ViewState? _cached;

    /// <summary>Whether this is the headset panel's memory, whose rules are dragged by the ray.</summary>
    public bool Headset => headset;

    /// <summary>
    /// Each pane's share of the strip at this pane count, or null for equal panes — which is both the
    /// untouched default and what a stored value that cannot be trusted falls back to.
    /// </summary>
    public IReadOnlyList<double>? Remembered(int panes)
    {
        _cached ??= store.Load();
        return _cached.SharesFor(panes, headset);
    }

    /// <summary>Records a drag.</summary>
    public void Remember(int panes, IReadOnlyList<double> shares)
    {
        var next = store.Load().With(panes, shares, headset);

        store.Save(next);
        _cached = next;
    }

    /// <summary>Whether the leftmost pane of a drill strip is dragged out of sight.</summary>
    public bool LeftmostHidden
    {
        get
        {
            _cached ??= store.Load();
            return headset ? _cached.HeadsetLeftmostPaneHidden : _cached.LeftmostPaneHidden;
        }
    }

    /// <summary>Records that the leftmost pane was hidden or shown.</summary>
    public void RememberLeftmostHidden(bool hidden)
    {
        var current = store.Load();
        var next = headset ? current with { HeadsetLeftmostPaneHidden = hidden } : current with { LeftmostPaneHidden = hidden };

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
