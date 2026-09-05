using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary>
/// <see cref="ITradePlanService"/> — the markets fetched, the local ones preferred, and
/// <see cref="TradePlanner"/> run over both (Phase 36).
/// <para>
/// <b>Spansh is the lookup and d47 is the planner.</b> The station search already returns every
/// result's whole <c>market</c> array, so the routing is d47's own arithmetic over data it was
/// already entitled to ask for — not a galaxy dump, and not somebody else's planner.
/// </para>
/// <para>
/// <b>Two things the design starts from rather than discovers</b>, both measured on 2026-08-19. A
/// station search answers in 1.1 to 1.3 seconds whatever it returns, so the bill is the number of
/// requests and hardly the size of them: <i>ask for large pages</i>. And <c>market_updated_at</c>
/// says exactly how stale a price is while a market does not move between two plans made a minute
/// apart: <i>cache them</i>. A planner that re-fetches what it already holds is the only way this
/// gets slow.
/// </para>
/// </summary>
public sealed class SpanshTradePlanService : ITradePlanService, IDisposable
{
    /// <summary>The same host the searches and the plotters reach, named here too so grepping finds it.</summary>
    public const string Host = SpanshGalaxyService.Host;

    /// <summary>
    /// How many stations one request returns. The service's own page ceiling, and the phase's
    /// first consequence: four times the stations cost a fifth more time.
    /// </summary>
    public const int PageSize = 50;

    /// <summary>
    /// How many of those pages a plan is worth. Three is 150 candidate markets, which is the shape
    /// the search timing was measured against, and about four seconds of the Commander's wait.
    /// </summary>
    public const int Pages = 3;

    private readonly HttpClient _http;

    private readonly ILogger<SpanshTradePlanService> _logger;

    private readonly MarketBook? _book;

    private readonly Func<DateTimeOffset> _now;

    private readonly bool _ownsClient;

    private readonly Lock _gate = new();


    /// <param name="book">
    /// The markets the Commander has stood in themselves, or null where none is composed. Their
    /// prices win over a report of the same station when they are newer, which they usually are
    /// for the station under their feet and usually are not for one they saw last month.
    /// </param>
    public SpanshTradePlanService(
        ILogger<SpanshTradePlanService> logger,
        MarketBook? book = null,
        HttpClient? http = null,
        Func<DateTimeOffset>? now = null)
    {
        _logger = logger;
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

    /// <summary>
    /// How long a gathered sweep stays good for. Long enough that asking twice about the same
    /// evening's trading costs one pull, short enough that a Commander who waited out a session
    /// gets fresh prices. <c>market_updated_at</c> still decides whether a price is usable at all;
    /// this only decides whether to ask again.
    /// </summary>
    private static readonly TimeSpan CacheLife = TimeSpan.FromMinutes(15);

    /// <summary>
    /// One page's wait. Generous against the 1.1 to 1.3 seconds measured, because the pages are
    /// the largest bodies d47 pulls from anywhere and a page lost to a timeout costs the plan.
    /// </summary>
    private static readonly TimeSpan PageBudget = TimeSpan.FromSeconds(20);

    public async Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken)
    {
        var markets = await GatherAsync(query, cancellationToken).ConfigureAwait(false);

        return TradePlanner.Plan(query, markets);
    }

    /// <summary>
    /// Where to buy one commodity, or where to dump it (Phase 49).
    /// <para>
    /// <b>Nothing new is fetched.</b> This is the planner's own sweep, its own cache and its own
    /// merge with the Commander's <c>MarketBook</c>, followed by one local pass. Spansh will not
    /// rank on a commodity's price server-side and its demand bounds are accepted and ignored, so
    /// local is the arrangement a "cheapest" answer needs rather than a workaround for one.
    /// </para>
    /// </summary>
    public async Task<CommodityAnswer> FindCommodityAsync(
        CommoditySearch search,
        CancellationToken cancellationToken)
    {
        var radius = search.Query.MaxDistance;

        // The commodity goes into the request (#156). This is the named-commodity path and the
        // only one that changes: the sweep's 150-station budget is spent on stations that stock
        // the thing rather than on whichever 150 happen to be nearest.
        var sweep = await SweepAsync(
                search.System,
                radius,
                cancellationToken,
                search.Query.Commodity,
                search.Query.Side == TradeSide.Selling,

                // The Commander's floor goes into the request too (#296), so the 150-station
                // budget is spent on stations that have enough rather than on ones that have any.
                search.Query.MinAvailable ?? 1)
            .ConfigureAwait(false);

        var fetched = sweep.Markets;

        var origin = fetched.FirstOrDefault(market =>
            string.Equals(market.System, search.System, StringComparison.OrdinalIgnoreCase)
            && (search.Station is null || market.IsSamePlaceAs(search.Station, search.System)));

        // Failing that, anything in the reference system at all: the coordinates are the system's
        // rather than the station's, and a question asked in light years does not need the pad.
        origin ??= fetched.FirstOrDefault(market =>
            string.Equals(market.System, search.System, StringComparison.OrdinalIgnoreCase));

        // And failing *that*, the coordinates the search itself resolved the reference to (#156).
        // A commodity-filtered sweep has no reason to return the origin's own stations — they need
        // not stock the thing — so without this the fix would have bought a right answer and paid
        // for it in every distance, silently, by falling back to ranking on price.
        origin ??= Somewhere(search.System, sweep.Reference);

        var oldest = _now() - TimeSpan.FromHours(search.MaxPriceAge);
        var usable = new List<MarketSnapshot>(fetched.Count);
        var stale = 0;

        foreach (var market in Merge(fetched))
        {
            if (market.UpdatedAt is { } when && when < oldest)
            {
                stale++;
                continue;
            }

            usable.Add(market);
        }

        return new CommodityAnswer(
            CommodityMarketSearch.Rank(search.Query, usable, origin),
            usable.Count + stale,
            stale,
            origin is not null)
        {
            Horizon = sweep.Horizon,
        };
    }

    /// <summary>
    /// A place with coordinates and no market: the reference system itself, as the search resolved
    /// it. Enough to measure from, which is all an origin is for.
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

    /// <summary>
    /// Where to buy everything one construction site still needs (Phase 50).
    /// <para>
    /// <b>Nothing new is fetched here either.</b> It is the planner's sweep, its cache and its
    /// <c>MarketBook</c> merge, and then <see cref="ColonisationSourcing"/> — which reads no clock
    /// and opens no socket — does the whole of the arithmetic. So a Commander who asks where to
    /// buy tritium and then asks what the whole build needs pays for one pull.
    /// </para>
    /// </summary>
    public async Task<SourcingAnswer> SourceConstructionAsync(
        SourcingSearch search,
        CancellationToken cancellationToken)
    {
        if (search.Outstanding.Count == 0)
        {
            return SourcingAnswer.Empty;
        }

        // Unfiltered, and #156 leaves it that way on purpose: a build asks about twenty
        // commodities at once, so there is no single one to narrow the sweep by.
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

        foreach (var market in Merge(fetched))
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

    /// <summary>
    /// The sweep with the Commander's own markets folded in, newer wins. Their <c>Market.json</c>
    /// is exact where a report is somebody's word, but only where it is also current — nothing
    /// here assumes their eyes are automatically better than this morning's report.
    /// </summary>
    private IEnumerable<MarketSnapshot> Merge(IReadOnlyList<MarketSnapshot> fetched)
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

        // One the sweep never returned: the outpost under their feet, most often, since three
        // pages of fifty is not the whole radius. It keeps no coordinates of its own, so it can
        // still be priced and simply has no measurable distance.
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
    /// Everything worth planning over: the sweep, the Commander's own markets folded in, and
    /// anything too old to trust dropped.
    /// </summary>
    private async Task<IReadOnlyList<MarketSnapshot>> GatherAsync(
        TradeQuery query,
        CancellationToken cancellationToken)
    {
        var radius = query.MaxHopDistance;

        // Unfiltered, and unchanged by #156: trade planning ranks every commodity at every
        // market against every other, so there is nothing to filter by.
        var fetched = (await SweepAsync(query.System, radius, cancellationToken).ConfigureAwait(false)).Markets;

        var oldest = _now() - TimeSpan.FromHours(query.MaxPriceAge);
        var merged = new List<MarketSnapshot>(fetched.Count);
        var mine = new List<MarketSnapshot>();

        if (_book is not null)
        {
            foreach (var seen in _book.Markets)
            {
                // A remembered market is only a candidate if it is somewhere this plan could
                // reach. The book has no idea what radius it is being read for.
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

            // The newer of the two, every time. A price the Commander read off the board an hour
            // ago beats a report from last week; a report from this morning beats what they saw a
            // month ago. Nothing here assumes their own eyes are automatically better.
            var chosen = seen is not null && seen.UpdatedAt > market.UpdatedAt
                ? seen with { X = market.X, Y = market.Y, Z = market.Z, DistanceToArrival = market.DistanceToArrival }
                : market;

            if (chosen.UpdatedAt is { } when && when < oldest)
            {
                continue;
            }

            merged.Add(chosen);
        }

        // A market of the Commander's own that the sweep did not return at all — the outpost they
        // are docked at right now, most often, since a page of fifty is not the whole radius.
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

    /// <summary>
    /// Where a remembered market sits relative to this plan's origin. Its own coordinates where
    /// the sweep also found it, and otherwise measured against the origin's own — which is exact
    /// for the station the Commander is standing on and the only case that matters.
    /// </summary>
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
    /// <b>Read rather than derived</b>, because a commodity-filtered sweep may not return the
    /// origin's own stations at all — they need not stock the thing — and distances measured from
    /// nothing are the ranking falling back to price with no warning.
    /// </param>
    /// <param name="Exhausted">
    /// Whether the sweep ran out of <em>galaxy</em> rather than out of budget: a short page ended
    /// it, so everything inside the radius was examined and "nothing within N light years" is a
    /// claim about N light years. False means the page budget ran out first, and the radius
    /// answered is not the radius searched.
    /// </param>
    private sealed record Sweep(
        IReadOnlyList<MarketSnapshot> Markets,
        (double X, double Y, double Z)? Reference,
        bool Exhausted)
    {
        public static readonly Sweep Nothing = new([], null, true);

        /// <summary>
        /// How far the sweep actually reached, in light years, or null when it reached the whole
        /// radius it was asked for. This is the number a negative answer has to say.
        /// </summary>
        public double? Horizon
        {
            get
            {
                if (Exhausted || Reference is not { } origin || Markets.Count == 0)
                {
                    return null;
                }

                var from = new MarketSnapshot
                {
                    Station = string.Empty,
                    System = string.Empty,
                    X = origin.X,
                    Y = origin.Y,
                    Z = origin.Z,
                };

                // The results come back sorted by distance ascending, so the furthest is the last
                // one — but Max rather than Last, because that ordering is the service's promise
                // and this is the one number the honesty of a negative answer rests on.
                return Markets.Max(from.DistanceTo);
            }
        }
    }

    private sealed record Cached(
        string System,
        double Radius,
        string? Commodity,
        bool Selling,
        int Minimum,
        DateTimeOffset At,
        Sweep Sweep);

    /// <summary>
    /// How many sweeps are remembered at once. More than one since #156, because a named-commodity
    /// search and the general sweep are now different requests and a single slot would make two
    /// questions asked in one breath evict each other.
    /// </summary>
    private const int CacheSlots = 8;

    private readonly List<Cached> _sweeps = [];

    /// <summary>
    /// The markets to plan over, and the two facts about how they were gathered (#156).
    /// </summary>
    /// <param name="commodity">
    /// <b>Pushed into the request when the question names one.</b> This is the whole of #156. The
    /// sweep fetches the nearest 150 stations and nothing else, and near a bubble system those 150
    /// span only a few light years — so a commodity none of them carried was reported absent from
    /// the entire radius. Asked for the closest place to buy 200 Landmines near Eurybia, d47 said
    /// there was none within 250 light years while its own data source had 5,229 units 11 light
    /// years away, and said it twice with rising confidence.
    /// <para>
    /// Filtering server-side spends the same 150-station budget entirely on stations that stock
    /// the thing, which is what makes the horizon stop mattering for the case that had it. The
    /// general sweep — trade planning, colonisation sourcing — passes null and is untouched:
    /// those questions have no commodity to filter by, and for them the comment this replaces was
    /// right that extra stations cost arithmetic rather than correctness.
    /// </para>
    /// </param>
    private async Task<Sweep> SweepAsync(
        string system,
        double radius,
        CancellationToken cancellationToken,
        string? commodity = null,
        bool selling = false,
        int minimum = 1)
    {
        lock (_gate)
        {
            // A wider cached sweep answers a narrower question: the planner measures every leg
            // itself, so extra stations cost arithmetic rather than correctness.
            //
            // <b>Only within the same filter, though.</b> An unfiltered sweep is the nearest 150
            // markets and a filtered one is the nearest 150 that stock a named commodity; neither
            // is a superset of the other, and answering one question with the other's cache is
            // #156 arriving through the back door.
            var cached = _sweeps.Find(entry =>
                string.Equals(entry.System, system, StringComparison.OrdinalIgnoreCase)
                && entry.Radius >= radius
                && string.Equals(entry.Commodity, commodity, StringComparison.OrdinalIgnoreCase)
                && entry.Selling == selling

                // A sweep with a lower floor is a superset; one with a higher floor is not (#296).
                && entry.Minimum <= minimum
                && _now() - entry.At < CacheLife);

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
                SpanshRequest.Markets(system, radius, PageSize, page, commodity, selling, minimum),
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

            // A short page is the last page. Asking for the next one costs another second and
            // returns nothing — and it is also the evidence that the radius was examined rather
            // than merely asked about.
            if (read.Count < PageSize)
            {
                exhausted = true;
                break;
            }
        }

        var sweep = new Sweep(markets, reference, exhausted);

        lock (_gate)
        {
            _sweeps.RemoveAll(entry =>
                string.Equals(entry.System, system, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.Commodity, commodity, StringComparison.OrdinalIgnoreCase)
                && entry.Selling == selling
                && entry.Minimum == minimum);

            _sweeps.Add(new Cached(system, radius, commodity, selling, minimum, _now(), sweep));

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
