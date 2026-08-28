namespace D47.Core.Memory;

/// <summary>
/// How a remembered fact arrived at being believed, and therefore which sentence reads it back
/// (Phase 31, "A memory is written down, never inferred into being").
/// <para>
/// <b>Phase 23's tiered truth, extended by one tier.</b> <see cref="Lore.LoreTier"/> exists for
/// notes about places and this exists for facts about the Commander, and the reason both are a
/// field rather than a note the persona is trusted to add is the same: left to prose, hedging
/// would depend on a block of text the Commander can switch off.
/// </para>
/// <para>
/// A tier is set when the entry arrives and is <b>never promoted</b>, exactly as a lore tier is
/// not. An inference that a later turn appeared to confirm is still an inference — appearing to be
/// confirmed is the road by which something d47 made up becomes something d47 states flatly, and
/// there is no code here that walks it.
/// </para>
/// <para>
/// <b>The tier is a property of the write path and never a parameter.</b> A model that could
/// declare its own entry <see cref="Stated"/> would be one a hostile in-game message could tell to
/// (architecture.md §7), so the routes decide: the panel produces <see cref="Stated"/>, the journal
/// observer produces <see cref="Observed"/>, and the tool produces <see cref="Inferred"/>. A
/// Commander who wants their own word on the record types it where a person's hands are.
/// </para>
/// </summary>
public enum MemoryTier
{
    /// <summary>
    /// The Commander said so, on the panel, in their own words. The only tier a person authored,
    /// and the only one whose expiry is worth announcing (Phase 31, item 3).
    /// </summary>
    Stated,

    /// <summary>
    /// d47 read it out of the journal. Read back as an observation — <em>I noticed…</em> — because
    /// that is what it is: true about what the game reported, and not something the Commander
    /// chose to tell anybody.
    /// </summary>
    Observed,

    /// <summary>
    /// d47 wrote it down on its own initiative, out of a conversation. <b>Never spoken as fact</b>,
    /// which is item 1's requirement stated in the checklist's own emphasis, and is what
    /// <see cref="MemoryEntry.Spoken"/> enforces for as long as the entry exists.
    /// </summary>
    Inferred,
}

/// <summary>
/// How an entry got into the store. Recorded because <b>nothing else can tell a memory the
/// Commander dictated from one a hostile in-game message planted</b>.
/// <para>
/// <b>There are three values and the checklist names a different three.</b> It says "panel, keyword
/// router, or the model's own tool call". The router is not here, and the reason is already written
/// down in <see cref="Lore.LoreArrival"/>: the router's grammar is closed — a declared phrase
/// carries declared arguments and extracts nothing from what was said (see
/// <see cref="Capabilities.ToolCommandPhrase"/>) — and a memory is a sentence, so there is no
/// phrase that could carry one. Phase 23 met this and shipped two values with the absence
/// documented. Phase 31 ships three, because it has a third real route that checklist line did not
/// anticipate: the journal.
/// </para>
/// </summary>
public enum MemoryArrival
{
    /// <summary>
    /// The Commander's own hands, on the panel. The only route that produces
    /// <see cref="MemoryTier.Stated"/>, because it is the only one where d47 knows a person typed
    /// the words.
    /// </summary>
    Panel,

    /// <summary>
    /// The model's tool call. <b>Recorded as the model's whether or not it believes the Commander
    /// asked</b> — the conservative reading and the honest one, since a turn steered by a hostile
    /// in-game message looks exactly like a turn steered by the Commander from inside the handler.
    /// </summary>
    Model,

    /// <summary>
    /// d47's own reading of the journal. No model was involved and no person typed it, which is
    /// worth distinguishing from both: an observation cannot have been planted by anything the
    /// model read, and it is also not something the Commander chose to say.
    /// </summary>
    Journal,
}

/// <summary>
/// One thing d47 remembers about the Commander.
/// <para>
/// <b>A fact or an observation, and never a transcript.</b> Phase 31 gives all three reasons
/// in one line — a rolling transcript is a privacy liability, a context-window problem and a
/// confabulation engine — so there is no field here that could hold one and no writer that offers.
/// </para>
/// </summary>
/// <param name="Key">
/// Identity, and what a rewrite replaces. Authored and model-written entries get a deterministic
/// lowest-unused key like an authored checklist line, because Core owns no randomness and reads no
/// clock; the observer uses a fixed key per subject, which is what makes an observation update in
/// place rather than accumulate a row per session.
/// </param>
/// <param name="Fact">The thing itself, as one sentence. Read out, so it is written to be read out.</param>
public sealed record MemoryEntry(string Key, string Fact)
{
    public required MemoryTier Tier { get; init; }

    /// <summary>How it arrived. Defaulted rather than made nullable, like <see cref="Lore.LoreEntry.Arrival"/>.</summary>
    public MemoryArrival Arrival { get; init; } = MemoryArrival.Panel;

    /// <summary>
    /// What was going on when it was written down — the system, the hull, the activity. <b>Taken
    /// from the situation rather than from the writer</b>, which is what makes it free and what
    /// keeps it out of the model's hands: a tag is a fact about when d47 learned something, and a
    /// tag the model chose would be a tag a hostile message could aim at
    /// <see cref="MemoryRecall"/>.
    /// </summary>
    public IReadOnlyList<string> About { get; init; } = [];

    /// <summary>
    /// When it was written down. Absolute, like every other stamp d47 stores, and the only thing
    /// expiry measures against.
    /// <para>
    /// For an observation it is the instant the <em>journal</em> reported, not the instant the file
    /// was written — which is what makes "you were last aboard three days ago" answerable at all.
    /// </para>
    /// </summary>
    public DateTimeOffset? AddedAt { get; init; }

    /// <summary>
    /// The sentence this entry is read back in. The one place a tier and an arrival turn into
    /// words, for the reason <see cref="Lore.LoreEntry.Spoken"/> is the only place the other pair
    /// does: hedging that lives in prose lives at the mercy of whoever writes the next prompt.
    /// <para>
    /// It reads both fields, and that is not redundancy. The tier says how good the fact is and the
    /// arrival says who wrote it, and an entry hand-edited to <see cref="MemoryTier.Stated"/> with
    /// <see cref="MemoryArrival.Model"/> still on it is a real state of the file — read back as the
    /// Commander's word, because they edited the file to say so, with nothing invented about where
    /// it came from.
    /// </para>
    /// </summary>
    public string Spoken() => Tier switch
    {
        MemoryTier.Stated => $"You told me: {Fact}",
        MemoryTier.Observed => $"I noticed: {Fact}",
        _ => $"I wrote this one down myself, and nothing has checked it: {Fact}",
    };
}

/// <summary>One entry that could not be read back, and why. Kept rather than dropped.</summary>
public sealed record MemoryProblem(string What, string Why);
