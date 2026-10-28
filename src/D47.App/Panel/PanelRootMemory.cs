using D47.Core.Configuration;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Remembers which reading each tab was left on, across launches (#268).</summary>
public sealed class PanelRootMemory(ViewStateStore store)
{
    private Dictionary<string, string>? _roots;

    private Dictionary<string, string> Roots =>
        _roots ??= new Dictionary<string, string>(store.Load().PanelRoots, StringComparer.Ordinal);

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

        store.Save(store.Load().With(name, root));
    }
}
