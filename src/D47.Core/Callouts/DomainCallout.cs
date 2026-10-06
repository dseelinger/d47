using D47.Core.Journal;
using D47.Core.Persona;

namespace D47.Core.Callouts;

/// <summary>
/// A remark on the subject the core aboard pays attention to, carrying a figure from this session (#611, #613),
/// or naming a community goal not yet joined (#612). A core with no domain says nothing.
/// </summary>
public sealed class DomainCallout : ICallout
{
    /// <summary>Every domain key starts with this; <see cref="RewordChance"/> always rewords them.</summary>
    public const string KeyPrefix = "domain.";

    public const string EarningsKey = KeyPrefix + "earnings";

    public const string CombatKey = KeyPrefix + "combat";

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

    private DateTimeOffset? _lastAt;

    private readonly HashSet<int> _namedGoals = [];

    /// <summary>The rate said last this session, in credits an hour.</summary>
    private long? _lastRate;

    private PersonaDomain _lastRateDomain;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var earned = false;
        var fought = false;

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind == "LoadGame")
            {
                _lastAt = null;
                _lastRate = null;
                _namedGoals.Clear();
                earned = false;
                fought = false;
            }
            else
            {
                earned |= EarningEvents.Contains(journalEvent.Kind);
                fought |= CombatEvents.Contains(journalEvent.Kind);
            }
        }

        var domain = Domain();

        if (context.IsPriming || !Enabled() || domain == PersonaDomain.None)
        {
            yield break;
        }

        var previousRate = _lastRateDomain == domain ? _lastRate : null;

        if ((domain == PersonaDomain.Combat ? fought : earned)
            && context.State?.Session is { } session
            && !(_lastAt is { } last && context.Now - last < Interval)
            && (domain == PersonaDomain.Combat
                ? Combat(session, previousRate, LeastSession)
                : Earnings(session, previousRate, LeastSession)) is { } remark)
        {
            _lastAt = context.Now;
            _lastRate = remark.Rate;
            _lastRateDomain = domain;

            yield return new Announcement(domain == PersonaDomain.Combat ? CombatKey : EarningsKey, remark.Text);
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

        var largest = sources.MaxBy(source => source.Amount);

        var text = $"{SpokenCredits.Band(rate)} credits an hour {scope}, the largest share from {largest.Name}.";

        if (previousRate is { } previous)
        {
            text += $" At the last count it was {SpokenCredits.Band(previous)} an hour.";
        }

        return (rate, text);
    }
}
