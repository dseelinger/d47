using D47.Core.Configuration;

namespace D47.App.Panel;

/// <summary>Remembers which way the journal's Raw switch was left (#267).</summary>
public sealed class JournalReadingMemory(ViewStateStore store)
{
    private bool? _raw;

    /// <summary>Whether the journal reading should be drawn as the file's own JSON.</summary>
    public bool Raw => _raw ??= store.Load().JournalRaw;

    /// <summary>Records where the switch was left.</summary>
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
