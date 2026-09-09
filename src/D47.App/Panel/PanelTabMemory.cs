using D47.Core.Configuration;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Remembers which tab a surface was left on, across launches (#276).</summary>
public sealed class PanelTabMemory(ViewStateStore store, bool vr = false)
{
    private string? _tab;
    private bool _loaded;

    /// <summary>Which tab this surface was left on, or null for one never visited.</summary>
    public PanelTab? Remembered()
    {
        if (!_loaded)
        {
            _tab = vr ? store.Load().LastTabVr : store.Load().LastTab;
            _loaded = true;
        }

        return _tab is { } name && Enum.TryParse<PanelTab>(name, out var tab) ? tab : null;
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

        store.Save(vr ? store.Load() with { LastTabVr = name } : store.Load() with { LastTab = name });
    }
}
