namespace D47.Core.Debrief;

/// <summary>The standing directions, read and written as one thing (#162).</summary>
/// <param name="store">The file.</param>
/// <param name="commander">
/// The Frontier id of whoever is aboard, or null before the journal has said.
/// </param>
public sealed class DebriefBook(StandingDirectionsStore store, Func<string?> commander)
{
    public StandingDirectionsStore Store => store;

    /// <summary>Everything filed for whoever is aboard right now.</summary>
    public IReadOnlyList<StandingDirection> Mine => store.For(commander());

    /// <summary>What the pass drafted and nobody has ruled on.</summary>
    public IReadOnlyList<StandingDirection> Waiting =>
        [.. Mine.Where(entry => entry.State == DirectionState.Proposed)];

    /// <summary>What the Commander took.</summary>
    public IReadOnlyList<StandingDirection> Adopted =>
        [.. Mine.Where(entry => entry.State == DirectionState.Adopted)];

    /// <summary>Runs the pass over a session and files what it drafted, returning it.</summary>
    public IReadOnlyList<StandingDirection> Propose(
        DebriefSession session,
        IReadOnlyList<DebriefSignal> signals,
        DateTimeOffset now,
        string? saidUnder = null,
        IReadOnlyCollection<string>? addressedAs = null,
        string? frontierId = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Named rather than asked for, when the caller knows.
        var who = frontierId ?? commander();
        var known = store.For(who);

        var drafted = DebriefExtractor.Extract(session.Lines, signals, known, now, saidUnder, addressedAs);

        foreach (var entry in drafted)
        {
            store.Write(who, entry);
        }

        return drafted;
    }

    /// <summary>Takes one proposal, in whatever words the Commander left in the editor.</summary>
    /// <param name="key">Which proposal.</param>
    /// <param name="text">The direction as it will enter the prompt.</param>
    /// <param name="persona">Which core it applies to, or null for all of them.</param>
    /// <returns>The entry as stored, or null where nothing carried that key.</returns>
    public StandingDirection? Adopt(string key, DateTimeOffset now, string? text = null, string? persona = null)
    {
        if (Find(key) is not { } entry)
        {
            return null;
        }

        // A question's own text is never adopted.
        var fallback = entry.Kind == DirectionKind.Question ? entry.Suggested : entry.Text;
        var wording = (string.IsNullOrWhiteSpace(text) ? fallback : text.Trim()) ?? string.Empty;

        if (wording.Length == 0)
        {
            return null;
        }

        if (wording.Length > StandingDirection.MaxText)
        {
            wording = wording[..StandingDirection.MaxText].TrimEnd() + "…";
        }

        return store.Write(commander(), entry with
        {
            Text = wording,

            // A question that has been answered with a direction is a direction.
            Kind = DirectionKind.Direction,
            State = DirectionState.Adopted,
            Persona = string.IsNullOrWhiteSpace(persona) ? null : persona.Trim(),
            AdoptedAt = now,
        });
    }

    /// <summary>Turns one down, and keeps the refusal.</summary>
    public bool Decline(string key)
    {
        if (Find(key) is not { } entry)
        {
            return false;
        }

        store.Write(commander(), entry with { State = DirectionState.Declined, AdoptedAt = null });
        return true;
    }

    /// <summary>Removes one outright, whatever state it was in.</summary>
    public bool Forget(string key) => store.Remove(commander(), key);

    /// <summary>The line the pane's header and the settings row both read.</summary>
    public string Summarise()
    {
        var mine = Mine;

        if (mine.Count == 0)
        {
            return "Nothing yet. After a session, D47 drafts directions from what you corrected it on.";
        }

        var adopted = mine.Count(entry => entry.State == DirectionState.Adopted);
        var waiting = mine.Count(entry => entry.State == DirectionState.Proposed);

        var said = adopted == 1 ? "1 direction you have taken" : $"{adopted} directions you have taken";

        return waiting == 0
            ? $"{said}, and nothing waiting."
            : $"{said}, and {waiting} waiting for you to look at.";
    }

    private StandingDirection? Find(string key) =>
        Mine.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.Ordinal));
}
