using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary>
/// <see cref="ITradePlanService"/> — the markets fetched, the local ones preferred, and <see
/// cref="TradePlanner"/> run over both (Phase 36).
/// </summary>
public sealed class SpanshTradePlanService : ITradePlanService, IDisposable
{
    /// <summary>The same host the searches and the plotters reach, named here too so grepping finds it.</summary>
    public const string Host = SpanshGalaxyService.Host;

    /// <summary>How many stations one request returns.</summary>
    public const int PageSize = 50;

    /// <summary>How many of those pages a plan is worth.</summary>
    public const int Pages = 3;

    private readonly HttpClient _http;

    private readonly ILogger<SpanshTradePlanService> _logger;

    private readonly MarketBook? _book;

    private readonly ArdentCommodityService _commodities;

    private readonly Func<DateTimeOffset> _now;

    private readonly bool _ownsClient;

    private readonly Lock _gate = new();

    /// <summary>
    /// <param name="commodities"> Where a named commodity's prices come from since #350 — a live
    /// EDDN-fed index rather than this one's station sweep.
    /// </summary>
    /// <param name="commodities">
    /// Where a named commodity's prices come from since #350 — a live EDDN-fed index rather than this
    /// one's station sweep.
    /// </param>
    /// <param name="book">
    /// The markets the Commander has stood in themselves, or null where none is composed.
    /// </param>
    public SpanshTradePlanService(
        ILogger<SpanshTradePlanService> logger,
        ArdentCommodityService commodities,
        MarketBook? book = null,
        HttpClient? http = null,
        Func<DateTimeOffset>? now = null)
    {
        _logger = logger;
        _commodities = commodities;
        _book = book;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _ownsClient = http is null;

        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(45) };

        _http.BaseAddress ??= new Uri($"https://{Host}/");

        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47/0.1 (+https://github.com/dseelinger/d47)");
        }
    }

    /// <summary>How long a gathered sweep stays good for.</summary>
    private static readonly TimeSpan CacheLife = TimeSpan.FromMinutes(15);

    /// <summary>One page's wait.</summary>
    private static readonly TimeSpan PageBudget = TimeSpan.FromSeconds(20);

    public async Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken)
    {
        var markets = await GatherAsync(query, cancellationToken).ConfigureAwait(false);

        return TradePlanner.Plan(query, markets);
    }

    /// <summary>
    /// Where to buy one commodity, or where to dump it (Phase 49), read from a live EDDN-fed index
    /// since #350.
    /// </summary>
    public async Task<CommodityAnswer> FindCommodityAsync(
        CommoditySearch search,
        CancellationToken cancellationToken)
    {
        var reply = await _commodities.FindAsync(search, cancellationToken).ConfigureAwait(false);

        var fetched = reply.Markets;

        var origin = fetched.FirstOrDefault(market =>
            string.Equals(market.System, search.System, StringComparison.OrdinalIgnoreCase)
            && (search.Station is null || market.IsSamePlaceAs(search.Station, search.System)));

        // Failing that, anything in the reference system at all: the coordinates are the system's rather than
        // the station's, and a question asked in light years does not need the pad.
        origin ??= fetched.FirstOrDefault(market =>
            string.Equals(market.System, search.System, StringComparison.OrdinalIgnoreCase));

        // And failing *that*, the coordinates the search itself resolved the reference to (#156).
        origin ??= Somewhere(search.System, reply.Reference);

        var complete = Reaches(reply);

        var oldest = _now() - TimeSpan.FromHours(search.MaxPriceAge);
        var usable = new List<MarketSnapshot>(fetched.Count);
        var stale = 0;

        // Absence from a whole reply is itself an answer (#350).
        foreach (var market in Merge(fetched, unfetched: !complete))
        {
            if (market.UpdatedAt is { } when && when < oldest)
            {
                stale++;
                continue;
            }

            usable.Add(Answerable(market, search.Query.Commodity));
        }

        return new CommodityAnswer(
            CommodityMarketSearch.Rank(search.Query, usable, origin),
            usable.Count + stale,
            stale,
            origin is not null)
        {
            Horizon = Covered(reply, search.Query.MaxDistance),
            Complete = complete,
        };
    }

    /// <summary>Whether the reply is evidence about the radius that was asked for (#350).</summary>
    private static bool Reaches(ArdentCommodityService.Reply reply) =>
        !reply.Truncated && reply.Markets.Count > 0 && reply.Reference is not null;

    /// <summary>
    /// How far the market index actually covered, in light years — or null where there is no honest
    /// number, which is either because it covered the whole radius asked for or because it cannot be
    /// known at all (#350).
    /// </summary>
    private static double? Covered(ArdentCommodityService.Reply reply, double asked)
    {
        if (!Reaches(reply) || reply.Reference is not { } origin)
        {
            return null;
        }

        var furthest = Furthest(reply.Markets, origin);

        return furthest < asked ? furthest : null;
    }

    /// <summary>How far the furthest of these markets sits from a point, in light years.</summary>
    private static double Furthest(
        IReadOnlyList<MarketSnapshot> markets,
        (double X, double Y, double Z) origin)
    {
        var from = new MarketSnapshot
        {
            Station = string.Empty,
            System = string.Empty,
            X = origin.X,
            Y = origin.Y,
            Z = origin.Z,
        };

        return markets.Max(from.DistanceTo);
    }

    /// <summary>The same market, reachable under the spelling the search was made with (#350).</summary>
    private static MarketSnapshot Answerable(MarketSnapshot market, string commodity)
    {
        if (market.Quote(commodity) is not null)
        {
            return market;
        }

        foreach (var (spelling, quote) in market.Quotes)
        {
            if (!ArdentCommodityService.SameCommodity(spelling, commodity))
            {
                continue;
            }

            return market with
            {
                Quotes = new Dictionary<string, MarketQuote>(market.Quotes, StringComparer.OrdinalIgnoreCase)
                {
                    [commodity] = quote,
                },
            };
        }

        return market;
    }

    /// <summary>
    /// A place with coordinates and no market: the reference system itself, as the search resolved it.
    /// </summary>
    private static MarketSnapshot? Somewhere(string system, (double X, double Y, double Z)? at) =>
        at is not { } found
            ? null
            : new MarketSnapshot
            {
                Station = system,
                System = system,
                X = found.X,
                Y = found.Y,
                Z = found.Z,
            };

    /// <summary>Where to buy everything one construction site still needs (Phase 50).</summary>
    public async Task<SourcingAnswer> SourceConstructionAsync(
        SourcingSearch search,
        CancellationToken cancellationToken)
    {
        if (search.Outstanding.Count == 0)
        {
            return SourcingAnswer.Empty;
        }

        // Unfiltered, and #156 leaves it that way on purpose: a build asks about twenty commodities at once,
        // so there is no single one to narrow the sweep by.
        var fetched = (await SweepAsync(search.System, search.MaxDistance, cancellationToken)
            .ConfigureAwait(false)).Markets;

        var origin = fetched.FirstOrDefault(market =>
            string.Equals(market.System, search.System, StringComparison.OrdinalIgnoreCase)
            && (search.Station is null || market.IsSamePlaceAs(search.Station, search.System)));

        origin ??= fetched.FirstOrDefault(market =>
            string.Equals(market.System, search.System, StringComparison.OrdinalIgnoreCase));

        var oldest = _now() - TimeSpan.FromHours(search.MaxPriceAge);
        var usable = new List<MarketSnapshot>(fetched.Count);
        var stale = 0;

        // A sweep, so a station it missed is one it did not look at: three pages of fifty is not the whole
        // radius, and the book still fills the gap here (#350).
        foreach (var market in Merge(fetched, unfetched: true))
        {
            if (market.UpdatedAt is { } when && when < oldest)
            {
                stale++;
                continue;
            }

            usable.Add(market);
        }

        return new SourcingAnswer(
            ColonisationSourcing.Plan(
                search.Outstanding,
                usable,
                origin,
                search.LargePadOnly,
                search.MaxStops),
            usable.Count + stale,
            stale,
            origin is not null);
    }

    /// <summary>The sweep with the Commander's own markets folded in, newer wins.</summary>
    /// <param name="unfetched">
    /// Whether a remembered station the fetch never returned may be added to it (#350).
    /// </param>
    private IEnumerable<MarketSnapshot> Merge(IReadOnlyList<MarketSnapshot> fetched, bool unfetched)
    {
        if (_book is null)
        {
            return fetched;
        }

        var merged = new List<MarketSnapshot>(fetched.Count);

        foreach (var market in fetched)
        {
            var seen = _book.Markets.FirstOrDefault(local => local.IsSamePlaceAs(market.Station, market.System));

            merged.Add(seen is not null && seen.UpdatedAt > market.UpdatedAt
                ? seen with { X = market.X, Y = market.Y, Z = market.Z, DistanceToArrival = market.DistanceToArrival }
                : market);
        }

        if (!unfetched)
        {
            return merged;
        }

        // One the sweep never returned: the outpost under their feet, most often, since three pages of fifty
        // is not the whole radius.
        foreach (var seen in _book.Markets)
        {
            if (!merged.Any(market => market.IsSamePlaceAs(seen.Station, seen.System)))
            {
                merged.Add(seen);
            }
        }

        return merged;
    }

    /// <summary>
    /// Everything worth planning over: the sweep, the Commander's own markets folded in, and anything
    /// too old to trust dropped.
    /// </summary>
    private async Task<IReadOnlyList<MarketSnapshot>> GatherAsync(
        TradeQuery query,
        CancellationToken cancellationToken)
    {
        var radius = query.MaxHopDistance;

        // Unfiltered, and unchanged by #156: trade planning ranks every commodity at every market against
        // every other, so there is nothing to filter by.
        var fetched = (await SweepAsync(query.System, radius, cancellationToken).ConfigureAwait(false)).Markets;

        var oldest = _now() - TimeSpan.FromHours(query.MaxPriceAge);
        var merged = new List<MarketSnapshot>(fetched.Count);
        var mine = new List<MarketSnapshot>();

        if (_book is not null)
        {
            foreach (var seen in _book.Markets)
            {
                // A remembered market is only a candidate if it is somewhere this plan could reach.
                var distance = StationDistance(seen, query, fetched);

                if (distance is null || distance > radius)
                {
                    continue;
                }

                mine.Add(seen);
            }
        }

        foreach (var market in fetched)
        {
            var seen = mine.FirstOrDefault(local => local.IsSamePlaceAs(market.Station, market.System));

            // The newer of the two, every time.
            var chosen = seen is not null && seen.UpdatedAt > market.UpdatedAt
                ? seen with { X = market.X, Y = market.Y, Z = market.Z, DistanceToArrival = market.DistanceToArrival }
                : market;

            if (chosen.UpdatedAt is { } when && when < oldest)
            {
                continue;
            }

            merged.Add(chosen);
        }

        // A market of the Commander's own that the sweep did not return at all — the outpost they are docked
        // at right now, most often, since a page of fifty is not the whole radius.
        foreach (var seen in mine)
        {
            if (merged.Any(market => market.IsSamePlaceAs(seen.Station, seen.System)))
            {
                continue;
            }

            merged.Add(seen);
        }

        return merged;
    }

    /// <summary>Where a remembered market sits relative to this plan's origin.</summary>
    private static double? StationDistance(
        MarketSnapshot seen,
        TradeQuery query,
        IReadOnlyList<MarketSnapshot> fetched)
    {
        if (seen.IsSamePlaceAs(query.Station, query.System))
        {
            return 0;
        }

        var origin = fetched.FirstOrDefault(market => market.IsSamePlaceAs(query.Station, query.System));

        return origin is null ? null : origin.DistanceTo(seen);
    }

    /// <summary>
    /// One sweep, and the two things a caller needs to know about it besides its markets (#156).
    /// </summary>
    /// <param name="Reference">
    /// The coordinates the search resolved the reference system to, straight out of the response.
    /// </param>
    /// <param name="Exhausted">
    /// Whether the sweep ran out of galaxy rather than out of budget: a short page ended it, so
    /// everything inside the radius was examined and "nothing within N light years" is a claim about N
    /// light years.
    /// </param>
    private sealed record Sweep(
        IReadOnlyList<MarketSnapshot> Markets,
        (double X, double Y, double Z)? Reference,
        bool Exhausted)
    {
        public static readonly Sweep Nothing = new([], null, true);

        /// <summary>
        /// How far the sweep actually reached, in light years, or null when it reached the whole radius
        /// it was asked for.
        /// </summary>
        public double? Horizon =>
            Exhausted || Markets.Count == 0 || Reference is not { } origin
                ? null
                : Furthest(Markets, origin);
    }

    private sealed record Cached(
        string System,
        double Radius,
        string? Commodity,
        bool Selling,
        int Minimum,
        bool IncludeCarriers,
        bool SurfaceStations,
        double? MaxStationDistance,
        bool LargePad,
        DateTimeOffset At,
        Sweep Sweep);

    /// <summary>How many sweeps are remembered at once.</summary>
    private const int CacheSlots = 8;

    private readonly List<Cached> _sweeps = [];

    /// <summary>The markets to plan over, and the two facts about how they were gathered (#156).</summary>
    /// <param name="commodity">Nothing passes one any more.</param>
    private async Task<Sweep> SweepAsync(
        string system,
        double radius,
        CancellationToken cancellationToken,
        string? commodity = null,
        bool selling = false,
        int minimum = 1,
        bool includeCarriers = false,
        bool surfaceStations = false,
        double? maxStationDistance = null,
        bool largePad = false)
    {
        // Every knob that reaches the request server-side (#308): each one changes which 150 stations came
        // back, so two sweeps are the same search only when all of them agree.
        bool SameSearch(Cached entry) =>
            string.Equals(entry.System, system, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.Commodity, commodity, StringComparison.OrdinalIgnoreCase)
            && entry.Selling == selling
            && entry.Minimum == minimum
            && entry.IncludeCarriers == includeCarriers
            && entry.SurfaceStations == surfaceStations
            && entry.MaxStationDistance == maxStationDistance
            && entry.LargePad == largePad;

        lock (_gate)
        {
            // A wider cached sweep answers a narrower question: the planner measures every leg itself, so
            // extra stations cost arithmetic rather than correctness. Only within the same filter,
            // though. An unfiltered sweep is the nearest 150 markets and a filtered one is the nearest
            // 150 that stock a named commodity; neither is a superset of the other, and answering one
            // question with the other's cache is #156 arriving through the back door.
            var cached = _sweeps.Find(entry =>
                SameSearch(entry) && entry.Radius >= radius && _now() - entry.At < CacheLife);

            if (cached is not null)
            {
                _logger.LogDebug(
                    "Re-using {Count} cached markets near {System}", cached.Sweep.Markets.Count, system);

                return cached.Sweep;
            }
        }

        var markets = new List<MarketSnapshot>();
        (double X, double Y, double Z)? reference = null;
        var exhausted = false;

        for (var page = 0; page < Pages; page++)
        {
            using var content = new StringContent(
                SpanshRequest.Markets(
                    system, radius, PageSize, page, commodity, selling, minimum,
                    includeCarriers, surfaceStations, maxStationDistance, largePad),
                Encoding.UTF8,
                "application/json");

            using var document = await SendAsync(
                token => _http.PostAsync("api/stations/search", content, token),
                cancellationToken).ConfigureAwait(false);

            if (document is null)
            {
                break;
            }

            reference ??= SpanshResponse.ReadReferenceCoordinates(document);

            var read = SpanshResponse.ReadMarkets(document);

            markets.AddRange(read);

            // A short page is the last page.
            if (read.Count < PageSize)
            {
                exhausted = true;
                break;
            }
        }

        var sweep = new Sweep(markets, reference, exhausted);

        lock (_gate)
        {
            _sweeps.RemoveAll(SameSearch);

            _sweeps.Add(new Cached(
                system, radius, commodity, selling, minimum,
                includeCarriers, surfaceStations, maxStationDistance, largePad,
                _now(), sweep));

            if (_sweeps.Count > CacheSlots)
            {
                _sweeps.RemoveAt(0);
            }
        }

        return sweep;
    }

    /// <summary>
    /// One page, with every way it can go wrong turned into a sentence — the same distinction the
    /// galaxy search draws between "not right now" and "not ever with this question".
    /// </summary>
    private async Task<JsonDocument?> SendAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(PageBudget);

        HttpResponseMessage response;

        try
        {
            response = await send(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("The market search did not answer within the timeout");
            throw new GalaxyUnavailableException("The market search took too long to answer.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "The market search could not be reached");
            throw new GalaxyUnavailableException(
                "I couldn't reach the market search — check the network connection.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("The market search answered {Status}", (int)response.StatusCode);

                throw new GalaxyUnavailableException(response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests =>
                        "The market search is rate limiting me. It should clear shortly.",
                    >= HttpStatusCode.InternalServerError =>
                        "The market search reported a server error. It should clear shortly.",
                    _ => "The market search refused that request.",
                });
            }

            try
            {
                var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);

                return await JsonDocument.ParseAsync(stream, cancellationToken: deadline.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("The market search did not finish answering within the timeout");
                throw new GalaxyUnavailableException("The market search took too long to answer.");
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "The market search answered with something that was not JSON");
                throw new GalaxyUnavailableException(
                    "The market search answered with something I couldn't read.");
            }
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }
}
