namespace D47.Core.Goals;

/// <summary>How an arc's progress is settled (Phase 34, "Progress is derived, never typed").</summary>
public enum GoalKind
{
    /// <summary>A diff against journal state.</summary>
    Derived,

    /// <summary>A goal the Commander invented.</summary>
    Authored,
}

/// <summary>
/// A named ambition with a definition of done — the thing the checklist has never had anywhere to put
/// (Phase 34, "Goals that outlive a checklist").
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

    /// <summary>The unit the figures are in — "engineers", "hulls", "systems", "light years".</summary>
    public string? Unit { get; init; }

    /// <summary>The tool d47 already has for this career, where there is one.</summary>
    public string? Helper { get; init; }

    /// <summary>When the Commander wrote it.</summary>
    public DateTimeOffset? Written { get; init; }

    /// <summary>Their own decision, and the only place in this namespace a person's tick is stored.</summary>
    public bool Finished { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }
}

/// <summary>Where one figure came from, which decides how much a Commander should trust it.</summary>
public enum GoalSource
{
    /// <summary>Nothing has ever been able to say.</summary>
    Unknown,

    /// <summary>The game state as it stands right now.</summary>
    Live,

    /// <summary>The last pass over the journals on this disk, and it says when.</summary>
    Mined,

    /// <summary>A person said so.</summary>
    Commander,
}

/// <summary>What an arc is worth, computed rather than read (Phase 34).</summary>
public sealed record GoalStanding
{
    public required GoalArc Arc { get; init; }

    /// <summary>How far along, in <see cref="GoalArc.Unit"/>.</summary>
    public long? Have { get; init; }

    /// <summary>What done looks like as a figure, where done has one.</summary>
    public long? Need { get; init; }

    public required GoalSource Source { get; init; }

    /// <summary>When <see cref="Have"/> was true.</summary>
    public DateTimeOffset? AsOf { get; init; }

    /// <summary>When the arc started, from the mine.</summary>
    public DateTimeOffset? Started { get; init; }

    /// <summary>The detail the figure alone loses — "rank 5 of 8, 12% into it".</summary>
    public string? Note { get; init; }

    public bool IsDone { get; init; }

    /// <summary>0 to 1, or null where either half is unknown.</summary>
    public double? Fraction => Have is { } have && Need is { } need && need > 0
        ? Math.Clamp((double)have / need, 0, 1)
        : null;

    /// <summary>How long it has been running, against the instant it is asked at.</summary>
    public TimeSpan? Age(DateTimeOffset now) => Started is { } started && started <= now ? now - started : null;

    /// <summary>The whole arc in one sentence — progress, age, and where the figure came from.</summary>
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
