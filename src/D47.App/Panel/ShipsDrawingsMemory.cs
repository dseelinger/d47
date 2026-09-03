using D47.Core.Configuration;

namespace D47.App.Panel;

/// <summary>
/// Remembers whether the Ships index was left showing hull artwork.
/// <para>
/// The neighbour to <see cref="JournalReadingMemory"/> and built the same way, for the same
/// reasons: a small class over <c>ViewStateStore</c>, cached after the first read because the page
/// asks on every navigation, and written only when the switch actually moves — re-reading first so
/// a flick cannot clobber a pane drag written between one press and the next.
/// </para>
/// <para>
/// <b>The stored fact is the negative.</b> Drawings are what the page is for, so the default has to
/// survive an absent or unreadable file — and a bool shrugs to false. What is kept is therefore the
/// Commander who turned them off. See <see cref="ViewState.ShipsDrawingsOff"/>.
/// </para>
/// </summary>
public sealed class ShipsDrawingsMemory(ViewStateStore store)
{
    private bool? _drawings;

    /// <summary>Whether the fleet cards should carry their hull artwork.</summary>
    public bool Drawings => _drawings ??= !store.Load().ShipsDrawingsOff;

    /// <summary>Records where the switch was left. Cheap to call with the value it already holds.</summary>
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
