using D47.Core.Memory;

namespace D47.Core.Debrief;

/// <summary>
/// What the debrief drafted, and what became of it
/// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
/// <para>
/// <b>Three states and one of them is a tombstone.</b> <see cref="Declined"/> entries stay in the
/// file rather than being deleted, because the pass is deterministic: a direction the Commander
/// turned down would be drafted again from the same sentence on the next session, and a review pane
/// that keeps offering something already refused is one nobody opens twice.
/// </para>
/// </summary>
public enum DirectionState
{
    /// <summary>
    /// Drafted by the pass and waiting on a person. In the file, in the review pane, and
    /// <b>nowhere near the prompt</b>.
    /// </summary>
    Proposed,

    /// <summary>The Commander took it. The only state that reaches a prompt.</summary>
    Adopted,

    /// <summary>The Commander turned it down. Kept so it is not offered again.</summary>
    Declined,
}

/// <summary>
/// Whether the pass is proposing a direction or asking a question.
/// </summary>
public enum DirectionKind
{
    /// <summary>
    /// Drawn from something the Commander actually said. The text is their sentence, tidied and
    /// not reinterpreted.
    /// </summary>
    Direction,

    /// <summary>
    /// Drawn from a pattern nobody put into words — see <see cref="DebriefSignalKind"/>. It reaches
    /// the Commander as a question and can only become a direction by their writing one.
    /// </summary>
    Question,
}

/// <summary>
/// One proposed or adopted standing direction
/// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
/// <para>
/// <b>The tier is computed and has no setter, which is the merge gate in one line.</b> Phase 31
/// established that <see cref="MemoryTier"/> is a property of the write path and never a parameter,
/// so that no call — and therefore nothing a hostile in-game message could steer — can declare its
/// own entry the Commander's word. The same rule holds here and is stronger for being arithmetic:
/// adoption is the act that produces <see cref="MemoryTier.Stated"/>, so there is no route that
/// produces one without it, not even a wrong one.
/// </para>
/// </summary>
/// <param name="Key">Identity, and what an edit replaces. Deterministic and lowest-unused, like every other store's.</param>
/// <param name="Text">
/// <b>The exact text that would enter the prompt</b>, rendered verbatim by
/// <see cref="StandingDirections.Render"/>. Not a summary of it, and not a description of it —
/// #160's rule about showing the exact bytes that leave the machine, applied to the bytes that
/// enter the model.
/// </param>
public sealed record StandingDirection(string Key, string Text)
{
    /// <summary>
    /// How long one direction may be. A direction is a sentence — long enough to say "shorter
    /// answers while I am in a fight", short enough that a dozen of them cannot quietly become
    /// most of the cached prefix.
    /// </summary>
    public const int MaxText = 240;

    /// <summary>Proposed, adopted or declined. Defaulted so a hand-edited entry is not silently live.</summary>
    public DirectionState State { get; init; } = DirectionState.Proposed;

    public DirectionKind Kind { get; init; } = DirectionKind.Direction;

    /// <summary>
    /// How good this is, derived from <see cref="State"/> and settable by nothing.
    /// <para>
    /// A proposal is d47's own reading of a sentence, which is exactly what
    /// <see cref="MemoryTier.Inferred"/> means and exactly how it is labelled in the pane. Adoption
    /// is a person's hands, which is <see cref="MemoryTier.Stated"/>. There is no third case,
    /// because a declined direction reaches nothing that reads a tier.
    /// </para>
    /// </summary>
    public MemoryTier Tier => State == DirectionState.Adopted ? MemoryTier.Stated : MemoryTier.Inferred;

    /// <summary>
    /// The Commander's own sentence this was drafted from, verbatim. What the review pane shows
    /// underneath the proposal, and the only thing that makes a proposal checkable rather than
    /// merely plausible.
    /// <para>
    /// Empty on a <see cref="DirectionKind.Question"/>, which by definition came from nobody's
    /// words; that is what the question text says instead.
    /// </para>
    /// </summary>
    public string Because { get; init; } = string.Empty;

    /// <summary>
    /// For a question, the direction it would become if the Commander agreed — prefilled into the
    /// pane's editor and never adopted on its own. Null where the pass has no honest suggestion,
    /// and the Commander then writes one or discards the question.
    /// </summary>
    public string? Suggested { get; init; }

    /// <summary>
    /// The core this applies to, or null for every core.
    /// <para>
    /// <b>This is the persona style overlay, and it lives here rather than in the pack.</b> Persona
    /// writing lives twice — <c>guardian-personas.md</c> ported into
    /// <see cref="Persona.PersonaCatalog"/> — so a loop editing either copy drifts them apart. A
    /// per-core direction is a line in <c>data\</c> that the host appends to the persona block for
    /// as long as that core is aboard, and both copies of the pack stay exactly as they shipped.
    /// </para>
    /// </summary>
    public string? Persona { get; init; }

    /// <summary>
    /// Which core was aboard when the sentence was said. Not the same thing as
    /// <see cref="Persona"/>: this is a fact about the session and that is the Commander's choice
    /// of scope, and the pane offers the second using the first.
    /// </summary>
    public string? SaidUnder { get; init; }

    /// <summary>
    /// The flight recorder row this line came from, where the recorder was on
    /// (<a href="https://github.com/dseelinger/d47/issues/164">#164</a>) — so a proposal can be
    /// checked against the exact audio rather than against a transcriber's best guess at it. Null
    /// is the ordinary case and costs nothing: the transcript alone is what the pass reads.
    /// </summary>
    public string? Clip { get; init; }

    /// <summary>When the pass drafted it.</summary>
    public DateTimeOffset? ProposedAt { get; init; }

    /// <summary>When a person took it, and null until one did.</summary>
    public DateTimeOffset? AdoptedAt { get; init; }

    /// <summary>
    /// How the pane labels it, which is the same distinction the tier makes, shown rather than
    /// implied — the rule <c>MemoryWindow</c> follows for the same reason.
    /// </summary>
    public string Label() => (State, Kind) switch
    {
        (DirectionState.Adopted, _) => "your word, in the prompt from your next session",
        (DirectionState.Declined, _) => "you turned this down",
        (_, DirectionKind.Question) => "a question, not a change",
        _ => "drafted by D47, not in the prompt",
    };
}

/// <summary>One line of the file that could not be read, and why. Kept rather than dropped.</summary>
public sealed record DirectionProblem(string What, string Why);
