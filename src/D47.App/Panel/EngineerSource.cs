using D47.Core.Engineers;

namespace D47.App.Panel;

/// <summary>
/// Where the Engineers tab gets its answer, and how it knows to ask again (Phase 28).
/// <para>
/// A source rather than a report, for the same reason the gap has one: the answer moves under the
/// page in four separate ways — a ship build, an on-foot build, where the Commander is, and what
/// they are flying — and none of them tells the page.
/// </para>
/// </summary>
public sealed class EngineerSource(Func<EngineerReport> report, Func<string?, string> promote)
{
    /// <summary>Raised when something underneath changed. The page redraws; nothing else cares.</summary>
    public event Action? Changed;

    /// <summary>Where everybody stands and which one to go and get next, computed fresh.</summary>
    public EngineerReport Read() => report();

    /// <summary>Offers one way in to the checklist, and says what happened.</summary>
    public string Promote(string? engineer) => promote(engineer);

    public void Invalidate() => Changed?.Invoke();
}
