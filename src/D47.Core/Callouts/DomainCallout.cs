using System.Globalization;
using D47.Core.Journal;
using D47.Core.Persona;
using CorePersona = D47.Core.Persona.Persona;

namespace D47.Core.Callouts;

/// <summary>
/// The session's rate in whatever the core aboard acts in — credits an hour, the largest source, and the
/// rate last said — once the session is half an hour old, and at most once an hour.
/// </summary>
/// <param name="core">The core aboard, read on the tick rather than captured once.</param>
public sealed class DomainCallout(Func<CorePersona> core) : ICallout
{
    public string Id => "domain";

    /// <summary>Every domain key starts with this.</summary>
    public const string KeyPrefix = "domain.";

    /// <summary>How long the session must have run before the rate is worth saying.</summary>
    public static readonly TimeSpan LeastSession = TimeSpan.FromMinutes(30);

    /// <summary>The least time between two remarks.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private long? _lastRate;
    private DateTimeOffset? _lastSaidAt;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind == "LoadGame")
            {
                // A fresh session wipes the previous remark.
                _lastRate = null;
                _lastSaidAt = null;
            }
        }

        if (context.IsPriming)
        {
            yield break;
        }

        var domain = core().Domain;

        if (domain == PersonaDomain.None || !context.Events.Any(journalEvent => Earns(domain, journalEvent)))
        {
            yield break;
        }

        if (context.State?.Session is not { } session
            || !session.IsKnown
            || session.Elapsed is not { } elapsed
            || elapsed < LeastSession)
        {
            yield break;
        }

        var (earned, source) = Earnings(domain, session);

        if (earned <= 0)
        {
            yield break;
        }

        if (_lastSaidAt is { } said && context.Now - said < Interval)
        {
            yield break;
        }

        var rate = (long)(earned / elapsed.TotalHours);

        // The callout owns the once-an-hour spacing, as KillCallout owns its own: the engine's key cooldown
        // survives a LoadGame, so leaning on it could swallow a remark after a relog while this one has
        // already stamped the rate as said.
        _lastSaidAt = context.Now;
        yield return new Announcement($"{KeyPrefix}{domain.ToString().ToLowerInvariant()}", Text(rate, source));

        _lastRate = rate;
    }

    private string Text(long rate, string source)
    {
        var said = $"{Number(rate)} credits an hour this session. Most of it from {source}.";

        return _lastRate is { } previous ? $"{said} Last time I said {Number(previous)}." : said;
    }

    /// <summary>Whether this event earns in the domain.</summary>
    private static bool Earns(PersonaDomain domain, JournalEvent journalEvent) => domain switch
    {
        PersonaDomain.Combat => journalEvent.Kind is "Bounty" or "FactionKillBond" or "RedeemVoucher",
        PersonaDomain.Earnings => journalEvent.Kind is "MarketSell" or "MissionCompleted" or "RedeemVoucher"
            or "SellExplorationData" or "MultiSellExplorationData" or "SellOrganicData",
        _ => false,
    };

    /// <summary>The domain's earnings and the source that contributed most of them.</summary>
    private static (long Earned, string Source) Earnings(PersonaDomain domain, SessionSummary session) =>
        domain switch
        {
            PersonaDomain.Combat => Largest(
                (session.BountyEarnings, "bounties"),
                (session.CombatBondEarnings, "combat bonds"),
                (session.VoucherEarnings, "vouchers")),
            PersonaDomain.Earnings => Largest(
                (session.TradeEarnings, "trade"),
                (session.MissionEarnings, "missions"),
                (session.ExplorationEarnings, "exploration data"),
                (session.VoucherEarnings, "vouchers")),
            _ => (0, string.Empty),
        };

    private static (long Earned, string Source) Largest(params (long Value, string Source)[] sources)
    {
        var total = 0L;
        var best = sources[0];

        foreach (var source in sources)
        {
            total += source.Value;

            if (source.Value > best.Value)
            {
                best = source;
            }
        }

        return (total, best.Source);
    }

    private static string Number(long credits) => credits.ToString("N0", CultureInfo.InvariantCulture);
}
