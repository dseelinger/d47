namespace D47.Core.Journal;

/// <summary>One Operation, from the first line Elite marked for it to the first unmarked line after.</summary>
public sealed record OperationRun(DateTimeOffset OpenedAt)
{
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>The single active mission in the first <c>Missions</c> event of the run, or null.</summary>
    public string? Name { get; init; }

    /// <summary>The names on each <c>WingAdd</c>, in order, without repeats and without the Commander's own.</summary>
    public IReadOnlyList<string> Squad { get; init; } = [];

    public int Kills { get; init; }

    public int ItemsCollected { get; init; }

    public long? CreditsBefore { get; init; }

    public long? CreditsAfter { get; init; }

    public double? MercCoinsBefore { get; init; }

    /// <summary>Set by the first <c>Statistics</c> after the run closes.</summary>
    public double? MercCoinsEarned { get; init; }

    public long? Reward => CreditsAfter - CreditsBefore;

    /// <summary>True when the Commander's credits rose across the run.</summary>
    public bool Finished => Reward > 0;

    public TimeSpan? Duration => ClosedAt - OpenedAt;

    internal bool MissionsSeen { get; init; }

    internal OperationRun Count(JournalEvent journalEvent, string? commander)
    {
        switch (journalEvent.Kind)
        {
            case "Missions" when !MissionsSeen:
                var active = journalEvent.Items("Active").ToList();
                return this with { MissionsSeen = true, Name = active.Count == 1 ? active[0].String("Name") : null };

            case "WingAdd" when journalEvent.String("Name") is { Length: > 0 } name
                && !string.Equals(name, commander, StringComparison.OrdinalIgnoreCase)
                && !Squad.Contains(name, StringComparer.OrdinalIgnoreCase):
                return this with { Squad = [.. Squad, name] };

            case "Bounty" or "FactionKillBond":
                return this with { Kills = Kills + 1 };

            case "CollectItems":
                return this with { ItemsCollected = ItemsCollected + 1 };

            default:
                return this;
        }
    }
}

/// <summary>The Operation run in progress and the last one to end.</summary>
public sealed record OperationRuns
{
    public static readonly OperationRuns None = new();

    public OperationRun? Open { get; init; }

    public OperationRun? Last { get; init; }

    private string? Commander { get; init; }

    private long? LastCredits { get; init; }

    private double? MercCoins { get; init; }

    private bool AwaitingStatistics { get; init; }

    public OperationRuns Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        var state = this;

        if (journalEvent.InOperation && state.Open is null)
        {
            state = state.Begin(journalEvent);
        }
        else if (!journalEvent.InOperation && state.Open is not null)
        {
            state = state.End(journalEvent);
        }
        else if (state.Open is { } open)
        {
            state = state with { Open = open.Count(journalEvent, state.Commander) };
        }

        return state.Remember(journalEvent, opened: journalEvent.InOperation && Open is null);
    }

    private OperationRuns Begin(JournalEvent journalEvent)
    {
        var known = journalEvent.Kind == "LoadGame" ? Remember(journalEvent, opened: false) : this;
        var run = new OperationRun(journalEvent.Timestamp)
        {
            CreditsBefore = known.LastCredits,
            MercCoinsBefore = known.MercCoins,
        };

        return known with { Open = run.Count(journalEvent, known.Commander) };
    }

    private OperationRuns End(JournalEvent journalEvent)
    {
        var run = Open! with { ClosedAt = journalEvent.Timestamp };

        if (journalEvent.Kind == "LoadGame" && run.CreditsAfter is null)
        {
            run = run with { CreditsAfter = journalEvent.Long("Credits") };
        }

        return this with { Open = null, Last = run, AwaitingStatistics = true };
    }

    private OperationRuns Remember(JournalEvent journalEvent, bool opened)
    {
        switch (journalEvent.Kind)
        {
            case "LoadGame":
                var credits = journalEvent.Long("Credits");
                var withCredits = Open is { CreditsAfter: null } run && !opened
                    ? this with { Open = run with { CreditsAfter = credits } }
                    : this;

                return withCredits with { LastCredits = credits ?? LastCredits, Commander = journalEvent.String("Commander") ?? Commander };

            case "Statistics":
                var coins = Sum(journalEvent);
                var settled = AwaitingStatistics && Last is { } last
                    ? this with { Last = last with { MercCoinsEarned = coins - last.MercCoinsBefore } }
                    : this;

                return settled with { MercCoins = coins, AwaitingStatistics = false };

            default:
                return this;
        }
    }

    private static double? Sum(JournalEvent journalEvent) =>
        journalEvent.Object("Bank_Account") is { } bank && bank.Double("MercCoins_Current") is { } held && bank.Double("MercCoins_Total_Spent") is { } spent
            ? held + spent
            : null;
}
