using D47.Core.Configuration;

namespace D47.App.Panel;

/// <summary>Remembers whether the Ships index was left showing hull artwork.</summary>
public sealed class ShipsDrawingsMemory(ViewStateStore store)
{
    private bool? _drawings;

    /// <summary>Whether the fleet cards should carry their hull artwork.</summary>
    public bool Drawings => _drawings ??= !store.Load().ShipsDrawingsOff;

    /// <summary>Records where the switch was left.</summary>
    public void Remember(bool drawings)
    {
        if (Drawings == drawings)
        {
            return;
        }

        _drawings = drawings;

        store.Save(store.Load() with { ShipsDrawingsOff = !drawings });
    }
}
