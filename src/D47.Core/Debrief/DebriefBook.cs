namespace D47.Core.Debrief;

/// <summary>
/// The standing directions, read and written as one thing
/// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
/// <para>
/// Same arrangement as <see cref="Memory.MemoryBook"/> over <see cref="Memory.MemoryStore"/>: the
/// store knows about a file, and this knows who is flying and which act is being performed. And
/// for the same reason, the thing a caller cannot choose is decided here —
/// <see cref="StandingDirection.Tier"/> is computed from the state, and the only route that
/// produces <see cref="Memory.MemoryTier.Stated"/> is <see cref="Adopt"/>, which is the pane, which
/// is a person's hands.
/// </para>
/// <para>
/// <b>Nothing here reaches a prompt.</b> Adoption writes a file; what the model is shown was
/// latched at session start by <see cref="StandingDirectionsSession"/>. That separation is what
/// makes the cadence keepable rather than merely intended.
/// </para>
/// </summary>
/// <param name="store">The file.</param>
/// <param name="commander">
/// The Frontier id of whoever is aboard, or null before the journal has said. Read per call rather
/// than captured, because a Commander can log into a second character without restarting d47.
/// </param>
public sealed class DebriefBook(StandingDirectionsStore store, Func<string?> commander)
{
    public StandingDirectionsStore Store => store;

    /// <summary>Everything filed for whoever is aboard right now.</summary>
    public IReadOnlyList<StandingDirection> Mine => store.For(commander());

    /// <summary>What the pass drafted and nobody has ruled on. What the pane opens with.</summary>
    public IReadOnlyList<StandingDirection> Waiting =>
        [.. Mine.Where(entry => entry.State == DirectionState.Proposed)];

    /// <summary>What the Commander took. What the next session's prompt will carry.</summary>
    public IReadOnlyList<StandingDirection> Adopted =>
        [.. Mine.Where(entry => entry.State == DirectionState.Adopted)];

    /// <summary>
    /// Runs the pass over a session and files what it drafted, returning it.
    /// <para>
    /// <b>Everything it writes is <see cref="DirectionState.Proposed"/></b>, which is not a choice
    /// this method makes — <see cref="DebriefExtractor"/> has no other state to give it, and the
    /// tier that follows from it is computed. There is no argument, here or anywhere below, that
    /// could make the pass file something as the Commander's word.
    /// </para>
    /// </summary>
    public IReadOnlyList<StandingDirection> Propose(
        DebriefSession session,
        IReadOnlyList<DebriefSignal> signals,
        DateTimeOffset now,
        string? saidUnder = null,
        IReadOnlyCollection<string>? addressedAs = null,
        string? frontierId = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Named rather than asked for, when the caller knows. The one moment this matters is a
        // Commander logging out mid-run: by the time the switch is announced the game state is
        // already pointed at whoever logged in, and filing the previous Commander's corrections
        // under the new one would be worse than losing them.
        var who = frontierId ?? commander();
        var known = store.For(who);

        var drafted = DebriefExtractor.Extract(session.Lines, signals, known, now, saidUnder, addressedAs);

        foreach (var entry in drafted)
        {
            store.Write(who, entry);
        }

        return drafted;
    }

    /// <summary>
    /// Takes one proposal, in whatever words the Commander left in the editor. <b>The act that
    /// produces <see cref="Memory.MemoryTier.Stated"/></b>, and the only one.
    /// </summary>
    /// <param name="key">Which proposal.</param>
    /// <param name="text">
    /// The direction as it will enter the prompt. Defaults to what was proposed; the pane passes
    /// whatever is in the editor, because editing a draft before taking it is the ordinary case and
    /// a proposal nobody may touch is one nobody adopts.
    /// </param>
    /// <param name="persona">
    /// Which core it applies to, or null for all of them. A per-core direction is the style overlay
    /// — see <see cref="StandingDirection.Persona"/> — and never an edit to the pack.
    /// </param>
    /// <returns>The entry as stored, or null where nothing carried that key.</returns>
    public StandingDirection? Adopt(string key, DateTimeOffset now, string? text = null, string? persona = null)
    {
        if (Find(key) is not { } entry)
        {
            return null;
        }

        // A question's own text is never adopted. It is a question — adopting it would put
        // "shorter answers there?" into the prompt as an instruction — so what it falls back to is
        // the suggestion, and where there is none, nothing at all.
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

            // A question that has been answered with a direction is a direction. Keeping it a
            // question would leave the pane offering to answer something already answered.
            Kind = DirectionKind.Direction,
            State = DirectionState.Adopted,
            Persona = string.IsNullOrWhiteSpace(persona) ? null : persona.Trim(),
            AdoptedAt = now,
        });
    }

    /// <summary>
    /// Turns one down, and keeps the refusal. A tombstone rather than a delete, because the pass is
    /// deterministic and would otherwise redraft it from the same sentence tomorrow.
    /// </summary>
    public bool Decline(string key)
    {
        if (Find(key) is not { } entry)
        {
            return false;
        }

        store.Write(commander(), entry with { State = DirectionState.Declined, AdoptedAt = null });
        return true;
    }

    /// <summary>
    /// Removes one outright, whatever state it was in. What "I want this gone" means, including
    /// for a tombstone the Commander has changed their mind about.
    /// </summary>
    public bool Forget(string key) => store.Remove(commander(), key);

    /// <summary>
    /// The line the pane's header and the settings row both read. Here rather than in the App, so
    /// the two cannot describe the file differently.
    /// </summary>
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
