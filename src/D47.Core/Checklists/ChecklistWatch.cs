using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Checklists;

/// <summary>The journal condition a watched line waits for.</summary>
public enum ChecklistWatchKind
{
    /// <summary><c>Docked</c> at a station, optionally in a named system.</summary>
    Dock,

    /// <summary><c>FSDJump</c> or <c>CarrierJump</c> into a system.</summary>
    Arrive,

    /// <summary><c>MarketBuy</c> of a commodity, optionally at a named station.</summary>
    Buy,

    /// <summary><c>MarketSell</c> of a commodity, optionally at a named station.</summary>
    Sell,

    /// <summary><c>CarrierTradeOrder</c> setting a buy order for a commodity.</summary>
    BuyOrder,
}

/// <summary>One journal condition on an authored line; the line ticks when it is met.</summary>
/// <param name="Subject">The station, system or commodity, compared after <see cref="ChecklistKeys.Compact"/>.</param>
public sealed record ChecklistWatch(ChecklistWatchKind Kind, string Subject)
{
    /// <summary>The system for <see cref="ChecklistWatchKind.Dock"/>, the station for a buy or a sale.</summary>
    public string? Place { get; init; }

    /// <summary>How many for a buy, a sale or a buy order.</summary>
    public int? Quantity { get; init; }

    /// <summary>When the watch was set; an earlier event never counts.</summary>
    public DateTimeOffset Since { get; init; }

    /// <summary>The running count for a buy or a sale.</summary>
    public int Progress { get; init; }

    /// <summary>The timestamp of the last event counted; only a later one counts.</summary>
    public DateTimeOffset? CountedTo { get; init; }

    /// <summary>The same watch with nothing counted, starting from <paramref name="now"/>.</summary>
    public ChecklistWatch Rearmed(DateTimeOffset now) => this with { Since = now, Progress = 0, CountedTo = null };
}

/// <summary>What one tick's events did to a watch.</summary>
/// <param name="Watch">The watch afterwards.</param>
/// <param name="Done">Whether the condition is now met.</param>
/// <param name="Happened">What the Commander did, as a clause — "you bought 5 Hydrogen Fuel".</param>
public sealed record ChecklistWatchOutcome(ChecklistWatch Watch, bool Done, string? Happened);

/// <summary>Matches journal events against a <see cref="ChecklistWatch"/>.</summary>
public static class ChecklistWatching
{
    /// <summary>
    /// Counts this batch's events against the watch, or returns null when none counted.
    /// </summary>
    /// <param name="here">Where the Commander is after the batch, for a market event no dock in the batch names.</param>
    public static ChecklistWatchOutcome? Count(
        ChecklistWatch watch,
        IEnumerable<JournalEvent> events,
        JournalLocation? here)
    {
        ArgumentNullException.ThrowIfNull(watch);
        ArgumentNullException.ThrowIfNull(events);

        var floor = watch.CountedTo;
        var stations = new Dictionary<long, string>();
        var progress = watch.Progress;
        DateTimeOffset? counted = null;
        string? happened = null;
        var done = false;

        foreach (var journalEvent in events)
        {
            if (journalEvent.Long("MarketID") is { } market
                && journalEvent.String("StationName") is { Length: > 0 } station
                && (journalEvent.Kind == "Docked" || journalEvent.Bool("Docked")))
            {
                stations[market] = station;
            }

            if (done
                || journalEvent.Timestamp < watch.Since
                || (floor is { } after && journalEvent.Timestamp <= after))
            {
                continue;
            }

            var adds = Adds(watch, journalEvent, StationOf);

            if (adds is not { } amount)
            {
                continue;
            }

            counted = journalEvent.Timestamp;

            switch (watch.Kind)
            {
                case ChecklistWatchKind.Buy or ChecklistWatchKind.Sell:
                    progress += amount;
                    done = progress >= (watch.Quantity ?? 1);
                    happened = Traded(watch, journalEvent, progress);
                    break;

                default:
                    done = true;
                    happened = Met(watch, journalEvent, amount);
                    break;
            }
        }

        return counted is { } latest
            ? new ChecklistWatchOutcome(watch with { Progress = progress, CountedTo = latest }, done, happened)
            : null;

        string? StationOf(JournalEvent marketEvent)
        {
            var id = marketEvent.Long("MarketID");

            if (id is { } known && stations.TryGetValue(known, out var named))
            {
                return named;
            }

            return here is { Docked: true } docked && (id is null || docked.MarketId == id)
                ? docked.StationName
                : null;
        }
    }

    /// <summary>
    /// What one event adds: the count for a buy or a sale, the order size for a buy order, 1 for a dock
    /// or an arrival; null when it does not match.
    /// </summary>
    private static int? Adds(ChecklistWatch watch, JournalEvent journalEvent, Func<JournalEvent, string?> stationOf)
    {
        switch (watch.Kind, journalEvent.Kind)
        {
            case (ChecklistWatchKind.Dock, "Docked"):
                return Same(journalEvent.String("StationName"), watch.Subject)
                       && (watch.Place is null || Same(journalEvent.String("StarSystem"), watch.Place))
                    ? 1
                    : null;

            case (ChecklistWatchKind.Arrive, "FSDJump" or "CarrierJump"):
                return Same(journalEvent.String("StarSystem"), watch.Subject) ? 1 : null;

            case (ChecklistWatchKind.Buy, "MarketBuy") or (ChecklistWatchKind.Sell, "MarketSell"):
                return (Same(journalEvent.String("Type"), watch.Subject)
                        || Same(journalEvent.String("Type_Localised"), watch.Subject))
                       && (watch.Place is null || Same(stationOf(journalEvent), watch.Place))
                       && journalEvent.Int("Count") is { } count and > 0
                    ? count
                    : null;

            case (ChecklistWatchKind.BuyOrder, "CarrierTradeOrder"):
                return (Same(journalEvent.String("Commodity"), watch.Subject)
                        || Same(journalEvent.String("Commodity_Localised"), watch.Subject))
                       && journalEvent.Int("PurchaseOrder") is { } order
                       && order >= (watch.Quantity ?? 1)
                    ? order
                    : null;

            default:
                return null;
        }
    }

    /// <summary>The condition as the Commander reads it — "Ticks when you buy 5 Hydrogen Fuel at Jameson Memorial".</summary>
    public static string Describe(ChecklistWatch watch, bool open = true)
    {
        ArgumentNullException.ThrowIfNull(watch);

        var what = watch.Quantity is { } quantity
            ? $"{quantity.ToString(CultureInfo.InvariantCulture)} {watch.Subject}"
            : watch.Subject;

        var said = watch.Kind switch
        {
            ChecklistWatchKind.Dock => $"Ticks when you dock at {watch.Subject}{In(watch.Place)}",
            ChecklistWatchKind.Arrive => $"Ticks when you arrive in {watch.Subject}",
            ChecklistWatchKind.Buy => $"Ticks when you buy {what}{At(watch.Place)}",
            ChecklistWatchKind.Sell => $"Ticks when you sell {what}{At(watch.Place)}",
            _ => $"Ticks when your carrier has a buy order for {what}",
        };

        return open && watch.Progress > 0 && watch.Quantity is { } wanted
               && watch.Kind is ChecklistWatchKind.Buy or ChecklistWatchKind.Sell
            ? $"{said} · {watch.Progress.ToString(CultureInfo.InvariantCulture)} of "
              + $"{wanted.ToString(CultureInfo.InvariantCulture)} so far"
            : said;

        static string In(string? place) => place is { Length: > 0 } ? $" in {place}" : string.Empty;

        static string At(string? place) => place is { Length: > 0 } ? $" at {place}" : string.Empty;
    }

    private static bool Same(string? journal, string? wanted) =>
        ChecklistKeys.Compact(journal) is { Length: > 0 } compact
        && string.Equals(compact, ChecklistKeys.Compact(wanted), StringComparison.Ordinal);

    private static string Traded(ChecklistWatch watch, JournalEvent journalEvent, int total)
    {
        var verb = watch.Kind == ChecklistWatchKind.Buy ? "bought" : "sold";

        return $"you {verb} {total.ToString(CultureInfo.InvariantCulture)} "
               + $"{journalEvent.String("Type_Localised") ?? watch.Subject}";
    }

    private static string Met(ChecklistWatch watch, JournalEvent journalEvent, int amount) => watch.Kind switch
    {
        ChecklistWatchKind.Dock => $"you docked at {journalEvent.String("StationName") ?? watch.Subject}",
        ChecklistWatchKind.Arrive => $"you arrived in {journalEvent.String("StarSystem") ?? watch.Subject}",
        _ => $"your carrier has a buy order for {amount.ToString(CultureInfo.InvariantCulture)} "
             + $"{journalEvent.String("Commodity_Localised") ?? watch.Subject}",
    };
}
