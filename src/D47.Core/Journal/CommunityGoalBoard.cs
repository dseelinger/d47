using System.Globalization;
using System.Text.Json;

namespace D47.Core.Journal;

/// <summary>Whether the Commander has signed up to a goal, as the journal has said so.</summary>
public enum CommunityGoalSignup
{
    Unsaid,
    Joined,
    Left,
}

/// <summary>One community goal, as the station's board reported it.</summary>
/// <param name="Id">The journal's <c>CGID</c>.</param>
public sealed record CommunityGoal(int Id, string Title)
{
    public string? SystemName { get; init; }

    /// <summary>The journal calls this <c>MarketName</c>; a Commander calls it the station.</summary>
    public string? StationName { get; init; }

    public DateTimeOffset? Expiry { get; init; }

    public bool IsComplete { get; init; }

    public long? CurrentTotal { get; init; }

    public long? PlayerContribution { get; init; }

    public int? NumContributors { get; init; }

    /// <summary>The tier the goal has actually reached.</summary>
    public int? TierReached { get; init; }

    /// <summary>The top tier on offer, from <c>TopTier.Name</c>.</summary>
    public int? TopTier { get; init; }

    /// <summary>
    /// What the top tier pays, in Frontier's own words — free text, written per goal, and very often
    /// empty.
    /// </summary>
    public string? TopTierBonus { get; init; }

    public int? TopRankSize { get; init; }

    public bool PlayerInTopRank { get; init; }

    /// <summary>Where the Commander stands, 0 to 100.</summary>
    public int? PlayerPercentileBand { get; init; }

    /// <summary>The credit reward for the band reached, where a tier has been met.</summary>
    public long? Bonus { get; init; }

    public CommunityGoalSignup Signup { get; init; }

    /// <summary>What was actually paid out, from <c>CommunityGoalReward</c>.</summary>
    public long? RewardPaid { get; init; }

    /// <summary>When this was last reported — the timestamp of the journal line rather than the clock.</summary>
    public DateTimeOffset SeenAt { get; init; }

    /// <summary>Whether the Commander is running this one.</summary>
    public bool IsParticipating =>
        PlayerContribution > 0 || (Signup == CommunityGoalSignup.Joined && RewardPaid is null);

    /// <summary>Whether the goal can still be contributed to at <paramref name="now"/>.</summary>
    public bool IsLive(DateTimeOffset now) => Expiry is not { } expiry || expiry > now;

    /// <summary>Where it is flown, as one phrase.</summary>
    public string? Where => (StationName, SystemName) switch
    {
        ({ } station, { } system) => $"{station}, {system}",
        (null, { } system) => system,
        ({ } station, null) => station,
        _ => null,
    };

    /// <summary>The tier as a number, from Elite's <c>"Tier 4"</c>.</summary>
    internal static int? ParseTier(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var digits = value.AsSpan().Trim();
        var space = digits.LastIndexOf(' ');

        if (space >= 0)
        {
            digits = digits[(space + 1)..];
        }

        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tier) ? tier : null;
    }
}

/// <summary>
/// Every community goal the Commander's journal has reported (Phase 14, "Know the current community
/// goals"), folded from <c>CommunityGoal</c> and the three events around it.
/// </summary>
public sealed record CommunityGoalBoard
{
    public static readonly CommunityGoalBoard Empty = new();

    public IReadOnlyList<CommunityGoal> Goals { get; init; } = [];

    /// <summary>When anything was last reported.</summary>
    public DateTimeOffset? SeenAt { get; init; }

    /// <summary>
    /// Whether anything has been heard at all — the difference between "no community goals" and
    /// "nothing has told me yet", and only one of those is an answer about the Commander.
    /// </summary>
    public bool IsKnown => SeenAt is not null;

    public CommunityGoal? For(int id) => Goals.FirstOrDefault(goal => goal.Id == id);

    /// <summary>Goals not expired as of <paramref name="now"/>, most recently reported first.</summary>
    public IReadOnlyList<CommunityGoal> Live(DateTimeOffset now) =>
        [.. Goals.Where(goal => goal.IsLive(now)).OrderByDescending(goal => goal.SeenAt)];

    public CommunityGoalBoard Apply(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        "CommunityGoal" => ApplyBoard(journalEvent),
        "CommunityGoalJoin" => Mark(journalEvent, goal => goal with { Signup = CommunityGoalSignup.Joined }),
        "CommunityGoalDiscard" => Mark(journalEvent, goal => goal with { Signup = CommunityGoalSignup.Left }),
        "CommunityGoalReward" => Mark(journalEvent, goal => goal with { RewardPaid = journalEvent.Long("Reward") }),
        _ => this,
    };

    private CommunityGoalBoard ApplyBoard(JournalEvent journalEvent)
    {
        var reported = journalEvent
            .Items("CurrentGoals")
            .Select(entry => Read(entry, journalEvent.Timestamp))
            .Where(goal => goal is not null)
            .Select(goal => goal!)
            .ToList();

        if (reported.Count == 0)
        {
            return this;
        }

        var merged = Goals.ToList();

        foreach (var goal in reported)
        {
            var at = merged.FindIndex(existing => existing.Id == goal.Id);

            if (at < 0)
            {
                merged.Add(goal);
                continue;
            }

            // The board says nothing about signing up or being paid, so those two survive the merge.
            merged[at] = goal with { Signup = merged[at].Signup, RewardPaid = merged[at].RewardPaid };
        }

        return this with { Goals = merged, SeenAt = Later(journalEvent.Timestamp) };
    }

    /// <summary>Applies a change that names one goal.</summary>
    private CommunityGoalBoard Mark(JournalEvent journalEvent, Func<CommunityGoal, CommunityGoal> change)
    {
        if (journalEvent.Int("CGID") is not { } id)
        {
            return this;
        }

        var goals = Goals.ToList();
        var at = goals.FindIndex(goal => goal.Id == id);

        var subject = change(at >= 0
            ? goals[at]
            : new CommunityGoal(id, journalEvent.String("Name") ?? $"Community goal {id}")
            {
                SystemName = journalEvent.String("System"),
                SeenAt = journalEvent.Timestamp,
            });

        if (at >= 0)
        {
            goals[at] = subject;
        }
        else
        {
            goals.Add(subject);
        }

        return this with { Goals = goals, SeenAt = Later(journalEvent.Timestamp) };
    }

    /// <summary>The newer of what is already known and what has just arrived.</summary>
    private DateTimeOffset Later(DateTimeOffset arrived) =>
        SeenAt is { } seen && seen > arrived ? seen : arrived;

    private static CommunityGoal? Read(JsonElement entry, DateTimeOffset seenAt)
    {
        // An id and a title, or nothing.
        if (entry.Int("CGID") is not { } id || entry.String("Title") is not { } title)
        {
            return null;
        }

        var topTier = entry.Object("TopTier");

        return new CommunityGoal(id, title)
        {
            SystemName = entry.String("SystemName"),
            StationName = entry.String("MarketName"),
            Expiry = ParseExpiry(entry.String("Expiry")),
            IsComplete = entry.Bool("IsComplete"),
            CurrentTotal = entry.Long("CurrentTotal"),
            PlayerContribution = entry.Long("PlayerContribution"),
            NumContributors = entry.Int("NumContributors"),
            TierReached = CommunityGoal.ParseTier(entry.String("TierReached")),
            TopTier = topTier is { } tier ? CommunityGoal.ParseTier(tier.String("Name")) : null,
            TopTierBonus = topTier?.String("Bonus") is { Length: > 0 } bonus ? bonus : null,
            TopRankSize = entry.Int("TopRankSize"),
            PlayerInTopRank = entry.Bool("PlayerInTopRank"),
            PlayerPercentileBand = entry.Int("PlayerPercentileBand"),
            Bonus = entry.Long("Bonus"),
            SeenAt = seenAt,
        };
    }

    /// <summary>
    /// The same rule <see cref="CarrierState"/> applies to a departure time, for the same reason: an
    /// unparseable expiry is dropped rather than defaulted, because a wrong deadline is worse than no
    /// deadline when the whole use of it is deciding whether there is time.
    /// </summary>
    private static DateTimeOffset? ParseExpiry(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
}
