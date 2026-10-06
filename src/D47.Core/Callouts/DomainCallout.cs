using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Persona;

namespace D47.Core.Callouts;

/// <summary>
/// A remark on the subject the core aboard pays attention to, carrying a figure from this session (#611, #613,
/// #614), or naming a community goal not yet joined (#612). A core with no domain says nothing.
/// </summary>
public sealed class DomainCallout : ICallout
{
    /// <summary>Every domain key starts with this; <see cref="RewordChance"/> always rewords them.</summary>
    public const string KeyPrefix = "domain.";

    public const string EarningsKey = KeyPrefix + "earnings";

    public const string CombatKey = KeyPrefix + "combat";

    public const string ExplorationKey = KeyPrefix + "exploration";

    public const string RepairsKey = KeyPrefix + "repairs";

    public const string FirstsKey = KeyPrefix + "firsts";

    public const string CommunityGoalKey = KeyPrefix + "community-goal";

    /// <summary>The journal events that pay the Commander, and so can prompt an earnings remark.</summary>
    private static readonly HashSet<string> EarningEvents = new(StringComparer.Ordinal)
    {
        "MarketSell",
        "MissionCompleted",
        "RedeemVoucher",
        "SellExplorationData",
        "MultiSellExplorationData",
        "SellOrganicData",
    };

    /// <summary>The journal events that pay for combat, and so can prompt a combat remark.</summary>
    private static readonly HashSet<string> CombatEvents = new(StringComparer.Ordinal)
    {
        "Bounty",
        "FactionKillBond",
        "RedeemVoucher",
    };

    /// <summary>The journal events that pay for exploration data, and so can prompt an exploration remark.</summary>
    private static readonly HashSet<string> ExplorationEvents = new(StringComparer.Ordinal)
    {
        "SellExplorationData",
        "MultiSellExplorationData",
        "SellOrganicData",
    };

    public string Id => "domain";

    /// <summary>The domain of the core aboard, read each tick.</summary>
    public Func<PersonaDomain> Domain { get; set; } = () => PersonaDomain.None;

    /// <summary>Off means no domain remark, whichever core is aboard.</summary>
    public Func<bool> Enabled { get; set; } = () => true;

    /// <summary>How old the session has to be, in journal time, before a rate is worth saying.</summary>
    public TimeSpan LeastSession { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>The shortest gap between two remarks.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>The least time a goal must have left before it is worth naming.</summary>
    public TimeSpan LeastGoalTime { get; set; } = TimeSpan.FromHours(24);

    /// <summary>The repair spend, in credits, the session must pass before it is worth saying.</summary>
    public long LeastRepairSpend { get; set; } = 100_000;

    /// <summary>How many firsts the session needs before they are counted aloud.</summary>
    public int LeastFirsts { get; set; } = 3;

    private DateTimeOffset? _lastAt;

    private readonly HashSet<int> _namedGoals = [];

    /// <summary>The rate said last this session, in credits an hour.</summary>
    private long? _lastRate;

    private PersonaDomain _lastRateDomain;

    private readonly HashSet<long> _firstStars = [];

    private readonly HashSet<(long System, int Body)> _firstMaps = [];

    /// <summary>Whether the first scan seen of each body said it was not yet mapped.</summary>
    private readonly Dictionary<(long System, int Body), bool> _unmapped = [];

    private int _firstFootfalls;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var earned = false;
        var fought = false;
        var explored = false;
        var repaired = false;
        var first = false;

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind == "LoadGame")
            {
                _lastAt = null;
                _lastRate = null;
                _namedGoals.Clear();
                _firstStars.Clear();
                _firstMaps.Clear();
                _unmapped.Clear();
                _firstFootfalls = 0;
                earned = false;
                fought = false;
                explored = false;
                repaired = false;
                first = false;
            }
            else
            {
                earned |= EarningEvents.Contains(journalEvent.Kind);
                fought |= CombatEvents.Contains(journalEvent.Kind);
                explored |= ExplorationEvents.Contains(journalEvent.Kind);
                repaired |= journalEvent.Kind is "Repair" or "RepairAll";
                first |= CountFirst(journalEvent, context.State);
            }
        }

        var domain = Domain();

        if (context.IsPriming || !Enabled() || domain == PersonaDomain.None)
        {
            yield break;
        }

        var previousRate = _lastRateDomain == domain ? _lastRate : null;

        if (context.State?.Session is { } session
            && !(_lastAt is { } last && context.Now - last < Interval)
            && Remark(domain, session, previousRate, earned, fought, explored, repaired, first) is var (key, remarkText, rate))
        {
            _lastAt = context.Now;

            if (rate is { } said)
            {
                _lastRate = said;
                _lastRateDomain = domain;
            }

            yield return new Announcement(key, remarkText);
        }

        if (domain == PersonaDomain.Earnings && context.State is { } state)
        {
            foreach (var goal in state.CommunityGoals.Goals)
            {
                if (UnjoinedGoal(goal, context.Now, LeastGoalTime) is { } text && _namedGoals.Add(goal.Id))
                {
                    yield return new Announcement(CommunityGoalKey, text);
                    yield break;
                }
            }
        }
    }

    private (string Key, string Text, long? Rate)? Remark(
        PersonaDomain domain,
        SessionSummary session,
        long? previousRate,
        bool earned,
        bool fought,
        bool explored,
        bool repaired,
        bool first)
    {
        switch (domain)
        {
            case PersonaDomain.Earnings when earned:
                return Rated(EarningsKey, Earnings(session, previousRate, LeastSession));

            case PersonaDomain.Combat when fought:
                return Rated(CombatKey, Combat(session, previousRate, LeastSession));

            case PersonaDomain.Exploration when explored:
                return Rated(ExplorationKey, Exploration(session, previousRate, LeastSession));

            case PersonaDomain.Repairs when repaired:
                return Repairs(session, LeastRepairSpend) is { } spend ? (RepairsKey, spend, null) : null;

            case PersonaDomain.Firsts when first:
                return Firsts(_firstStars.Count, _firstFootfalls, _firstMaps.Count, LeastFirsts) is { } firsts
                    ? (FirstsKey, firsts, null)
                    : null;

            default:
                return null;
        }
    }

    private static (string Key, string Text, long? Rate)? Rated(string key, (long Rate, string Text)? remark) =>
        remark is { } found ? (key, found.Text, found.Rate) : null;

    /// <summary>Counts the event if it is a first this session, and says whether it was.</summary>
    private bool CountFirst(JournalEvent journalEvent, CommanderGameState? state)
    {
        switch (journalEvent.Kind)
        {
            case "Scan":
                if (journalEvent.Long("SystemAddress") is { } system
                    && journalEvent.Int("BodyID") is { } body
                    && journalEvent.Raw.TryGetProperty("WasMapped", out var flag)
                    && flag.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    _unmapped.TryAdd((system, body), !flag.GetBoolean());
                }

                return DiscoveryCallout.IsUndiscoveredArrivalStar(journalEvent)
                    && journalEvent.Long("SystemAddress") is { } star
                    && _firstStars.Add(star);

            case "Disembark":
                if (journalEvent.Long("SystemAddress") is { } landed
                    && journalEvent.Int("BodyID") is { } ground
                    && state?.Scans.For(landed, ground) is { } scan
                    && scan.FootfallTakenAt == journalEvent.Timestamp)
                {
                    _firstFootfalls++;
                    return true;
                }

                return false;

            case "SAAScanComplete":
                return journalEvent.Long("SystemAddress") is { } mappedSystem
                    && journalEvent.Int("BodyID") is { } mappedBody
                    && _unmapped.TryGetValue((mappedSystem, mappedBody), out var wasUnmapped)
                    && wasUnmapped
                    && _firstMaps.Add((mappedSystem, mappedBody));

            default:
                return false;
        }
    }

    /// <summary>
    /// The line naming a live goal the Commander has not joined, or null when it is joined, complete, closed or
    /// has less than <paramref name="leastTime"/> left, or when neither its expiry nor its top-tier bonus is known.
    /// </summary>
    public static string? UnjoinedGoal(CommunityGoal goal, DateTimeOffset now, TimeSpan leastTime)
    {
        ArgumentNullException.ThrowIfNull(goal);

        var bonus = string.IsNullOrWhiteSpace(goal.TopTierBonus) ? null : goal.TopTierBonus.Trim();

        if (goal.IsParticipating
            || goal.IsComplete
            || !goal.IsLive(now)
            || (goal.Expiry is not { } expiry && bonus is null)
            || (goal.Expiry is { } soonest && soonest - now < leastTime))
        {
            return null;
        }

        var text = $"The {goal.Title} community goal";

        if (goal.Where is { } where)
        {
            text += $" at {where}";
        }

        if (goal.Expiry is { } ends)
        {
            var hours = (int)(ends - now).TotalHours;
            text += hours >= 48 ? $" has {hours / 24} days left." : $" has {hours} hours left.";
        }
        else
        {
            text += " is open.";
        }

        if (bonus is not null)
        {
            text += $" The top tier pays: {bonus}";
            text += bonus.EndsWith('.') ? "" : ".";
        }

        return text + " You have not joined it.";
    }

    /// <summary>
    /// The earnings remark for this session, or null while it is younger than <paramref name="leastSession"/>
    /// or has earned nothing.
    /// </summary>
    public static (long Rate, string Text)? Earnings(SessionSummary session, long? previousRate, TimeSpan leastSession)
    {
        ArgumentNullException.ThrowIfNull(session);

        (long Amount, string Name)[] sources =
        [
            (session.TradeEarnings, "trade"),
            (session.MissionEarnings, "missions"),
            (session.ExplorationEarnings, "exploration data"),
            (session.BountyEarnings, "bounties"),
            (session.CombatBondEarnings, "combat bonds"),
            (session.VoucherEarnings, "vouchers"),
        ];

        return Rate(session, session.TotalEarnings, sources, previousRate, leastSession, "this session");
    }

    /// <summary>
    /// The combat earnings remark for this session, from bounties, combat bonds and vouchers, or null while it
    /// is younger than <paramref name="leastSession"/> or has earned nothing in combat.
    /// </summary>
    public static (long Rate, string Text)? Combat(SessionSummary session, long? previousRate, TimeSpan leastSession)
    {
        ArgumentNullException.ThrowIfNull(session);

        (long Amount, string Name)[] sources =
        [
            (session.BountyEarnings, "bounties"),
            (session.CombatBondEarnings, "combat bonds"),
            (session.VoucherEarnings, "vouchers"),
        ];

        return Rate(session, sources.Sum(source => source.Amount), sources, previousRate, leastSession, "in combat this session");
    }

    /// <summary>
    /// The exploration data remark for this session, or null while it is younger than <paramref name="leastSession"/>
    /// or has earned nothing from exploration data.
    /// </summary>
    public static (long Rate, string Text)? Exploration(SessionSummary session, long? previousRate, TimeSpan leastSession)
    {
        ArgumentNullException.ThrowIfNull(session);

        return Rate(session, session.ExplorationEarnings, [], previousRate, leastSession, "from exploration data this session");
    }

    /// <summary>The repair spend remark, or null while the session has spent less than <paramref name="leastSpend"/>.</summary>
    public static string? Repairs(SessionSummary session, long leastSpend)
    {
        ArgumentNullException.ThrowIfNull(session);

        return session.RepairCosts <= 0 || session.RepairCosts < leastSpend
            ? null
            : $"{SpokenCredits.Band(session.RepairCosts)} credits on repairs this session.";
    }

    /// <summary>The count of firsts by kind, or null while there are fewer than <paramref name="least"/>.</summary>
    public static string? Firsts(int stars, int footfalls, int maps, int least)
    {
        var total = stars + footfalls + maps;

        if (total <= 0 || total < least)
        {
            return null;
        }

        var kinds = new List<string>();

        if (stars > 0)
        {
            kinds.Add(stars == 1 ? "1 undiscovered star" : $"{stars} undiscovered stars");
        }

        if (footfalls > 0)
        {
            kinds.Add(footfalls == 1 ? "1 first footfall" : $"{footfalls} first footfalls");
        }

        if (maps > 0)
        {
            kinds.Add(maps == 1 ? "1 body mapped first" : $"{maps} bodies mapped first");
        }

        return $"{total} firsts this session: {string.Join(", ", kinds)}.";
    }

    private static (long Rate, string Text)? Rate(
        SessionSummary session,
        long total,
        (long Amount, string Name)[] sources,
        long? previousRate,
        TimeSpan leastSession,
        string scope)
    {
        if (session.Elapsed is not { } elapsed || elapsed < leastSession || total <= 0)
        {
            return null;
        }

        var rate = (long)Math.Round(total / elapsed.TotalHours, MidpointRounding.AwayFromZero);

        var text = $"{SpokenCredits.Band(rate)} credits an hour {scope}";

        text += sources.Length > 0 ? $", the largest share from {sources.MaxBy(source => source.Amount).Name}." : ".";

        if (previousRate is { } previous)
        {
            text += $" At the last count it was {SpokenCredits.Band(previous)} an hour.";
        }

        return (rate, text);
    }
}
