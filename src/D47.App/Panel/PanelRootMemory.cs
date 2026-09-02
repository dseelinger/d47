using D47.Core.Configuration;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>
/// Remembers which reading each tab was left on, across launches
/// (<a href="https://github.com/dseelinger/d47/issues/268">#268</a>).
/// <para>
/// The third of these, after <see cref="PaneWidthMemory"/> and <see cref="JournalReadingMemory"/>,
/// and here for the same reason both of those are: which mode a tab is on has no default worth
/// documenting, changes no behaviour, and being unable to read it should cost one press rather
/// than a loud failure — so it lives in <c>ViewState</c> and not in the append-only settings file.
/// </para>
/// <para>
/// <b>Read through once and written only on a change.</b> The panel offers every furnished tab's
/// root on every navigation, which is far too often for a file read and would be a file write per
/// press. The store is re-read before each write, like every other writer of this record, so
/// leaving a tab cannot clobber a pane drag or a collapse state written in between.
/// </para>
/// </summary>
public sealed class PanelRootMemory(ViewStateStore store)
{
    private Dictionary<string, string>? _roots;

    private Dictionary<string, string> Roots =>
        _roots ??= new Dictionary<string, string>(store.Load().PanelRoots, StringComparer.Ordinal);

    /// <summary>
    /// Which reading a tab was left on, or null where nothing was remembered for it — which is
    /// both a tab never visited and a file that could not be read.
    /// </summary>
    public string? Remembered(PanelTab tab) =>
        Roots.TryGetValue(tab.ToString(), out var root) ? root : null;

    /// <summary>Every remembered pairing, for a caller restoring them all at once.</summary>
    public IReadOnlyDictionary<string, string> All => Roots;

    /// <summary>Records where a tab was left. Cheap to call with the value it already holds.</summary>
    public void Remember(PanelTab tab, string root)
    {
        var name = tab.ToString();

        if (Roots.TryGetValue(name, out var known) && known == root)
        {
            return;
        }

        Roots[name] = root;

        store.Save(store.Load().With(name, root));
    }
}
