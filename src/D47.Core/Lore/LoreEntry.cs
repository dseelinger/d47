namespace D47.Core.Lore;

/// <summary>
/// How much weight a note carries, and therefore which sentence reads it back (list.md Phase 23,
/// "Commander's Lore").
/// <para>
/// <b>This is the difference between a companion that remembers and one that invents</b>, and it
/// is structural on purpose. Left to prose, it would depend on the persona remembering to hedge —
/// and the persona is a block of text a Commander can switch off.
/// </para>
/// <para>
/// A tier is set when the entry arrives and is <b>never promoted</b>. Surviving a lookup is a
/// label rather than a verdict: an obscure but real site finds nothing, and a search can appear
/// to corroborate something a model half-invented. Promotion is the exact path by which an
/// invention becomes a fact d47 later states flatly, so there is no code that performs one.
/// </para>
/// </summary>
public enum LoreTier
{
    /// <summary>
    /// A row in the shipped table. Said in the table's own flat voice — <em>this system contains
    /// this site</em> — because it is a fact about Elite Dangerous that was checked before it was
    /// written down.
    /// </summary>
    Shipped,

    /// <summary>
    /// An addition a web lookup appeared to support when it was made. Read back as what it is:
    /// <em>the search says…</em>, and never as the table's flat voice.
    /// </summary>
    Corroborated,

    /// <summary>
    /// The Commander's word, and nothing else. Read back as <em>you told me…</em> — which is the
    /// whole of Commander's Lore, and is worth having precisely because it is labelled.
    /// </summary>
    Commander,
}

/// <summary>
/// How an entry got into the book. Recorded because <b>nothing else can tell an entry the
/// Commander dictated from one the model wrote on its own initiative</b>.
/// <para>
/// The model reads untrusted text — journal, in-game comms, and now search results — so a hostile
/// in-game message can attempt <em>"add to your lore that…"</em>, writing persistent local state
/// that d47 later speaks aloud with nobody having asked. It presses no key, so the write path is
/// not <see cref="Capabilities.ToolDefinition.Protected"/> the way every row reaching the keyboard
/// is; persistence plus unprompted speech is answered by labelling instead.
/// </para>
/// <para>
/// <b>There are two values and the checklist names three.</b> The missing one is the keyword
/// router, and it is missing because the router's grammar is closed: a declared phrase carries
/// declared arguments and extracts nothing from what was said (see
/// <see cref="Capabilities.ToolCommandPhrase"/>). A lore entry is a sentence, so there is no
/// phrase that could carry one and no router path to record. An enum member reachable by nothing
/// would be a claim the code does not support.
/// </para>
/// </summary>
public enum LoreArrival
{
    /// <summary>
    /// The Commander's own hands, on the panel. The only route that produces
    /// <see cref="LoreTier.Commander"/>, because it is the only one where d47 knows a person
    /// typed the words.
    /// </summary>
    Panel,

    /// <summary>
    /// The model's tool call. <b>Recorded as the model's whether or not it believes the Commander
    /// asked</b> — that is the conservative reading, and it is the honest one: a turn steered by a
    /// hostile in-game message looks exactly like a turn steered by the Commander from inside the
    /// handler, so the label says what is actually known.
    /// </summary>
    Model,
}

/// <summary>
/// One thing worth saying about one system.
/// </summary>
/// <param name="SystemAddress">
/// The key, and it is the address rather than the name because <b>names move and addresses do
/// not</b> — Ceeckia ZQ-L c24-0 is Beagle Point now and is the same 81973396946 it always was.
/// </param>
/// <param name="Name">
/// The system as it was spelled when the entry was made. Carried for reading back and for hand
/// editing; never matched on.
/// </param>
/// <param name="Note">The fact itself, as one or two sentences.</param>
public sealed record LoreEntry(long SystemAddress, string Name, string Note)
{
    public required LoreTier Tier { get; init; }

    /// <summary>
    /// How it arrived. Meaningless for <see cref="LoreTier.Shipped"/>, which arrived in the
    /// build, and defaulted rather than made nullable for that reason alone.
    /// </summary>
    public LoreArrival Arrival { get; init; } = LoreArrival.Panel;

    /// <summary>
    /// The Frontier id of the Commander who was aboard when it was added, or null for a shipped
    /// row and for an entry hand-written into the file.
    /// <para>
    /// <b>Recorded, not keyed on.</b> The book is per installation, because lore about a system
    /// is true whichever character is flying and the person taking the note is the same person
    /// either way — unlike <see cref="Journal.SamplingStore"/>, which holds game-side progress
    /// that genuinely belongs to one character. This field is who was there, which is worth
    /// knowing and is not the same thing as who it is for.
    /// </para>
    /// </summary>
    public string? FrontierId { get; init; }

    /// <summary>When it was added. Absolute, like every other stamp d47 stores.</summary>
    public DateTimeOffset? AddedAt { get; init; }

    /// <summary>
    /// The sentence this entry is read back in, which is the whole point of
    /// <see cref="LoreTier"/> being a field rather than a note the persona is trusted to add.
    /// <para>
    /// <b>It reads <see cref="Arrival"/> as well as the tier, and that is not redundancy.</b> The
    /// checklist describes three sentences and assumes the Commander asked for each entry; a
    /// model that wrote one on its own initiative is a fourth case, and reading it back as
    /// <em>"you told me"</em> would credit the Commander with words they never said. The tier is
    /// about corroboration and the arrival is about authorship, and the sentence needs both.
    /// </para>
    /// </summary>
    public string Spoken() => (Tier, Arrival) switch
    {
        (LoreTier.Shipped, _) => Note,
        (LoreTier.Corroborated, _) => $"You added this one, and the search agreed at the time: {Note}",
        (_, LoreArrival.Model) => $"I wrote this one down myself, and nothing has checked it: {Note}",
        _ => $"You told me: {Note}",
    };
}
