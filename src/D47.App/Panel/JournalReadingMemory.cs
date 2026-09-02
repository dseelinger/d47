using D47.Core.Configuration;

namespace D47.App.Panel;

/// <summary>
/// Remembers which way the journal's Raw switch was left
/// (<a href="https://github.com/dseelinger/d47/issues/267">#267</a>).
/// <para>
/// The neighbour to <see cref="PaneWidthMemory"/> and built the same way: a small class over
/// <c>ViewStateStore</c>, because "how the panel was left" is not a setting — it has no default
/// worth documenting, changes no behaviour, and being unable to read it should cost sentences
/// rather than a loud failure.
/// </para>
/// <para>
/// <b>Read through once and written only on a change.</b> The panel asks this on every navigation,
/// which is far too often for a file read; and a write per navigation would be a file write per
/// press. So the answer is cached after the first read and the store is touched only when the
/// switch actually moves — re-reading first, like every other writer of this record, so a toggle
/// cannot clobber a pane drag or a collapse state written between one flick and the next.
/// </para>
/// </summary>
public sealed class JournalReadingMemory(ViewStateStore store)
{
    private bool? _raw;

    /// <summary>Whether the journal reading should be drawn as the file's own JSON.</summary>
    public bool Raw => _raw ??= store.Load().JournalRaw;

    /// <summary>Records where the switch was left. Cheap to call with the value it already holds.</summary>
    public void Remember(bool raw)
    {
        if (Raw == raw)
        {
            return;
        }

        _raw = raw;

        store.Save(store.Load() with { JournalRaw = raw });
    }
}
