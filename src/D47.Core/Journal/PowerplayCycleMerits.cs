namespace D47.Core.Journal;

/// <summary>One <c>PowerplayMerits</c> gain for the pledged Power.</summary>
public readonly record struct MeritGain(DateTimeOffset At, long Gained);

/// <summary>
/// The merits earned for the pledged Power over the last week, so the total since any cycle boundary can be
/// summed. Joining, defecting and leaving start it again.
/// </summary>
public sealed record PowerplayCycleMerits
{
    public static readonly PowerplayCycleMerits None = new();

    /// <summary>A cycle is a week; anything older cannot fall in the current one, wherever the boundary is set.</summary>
    private static readonly TimeSpan Kept = TimeSpan.FromDays(7);

    /// <summary>The Power the gains were earned for, or null.</summary>
    public string? Power { get; init; }

    /// <summary>The gains, oldest first.</summary>
    public IReadOnlyList<MeritGain> Gains { get; init; } = [];

    /// <summary>When a join, defection or departure last started it again, or null.</summary>
    public DateTimeOffset? ResetAt { get; init; }

    /// <summary>Whether any event has been folded.</summary>
    public bool IsKnown => Power is not null || ResetAt is not null;

    /// <summary>The merits earned at or after <paramref name="cycleStart"/>.</summary>
    public long Since(DateTimeOffset cycleStart) =>
        Gains.Where(gain => gain.At >= cycleStart).Sum(gain => gain.Gained);

    /// <summary>Folds one event, given the pledge as it stands after that event.</summary>
    public PowerplayCycleMerits Apply(JournalEvent journalEvent, PowerplayPledge pledge) => journalEvent.Kind switch
    {
        "PowerplayJoin" or "PowerplayDefect" or "PowerplayLeave" =>
            new PowerplayCycleMerits { Power = pledge.Power, ResetAt = journalEvent.Timestamp },

        "PowerplayMerits" when pledge.IsPledged
            && string.Equals(journalEvent.String("Power"), pledge.Power, StringComparison.OrdinalIgnoreCase)
            && journalEvent.Long("MeritsGained") is { } gained =>
            Gained(pledge.Power!, new MeritGain(journalEvent.Timestamp, gained)),

        _ => this,
    };

    /// <summary>
    /// This, recovered from older journals, with what the live journal has folded since laid over it. A gain
    /// in both is counted once.
    /// </summary>
    public PowerplayCycleMerits With(PowerplayCycleMerits live)
    {
        ArgumentNullException.ThrowIfNull(live);

        if (!live.IsKnown)
        {
            return this;
        }

        var lastRecovered = Gains.Count > 0 ? Gains[^1].At : (DateTimeOffset?)null;

        if (live.ResetAt is { } reset && (lastRecovered is not { } last || reset >= last)
            || !string.Equals(live.Power, Power, StringComparison.OrdinalIgnoreCase)
            || lastRecovered is null)
        {
            return live;
        }

        // The walk read the current journal too, up to some point: gains after its last are new, and at that
        // same second only those beyond the count it already holds.
        var atLast = Gains.Count(gain => gain.At == lastRecovered);
        var newer = live.Gains.Where(gain => gain.At > lastRecovered);
        var sameSecond = live.Gains.Where(gain => gain.At == lastRecovered).Skip(atLast);

        return this with { Gains = Trimmed([.. Gains, .. sameSecond, .. newer]) };
    }

    private PowerplayCycleMerits Gained(string power, MeritGain gain) =>
        string.Equals(power, Power, StringComparison.OrdinalIgnoreCase)
            ? this with { Gains = Trimmed([.. Gains, gain]) }
            : new PowerplayCycleMerits { Power = power, Gains = [gain], ResetAt = ResetAt };

    private static MeritGain[] Trimmed(MeritGain[] gains)
    {
        var newest = gains.Length > 0 ? gains.Max(gain => gain.At) : default;

        return [.. gains.Where(gain => gain.At > newest - Kept).OrderBy(gain => gain.At)];
    }
}
