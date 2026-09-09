using D47.Core.Memory;

namespace D47.Core.Debrief;

/// <summary>What the debrief drafted, and what became of it (#162).</summary>
public enum DirectionState
{
    /// <summary>Drafted by the pass and waiting on a person.</summary>
    Proposed,

    /// <summary>The Commander took it.</summary>
    Adopted,

    /// <summary>The Commander turned it down.</summary>
    Declined,
}

/// <summary>Whether the pass is proposing a direction or asking a question.</summary>
public enum DirectionKind
{
    /// <summary>Drawn from something the Commander actually said.</summary>
    Direction,

    /// <summary>Drawn from a pattern nobody put into words — see <see cref="DebriefSignalKind"/>.</summary>
    Question,
}

/// <summary>One proposed or adopted standing direction (#162).</summary>
/// <param name="Key">Identity, and what an edit replaces.</param>
/// <param name="Text">
/// The exact text that would enter the prompt, rendered verbatim by <see
/// cref="StandingDirections.Render"/>.
/// </param>
public sealed record StandingDirection(string Key, string Text)
{
    /// <summary>How long one direction may be.</summary>
    public const int MaxText = 240;

    /// <summary>Proposed, adopted or declined.</summary>
    public DirectionState State { get; init; } = DirectionState.Proposed;

    public DirectionKind Kind { get; init; } = DirectionKind.Direction;

    /// <summary>How good this is, derived from <see cref="State"/> and settable by nothing.</summary>
    public MemoryTier Tier => State == DirectionState.Adopted ? MemoryTier.Stated : MemoryTier.Inferred;

    /// <summary>The Commander's own sentence this was drafted from, verbatim.</summary>
    public string Because { get; init; } = string.Empty;

    /// <summary>
    /// For a question, the direction it would become if the Commander agreed — prefilled into the
    /// pane's editor and never adopted on its own.
    /// </summary>
    public string? Suggested { get; init; }

    /// <summary>The core this applies to, or null for every core.</summary>
    public string? Persona { get; init; }

    /// <summary>Which core was aboard when the sentence was said.</summary>
    public string? SaidUnder { get; init; }

    /// <summary>
    /// The audio recorder row this line came from, where the recorder was on (#164) — so a proposal can
    /// be checked against the exact audio rather than against a transcriber's best guess at it.
    /// </summary>
    public string? Clip { get; init; }

    /// <summary>When the pass drafted it.</summary>
    public DateTimeOffset? ProposedAt { get; init; }

    /// <summary>When a person took it, and null until one did.</summary>
    public DateTimeOffset? AdoptedAt { get; init; }

    /// <summary>
    /// How the pane labels it, which is the same distinction the tier makes, shown rather than implied
    /// — the rule <c>MemoryWindow</c> follows for the same reason.
    /// </summary>
    public string Label() => (State, Kind) switch
    {
        (DirectionState.Adopted, _) => "your word, in the prompt from your next session",
        (DirectionState.Declined, _) => "you turned this down",
        (_, DirectionKind.Question) => "a question, not a change",
        _ => "drafted by D47, not in the prompt",
    };
}

/// <summary>One line of the file that could not be read, and why.</summary>
public sealed record DirectionProblem(string What, string Why);
