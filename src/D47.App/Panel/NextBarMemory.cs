using D47.Core.Configuration;

namespace D47.App.Panel;

/// <summary>Remembers whether the NEXT bar shows the story objective or the campaign front. One instance serves every surface.</summary>
public sealed class NextBarMemory(ViewStateStore store)
{
    private NextBarSource? _source;

    /// <summary>Raised after <see cref="Remember"/> changes the source.</summary>
    public event Action? Changed;

    /// <summary>What the bar shows when both a story objective and a chosen front exist.</summary>
    public NextBarSource Source => _source ??= store.Load().NextBar;

    /// <summary>Records the choice and saves it.</summary>
    public void Remember(NextBarSource source)
    {
        if (Source == source)
        {
            return;
        }

        _source = source;
        store.Save(store.Load() with { NextBar = source });
        Changed?.Invoke();
    }
}
