using D47.Core.Engineers;

namespace D47.App.Panel;

/// <summary>Where the Engineers tab gets its answer, and how it knows to ask again (Phase 28).</summary>
public sealed class EngineerSource(
    Func<EngineerReport> report,
    Func<string?, string> promote,
    Func<int, bool>? isPinned = null,
    Action<int, bool>? pin = null)
{
    /// <summary>Raised when something underneath changed.</summary>
    public event Action? Changed;

    /// <summary>Where everybody stands and which one to go and get next, computed fresh.</summary>
    public EngineerReport Read() => report();

    /// <summary>Offers one way in to the checklist, and says what happened.</summary>
    public string Promote(string? engineer) => promote(engineer);

    /// <summary>Whether the Commander has told d47 a blueprint is pinned with this engineer (#113).</summary>
    public bool IsPinned(int engineerId) => isPinned?.Invoke(engineerId) ?? false;

    /// <summary>Records or clears a pin, where this surface offers one (#113).</summary>
    public void Pin(int engineerId, bool pinned) => pin?.Invoke(engineerId, pinned);

    public void Invalidate() => Changed?.Invoke();
}
