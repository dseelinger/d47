using D47.Core.Configuration;

namespace D47.App.Panel;

/// <summary>
/// Remembers where the Commander dragged the rule between two panes (Phase 55).
/// <para>
/// The neighbour to <see cref="Windowing.WindowPlacementMemory"/> and deliberately so: both keep a
/// number nobody would ever type, on the one surface that has a mouse, out of the append-only
/// settings file. <c>ViewState</c> is where "how the panel was left" lives, and a split is exactly
/// that — it has no default worth documenting, changes no behaviour, and being unable to read it
/// should cost equal panes rather than a loud failure.
/// </para>
/// <para>
/// <b>Read through once and written through every time.</b> The load is cached because
/// <see cref="Remembered"/> is called on every redraw, which happens on every navigation; the save
/// re-reads first so a drag cannot clobber a collapse state written by another part of the panel
/// between one drag and the next.
/// </para>
/// </summary>
public sealed class PaneWidthMemory(ViewStateStore store)
{
    private ViewState? _cached;

    /// <summary>
    /// Each pane's share of the strip at this pane count, or null for equal panes — which is both
    /// the untouched default and what a stored value that cannot be trusted falls back to. The
    /// validation lives on <see cref="ViewState.SharesFor"/>, beside the record that has to survive
    /// a Commander editing the file.
    /// </summary>
    public IReadOnlyList<double>? Remembered(int panes)
    {
        _cached ??= store.Load();
        return _cached.SharesFor(panes);
    }

    /// <summary>
    /// Records a drag. Shares are normalised by the caller and are expected to sum to 1.
    /// <para>
    /// Re-read rather than written from the cache, because this record is shared: the collapse
    /// states, the checklist filter and the window rectangle all live on it and all write to it.
    /// Saving a stale copy here would silently undo whichever of them moved most recently.
    /// </para>
    /// </summary>
    public void Remember(int panes, IReadOnlyList<double> shares)
    {
        var next = store.Load().With(panes, shares);

        store.Save(next);
        _cached = next;
    }
}
