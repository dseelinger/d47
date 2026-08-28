namespace D47.Core.Goals;

/// <summary>
/// How an arc's progress is settled (Phase 34, "Progress is derived, never typed").
/// <para>
/// The same rule the checklist already draws in <see cref="Checklists.ChecklistItemKind"/> and for
/// the same reason: mixing computed progress with typed progress gives a page where some figures
/// are facts and some are opinions, with nothing on it saying which.
/// </para>
/// </summary>
public enum GoalKind
{
    /// <summary>A diff against journal state. <b>Cannot be ticked by hand and does not offer to be.</b></summary>
    Derived,

    /// <summary>A goal the Commander invented. A person decides when it is done, like an authored line.</summary>
    Authored,
}

/// <summary>
/// A named ambition with a definition of done — the thing the checklist has never had anywhere to
/// put (Phase 34, "Goals that outlive a checklist").
/// <para>
/// This is the <em>definition</em>. What it is worth right now is a <see cref="GoalStanding"/>,
/// computed fresh every time it is asked for, because <b>no progress figure is ever stored</b> —
/// the same rule <see cref="Checklists.ChecklistItem"/> states about numbers, arrived at from the
/// same direction. A stored percentage gets recited in October with nothing left to say where it
/// came from.
/// </para>
/// </summary>
public sealed record GoalArc
{
    /// <summary>Stable, and what a set-aside and a promoted checklist line are keyed by.</summary>
    public required string Key { get; init; }

    /// <summary>What the Commander calls it — "Elite in Exploration".</summary>
    public required string Name { get; init; }

    /// <summary>The definition of done, in words, because an arc nobody can state the end of is a mood.</summary>
    public required string Done { get; init; }

    public GoalKind Kind { get; init; } = GoalKind.Derived;

    /// <summary>
    /// The unit the figures are in — "engineers", "hulls", "systems", "light years". Used in the
    /// sentence rather than in the arithmetic, so an arc with no natural noun simply has none.
    /// </summary>
    public string? Unit { get; init; }

    /// <summary>
    /// The tool d47 already has for this career, where there is one. <b>Naming a capability that
    /// exists is not inventing a plan</b>, which is what a rank arc would otherwise have to do to
    /// have anything to say about today.
    /// </summary>
    public string? Helper { get; init; }

    /// <summary>When the Commander wrote it. Null on a built-in arc, which nobody wrote.</summary>
    public DateTimeOffset? Written { get; init; }

    /// <summary>Their own decision, and the only place in this namespace a person's tick is stored.</summary>
    public bool Finished { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }
}

/// <summary>
/// Where one figure came from, which decides how much a Commander should trust it.
/// <para>
/// <b>Item 2's "null stays a real answer", as an enum rather than as a comment.</b> An arc reported
/// from history and an arc reported from the live game are both true and are not the same claim,
/// and an arc nothing can currently say anything about must not read as zero.
/// </para>
/// </summary>
public enum GoalSource
{
    /// <summary>Nothing has ever been able to say. Not the same as none, and never drawn as none.</summary>
    Unknown,

    /// <summary>The game state as it stands right now.</summary>
    Live,

    /// <summary>The last pass over the journals on this disk, and it says when.</summary>
    Mined,

    /// <summary>A person said so.</summary>
    Commander,
}

/// <summary>
/// What an arc is worth, computed rather than read (Phase 34).
/// </summary>
public sealed record GoalStanding
{
    public required GoalArc Arc { get; init; }

    /// <summary>How far along, in <see cref="GoalArc.Unit"/>. <b>Null is "cannot say", never zero.</b></summary>
    public long? Have { get; init; }

    /// <summary>What done looks like as a figure, where done has one.</summary>
    public long? Need { get; init; }

    public required GoalSource Source { get; init; }

    /// <summary>When <see cref="Have"/> was true. Now, for a live figure; the mining run, for a mined one.</summary>
    public DateTimeOffset? AsOf { get; init; }

    /// <summary>
    /// When the arc started, from the mine. Null where the corpus holds no evidence the Commander
    /// has begun this one at all — which is a real answer and is drawn as one.
    /// </summary>
    public DateTimeOffset? Started { get; init; }

    /// <summary>The detail the figure alone loses — "rank 5 of 8, 12% into it".</summary>
    public string? Note { get; init; }

    public bool IsDone { get; init; }

    /// <summary>0 to 1, or null where either half is unknown. Never invented from a null.</summary>
    public double? Fraction => Have is { } have && Need is { } need && need > 0
        ? Math.Clamp((double)have / need, 0, 1)
        : null;

    /// <summary>How long it has been running, against the instant it is asked at.</summary>
    public TimeSpan? Age(DateTimeOffset now) => Started is { } started && started <= now ? now - started : null;

    /// <summary>
    /// The whole arc in one sentence — progress, age, and where the figure came from.
    /// <para>
    /// Here rather than beside the page, so the drawn line and the spoken one are the same
    /// sentence. <see cref="Checklists.ChecklistNextAction"/> records the reason: a state a
    /// Commander reads one way and hears another is two behaviours to keep in step.
    /// </para>
    /// </summary>
    public string Describe(DateTimeOffset now)
    {
        var parts = new List<string> { Arc.Name + ":" };

        if (IsDone)
        {
            parts.Add("done.");
        }
        else if (Note is { Length: > 0 } note)
        {
            parts.Add(note + ".");
        }
        else if (Have is { } have && Need is { } need)
        {
            parts.Add($"{have:N0} of {need:N0}{Unit()}.");
        }
        else if (Have is { } only)
        {
            parts.Add($"{only:N0}{Unit()}.");
        }
        else
        {
            parts.Add("I cannot say yet.");
        }

        if (Source == GoalSource.Mined && AsOf is { } stamp)
        {
            parts.Add($"That is as of {stamp:d MMM yyyy}, from your journals.");
        }

        if (!IsDone && Age(now) is { } age && age.TotalDays >= 1)
        {
            parts.Add($"Running {Describe(age)}.");
        }

        return string.Join(" ", parts);
    }

    private string Unit() => Arc.Unit is { Length: > 0 } unit ? " " + unit : string.Empty;

    private static string Describe(TimeSpan age) => age.TotalDays switch
    {
        < 14 => $"{(int)age.TotalDays} days",
        < 90 => $"{(int)(age.TotalDays / 7)} weeks",
        < 730 => $"{(int)(age.TotalDays / 30.44)} months",
        _ => $"{age.TotalDays / 365.25:0.#} years",
    };
}
