using D47.Core.Configuration;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Remembers which tab a surface was left on, across launches (#276).</summary>
public sealed class PanelTabMemory(ViewStateStore store)
{
    private string? _tab;
    private bool _loaded;

    /// <summary>Which tab this surface was left on, or null for one never visited.</summary>
    public PanelTab? Remembered()
    {
        if (!_loaded)
        {
            _tab = store.Load().LastTab;
            _loaded = true;
        }

        return PanelTabNames.Parse(_tab);
    }

    /// <summary>Records the tab this surface is on.</summary>
    public void Remember(PanelTab tab)
    {
        var name = tab.ToString();

        if (Remembered()?.ToString() == name)
        {
            return;
        }

        _tab = name;
        _loaded = true;

        store.Save(store.Load() with { LastTab = name });
    }
}
