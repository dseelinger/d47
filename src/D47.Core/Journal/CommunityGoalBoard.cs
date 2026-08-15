using System.Globalization;
using System.Text.Json;

namespace D47.Core.Journal;

/// <summary>
/// Whether the Commander has signed up to a goal, as the journal has said so.
/// <para>
/// Three states rather than a flag, because <see cref="Unsaid"/> is the common one: the board
/// event reports goals the Commander has merely docked near, and reading those as "not joined"
/// would be an assertion the journal never made.
/// </para>
/// </summary>
public enum CommunityGoalSignup
{
    Unsaid,
    Joined,
    Left,
}

/// <summary>One community goal, as the station's board reported it.</summary>
/// <param name="Id">
/// The journal's <c>CGID</c>. The key throughout, because a title is a string two sources spell
/// differently and an id is not — it is what an external listing has to be matched on.
/// </param>
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

    /// <summary>
    /// The tier the goal has actually reached. Elite writes it as <c>"Tier 3"</c> and only once
    /// the first success tier is met, so null means "not there yet" rather than "unknown".
    /// </summary>
    public int? TierReached { get; init; }

    /// <summary>The top tier on offer, from <c>TopTier.Name</c>. Absent on some boards.</summary>
    public int? TopTier { get; init; }

    /// <summary>
    /// What the top tier pays, in Frontier's own words — free text, written per goal, and very
    /// often empty. Kept verbatim rather than parsed into a number.
    /// </summary>
    public string? TopTierBonus { get; init; }

    public int? TopRankSize { get; init; }

    public bool PlayerInTopRank { get; init; }

    /// <summary>Where the Commander stands, 0 to 100. Meaningful only once they have contributed.</summary>
    public int? PlayerPercentileBand { get; init; }

    /// <summary>The credit reward for the band reached, where a tier has been met.</summary>
    public long? Bonus { get; init; }

    public CommunityGoalSignup Signup { get; init; }

    /// <summary>What was actually paid out, from <c>CommunityGoalReward</c>.</summary>
    public long? RewardPaid { get; init; }

    /// <summary>
    /// When this was last reported — the timestamp of the journal line rather than the clock.
    /// Every answer about a goal is really an answer about a board snapshot, and a snapshot has
    /// an age.
    /// </summary>
    public DateTimeOffset SeenAt { get; init; }

    /// <summary>
    /// Whether the Commander is running this one. A contribution is the strong evidence and a
    /// sign-up the weak one: a goal can be joined and never flown for, but nobody contributes to
    /// a goal by accident.
    /// </summary>
    public bool IsParticipating =>
        PlayerContribution > 0 || (Signup == CommunityGoalSignup.Joined && RewardPaid is null);

    /// <summary>
    /// Whether the goal can still be contributed to at <paramref name="now"/>.
    /// <para>
    /// <b>This is the trap the type exists around.</b> <c>CurrentGoals</c> is a board snapshot
    /// rather than a list of live goals, and the corpus holds one reported four days after it
    /// expired, carrying <c>IsComplete: true</c>. Announcing a finished goal as something a
    /// Commander can still fly for is a wrong answer that reads exactly like the feature
    /// working — and because the event fires on every dock, it is the common case rather than
    /// the edge one. An expiry that could not be read counts as live, because the alternative is
    /// hiding a goal that may well be running.
    /// </para>
    /// </summary>
    public bool IsLive(DateTimeOffset now) => Expiry is not { } expiry || expiry > now;

    /// <summary>Where it is flown, as one phrase. Null when the board named neither.</summary>
    public string? Where => (StationName, SystemName) switch
    {
        ({ } station, { } system) => $"{station}, {system}",
        (null, { } system) => system,
        ({ } station, null) => station,
        _ => null,
    };

    /// <summary>
    /// The tier as a number, from Elite's <c>"Tier 4"</c>. Anything that is not that shape is
    /// dropped rather than guessed at: a tier is an ordering a Commander plans around, and a
    /// wrong one is worse than a missing one.
    /// </summary>
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
/// Every community goal the Commander's journal has reported (list.md Phase 14, "Know the
/// current community goals"), folded from <c>CommunityGoal</c> and the three events around it.
/// <para>
/// <b>The split the item was written around is not the split the journal makes.</b> The board
/// event fires off the noticeboard at a station, so it carries goals the Commander merely docked
/// near — 952 of 16,999 entries across 912 journals report a contribution of zero. What an
/// external source has to cover is therefore "everywhere I have not been", which is wider than
/// "everything I have not joined". See <c>docs/spikes/community-goals.md</c>.
/// </para>
/// <para>
/// <b>Merged by id, never replaced.</b> <c>CurrentGoals</c> looks like a complete board per
/// event, which would argue for replacing — but it is the board at <em>one station</em>, and no
/// station in the corpus ran more than one goal at a time, so the two-stations case is untested.
/// Replacing on an untested assumption loses a goal the Commander is actually running; merging
/// keeps a goal that has ended, which <see cref="CommunityGoal.IsLive"/> already has to handle
/// because the snapshot goes stale regardless. Only one of those two failures is silent.
/// </para>
/// </summary>
public sealed record CommunityGoalBoard
{
    public static readonly CommunityGoalBoard Empty = new();

    public IReadOnlyList<CommunityGoal> Goals { get; init; } = [];

    /// <summary>When anything was last reported. Null means the journal has never said.</summary>
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

            // The board says nothing about signing up or being paid, so those two survive the
            // merge. Taking the defaults of a fresh read would un-join a goal every time the
            // Commander docked.
            merged[at] = goal with { Signup = merged[at].Signup, RewardPaid = merged[at].RewardPaid };
        }

        return this with { Goals = merged, SeenAt = Later(journalEvent.Timestamp) };
    }

    /// <summary>
    /// Applies a change that names one goal. The three sign-up events carry <c>CGID</c>,
    /// <c>Name</c> and <c>System</c> and nothing else, so one arriving for a goal no board has
    /// reported still establishes what it says rather than being dropped — a Commander who
    /// joined a goal before d47 was watching is not a Commander who has not joined it.
    /// </summary>
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

    /// <summary>
    /// The newer of what is already known and what has just arrived. Journals are replayed in
    /// order in production and out of order in a harness, and "when did anything last say
    /// anything" should not go backwards either way.
    /// </summary>
    private DateTimeOffset Later(DateTimeOffset arrived) =>
        SeenAt is { } seen && seen > arrived ? seen : arrived;

    private static CommunityGoal? Read(JsonElement entry, DateTimeOffset seenAt)
    {
        // An id and a title, or nothing. A goal with no id cannot be merged against the next
        // sighting of itself, and one with no title cannot be said out loud.
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
    /// The same rule <see cref="CarrierState"/> applies to a departure time, for the same
    /// reason: an unparseable expiry is dropped rather than defaulted, because a wrong deadline
    /// is worse than no deadline when the whole use of it is deciding whether there is time.
    /// </summary>
    private static DateTimeOffset? ParseExpiry(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
}
