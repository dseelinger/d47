using D47.Core.Journal;
using D47.Core.Persona;

namespace D47.Core.Callouts;

/// <summary>
/// A remark on the subject the core aboard pays attention to, carrying a figure from this session (#611). A
/// core with no domain says nothing.
/// </summary>
public sealed class DomainCallout : ICallout
{
    /// <summary>Every domain key starts with this; <see cref="RewordChance"/> always rewords them.</summary>
    public const string KeyPrefix = "domain.";

    public const string EarningsKey = KeyPrefix + "earnings";

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

    public string Id => "domain";

    /// <summary>The domain of the core aboard, read each tick.</summary>
    public Func<PersonaDomain> Domain { get; set; } = () => PersonaDomain.None;

    /// <summary>Off means no domain remark, whichever core is aboard.</summary>
    public Func<bool> Enabled { get; set; } = () => true;

    /// <summary>How old the session has to be, in journal time, before a rate is worth saying.</summary>
    public TimeSpan LeastSession { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>The shortest gap between two remarks.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    private DateTimeOffset? _lastAt;

    /// <summary>The rate said last this session, in credits an hour.</summary>
    private long? _lastRate;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var earned = false;

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind == "LoadGame")
            {
                _lastAt = null;
                _lastRate = null;
                earned = false;
            }
            else if (EarningEvents.Contains(journalEvent.Kind))
            {
                earned = true;
            }
        }

        if (!earned
            || context.IsPriming
            || !Enabled()
            || Domain() != PersonaDomain.Earnings
            || context.State?.Session is not { } session
            || (_lastAt is { } last && context.Now - last < Interval))
        {
            yield break;
        }

        if (Earnings(session, _lastRate, LeastSession) is not { } remark)
        {
            yield break;
        }

        _lastAt = context.Now;
        _lastRate = remark.Rate;

        yield return new Announcement(EarningsKey, remark.Text);
    }

    /// <summary>
    /// The earnings remark for this session, or null while it is younger than <paramref name="leastSession"/>
    /// or has earned nothing.
    /// </summary>
    public static (long Rate, string Text)? Earnings(SessionSummary session, long? previousRate, TimeSpan leastSession)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Elapsed is not { } elapsed || elapsed < leastSession || session.TotalEarnings <= 0)
        {
            return null;
        }

        var rate = (long)Math.Round(session.TotalEarnings / elapsed.TotalHours, MidpointRounding.AwayFromZero);

        (long Amount, string Name)[] sources =
        [
            (session.TradeEarnings, "trade"),
            (session.MissionEarnings, "missions"),
            (session.ExplorationEarnings, "exploration data"),
            (session.BountyEarnings, "bounties"),
            (session.CombatBondEarnings, "combat bonds"),
            (session.VoucherEarnings, "vouchers"),
        ];

        var largest = sources.MaxBy(source => source.Amount);

        var text = $"{SpokenCredits.Band(rate)} credits an hour this session, the largest share from {largest.Name}.";

        if (previousRate is { } previous)
        {
            text += $" At the last count it was {SpokenCredits.Band(previous)} an hour.";
        }

        return (rate, text);
    }
}
