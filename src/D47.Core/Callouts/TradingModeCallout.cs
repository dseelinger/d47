using System.Collections.Concurrent;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Core.Callouts;

/// <summary>
/// Docked with a route plotted: what to buy here for the system at the end of it (#312).
/// </summary>
public sealed class TradingModeCallout(ILogger<TradingModeCallout> logger) : ICallout
{
    private readonly ConcurrentQueue<Found> _found = new();

    /// <summary>Pairs already spoken for, so each is said once. Tick thread only.</summary>
    private readonly HashSet<(string Station, string Destination)> _answered =
        new(StationDestinationComparer.Instance);

    /// <summary>
    /// Pairs with a lookup out. Tick thread only: an entry is cleared when the result reaches the queue,
    /// which is the tick thread, so a lookup that failed is asked again rather than suppressed for the
    /// rest of the session.
    /// </summary>
    private readonly HashSet<(string Station, string Destination)> _pending =
        new(StationDestinationComparer.Instance);

    /// <summary>
    /// Whether a route has been plotted or a market opened since the last lookup. Tick thread only. The
    /// event and the file it points at are polled separately, so the two can land a tick apart; this
    /// waits for both rather than firing on whichever arrives first.
    /// </summary>
    private bool _armed;

    public string Id => "trading-mode";

    /// <summary>The service to ask, or null when galaxy search is off.</summary>
    public Func<ITradePlanService?> Trade { get; set; } = () => null;

    /// <summary>The least free hold, in tonnes, that is worth a lookup.</summary>
    public Func<int> MinHold { get; set; } = () => DefaultMinHold;

    /// <summary>The saved trade filters the destination sweep is narrowed by.</summary>
    public Func<BestCargoSearch, BestCargoSearch> Filters { get; set; } = search => search;

    /// <summary>Starts the lookup off the tick thread; must return without waiting for it.</summary>
    public Action<Func<Task>> Dispatch { get; set; } = work => _ = Task.Run(work);

    /// <summary>The default for <see cref="MinHold"/>, in tonnes.</summary>
    public const int DefaultMinHold = 25;

    /// <summary>The smallest and largest <see cref="MinHold"/> the setting accepts.</summary>
    public const int LeastMinHold = 1;

    public const int MostMinHold = 50;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        while (_found.TryDequeue(out var found))
        {
            var finished = Pair(found.Search);

            _pending.Remove(finished);

            if (found.Answer is null || !Still(context, found))
            {
                continue;
            }

            _answered.Add(finished);

            yield return Announce(found.Search.Destination, found.Answer);
        }

        if (context.IsPriming)
        {
            yield break;
        }

        _armed |= context.Events.Any(journalEvent => journalEvent.Kind is "NavRoute" or "Market");

        if (!_armed
            || Where(context) is not { } here
            || Destination(context.Route, here.System) is not { } plotted
            || Hold(context.State) is var hold && hold < Math.Max(LeastMinHold, MinHold())
            || Trade() is not { } trade)
        {
            yield break;
        }

        _armed = false;

        var pair = (here.Station, plotted);

        if (_answered.Contains(pair) || !_pending.Add(pair))
        {
            yield break;
        }

        var search = Filters(new BestCargoSearch
        {
            System = here.System,
            Station = here.Station,
            Destination = plotted,
            Hold = hold,
        });

        Dispatch(() => LookUp(trade, search));
    }

    /// <summary>Where the Commander is docked, or null when they are not.</summary>
    private static (string System, string Station)? Where(CalloutContext context) =>
        context.State?.Location is { Docked: true, StarSystem: { Length: > 0 } system, StationName: { Length: > 0 } station }
            ? (system, station)
            : null;

    /// <summary>The last system on the route, or null when it ends where the Commander already is.</summary>
    private static string? Destination(NavRoute route, string here)
    {
        if (route.Hops.Count == 0)
        {
            return null;
        }

        var last = route.Hops[^1].StarSystem;

        return string.Equals(last, here, StringComparison.OrdinalIgnoreCase) ? null : last;
    }

    /// <summary>The hold a trade would fill: the cargo capacity, less the limpets aboard.</summary>
    private static int Hold(CommanderGameState? state) =>
        state?.Ship.CargoCapacity is { } capacity
            ? Math.Max(0, capacity - state.Hold.Of(LimpetCallout.Limpet))
            : 0;

    /// <summary>Whether the Commander is still where the answer was asked for.</summary>
    private static bool Still(CalloutContext context, Found found) =>
        Where(context) is { } here
        && string.Equals(here.Station, found.Search.Station, StringComparison.OrdinalIgnoreCase)
        && string.Equals(here.System, found.Search.System, StringComparison.OrdinalIgnoreCase)
        && string.Equals(
            Destination(context.Route, here.System), found.Search.Destination, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Always queues a result, an unanswerable lookup included, because the queue is what clears the
    /// pair from <see cref="_pending"/>.
    /// </summary>
    private async Task LookUp(ITradePlanService trade, BestCargoSearch search)
    {
        BestCargoAnswer? answer = null;

        try
        {
            answer = await trade.BestCargoAsync(search, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "Could not work out the best cargo for {Destination}", search.Destination);
        }

        _found.Enqueue(new Found(search, answer));
    }

    private static Announcement Announce(string destination, BestCargoAnswer answer) =>
        new($"trading-mode.{destination}", BestCargo.Describe(destination, answer));

    private static (string Station, string Destination) Pair(BestCargoSearch search) =>
        (search.Station, search.Destination);

    /// <summary>A finished lookup. A null <paramref name="Answer"/> is one that could not be answered.</summary>
    private readonly record struct Found(BestCargoSearch Search, BestCargoAnswer? Answer);

    /// <summary>Station and destination names compare the way the journal spells them: case aside.</summary>
    private sealed class StationDestinationComparer : IEqualityComparer<(string Station, string Destination)>
    {
        public static readonly StationDestinationComparer Instance = new();

        public bool Equals((string Station, string Destination) x, (string Station, string Destination) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Station, y.Station)
            && StringComparer.OrdinalIgnoreCase.Equals(x.Destination, y.Destination);

        public int GetHashCode((string Station, string Destination) value) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Station),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Destination));
    }
}
