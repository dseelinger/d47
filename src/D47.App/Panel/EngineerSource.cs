using D47.Core.Engineers;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Where the Engineers tab gets its answer, and how it knows to ask again (Phase 28).</summary>
public sealed class EngineerSource(
    Func<EngineerReport> report,
    Func<int, bool>? isPinned = null,
    Action<int, bool>? pin = null,
    Func<Engineer, string>? addPrerequisites = null)
{
    /// <summary>Raised when something underneath changed.</summary>
    public event Action? Changed;

    /// <summary>Where everybody stands and which one to go and get next, computed fresh.</summary>
    public EngineerReport Read() => report();

    /// <summary>Whether the Commander has told d47 a blueprint is pinned with this engineer (#113).</summary>
    public bool IsPinned(int engineerId) => isPinned?.Invoke(engineerId) ?? false;

    /// <summary>Records or clears a pin, where this surface offers one (#113).</summary>
    public void Pin(int engineerId, bool pinned) => pin?.Invoke(engineerId, pinned);

    /// <summary>Adds one engineer's unmet unlock prerequisites to the checklist (#257).</summary>
    public string AddPrerequisites(Engineer engineer) =>
        addPrerequisites?.Invoke(engineer) ?? "There is nowhere to add this to.";

    public void Invalidate() => Changed?.Invoke();
}
