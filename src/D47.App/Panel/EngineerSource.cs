using D47.Core.Engineers;

namespace D47.App.Panel;

/// <summary>Where the Engineers tab gets its answer, and how it knows to ask again (Phase 28).</summary>
public sealed class EngineerSource(Func<EngineerReport> report, Func<string?, string> promote)
{
    /// <summary>Raised when something underneath changed.</summary>
    public event Action? Changed;

    /// <summary>Where everybody stands and which one to go and get next, computed fresh.</summary>
    public EngineerReport Read() => report();

    /// <summary>Offers one way in to the checklist, and says what happened.</summary>
    public string Promote(string? engineer) => promote(engineer);

    public void Invalidate() => Changed?.Invoke();
}
