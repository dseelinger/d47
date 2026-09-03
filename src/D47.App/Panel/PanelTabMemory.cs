using D47.Core.Configuration;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>
/// Remembers which tab a surface was left on, across launches
/// (<a href="https://github.com/dseelinger/d47/issues/276">#276</a>).
/// <para>
/// The neighbour to <see cref="PanelRootMemory"/> and built the same way: read through once and
/// written only on a change, so a launch that opens on the tab the Commander left rather than
/// always Transcript costs no more than the file read already paid for the roots.
/// </para>
/// <para>
/// <b>One instance per surface.</b> <paramref name="vr"/> chooses <c>ViewState.LastTabVr</c> over
/// <c>ViewState.LastTab</c>, the same split <see cref="PanelRootMemory"/> makes for roots: the
/// window can be on Settings while the headset reads the conversation, and a shared key would have
/// whichever surface moved last decide where both reopen.
/// </para>
/// </summary>
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

    /// <summary>Records the tab this surface is on. Cheap to call with the value it already holds.</summary>
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
