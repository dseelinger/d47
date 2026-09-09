using D47.Core.Configuration;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Remembers which reading each tab was left on, across launches (#268).</summary>
public sealed class PanelRootMemory(ViewStateStore store, bool vr = false)
{
    private Dictionary<string, string>? _roots;

    private Dictionary<string, string> Roots =>
        _roots ??= new Dictionary<string, string>(Source(store.Load()), StringComparer.Ordinal);

    private IReadOnlyDictionary<string, string> Source(ViewState state) =>
        vr ? state.PanelRootsVr : state.PanelRoots;

    /// <summary>
    /// Which reading a tab was left on, or null where nothing was remembered for it — which is both a
    /// tab never visited and a file that could not be read.
    /// </summary>
    public string? Remembered(PanelTab tab) =>
        Roots.TryGetValue(tab.ToString(), out var root) ? root : null;

    /// <summary>Every remembered pairing, for a caller restoring them all at once.</summary>
    public IReadOnlyDictionary<string, string> All => Roots;

    /// <summary>Records where a tab was left.</summary>
    public void Remember(PanelTab tab, string root)
    {
        var name = tab.ToString();

        if (Roots.TryGetValue(name, out var known) && known == root)
        {
            return;
        }

        Roots[name] = root;

        store.Save(vr ? store.Load().WithVr(name, root) : store.Load().With(name, root));
    }
}
