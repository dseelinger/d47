using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary>
/// <see cref="ITradePlanService"/> — the markets fetched, the local ones preferred, and
/// <see cref="TradePlanner"/> run over both (list.md Phase 36).
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

    private Cached? _cached;

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
    /// Where to buy one commodity, or where to dump it (list.md Phase 49).
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
        var fetched = await SweepAsync(search.System, radius, cancellationToken).ConfigureAwait(false);
        var origin = fetched.FirstOrDefault(market =>
            string.Equals(market.System, search.System, StringComparison.OrdinalIgnoreCase)
            && (search.Station is null || market.IsSamePlaceAs(search.Station, search.System)));

        // Failing that, anything in the reference system at all: the coordinates are the system's
        // rather than the station's, and a question asked in light years does not need the pad.
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

        return new CommodityAnswer(
            CommodityMarketSearch.Rank(search.Query, usable, origin),
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
        var fetched = await SweepAsync(query.System, radius, cancellationToken).ConfigureAwait(false);

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

    private sealed record Cached(
        string System,
        double Radius,
        DateTimeOffset At,
        IReadOnlyList<MarketSnapshot> Markets);

    private async Task<IReadOnlyList<MarketSnapshot>> SweepAsync(
        string system,
        double radius,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            // A wider cached sweep answers a narrower question: the planner measures every leg
            // itself, so extra stations cost arithmetic rather than correctness.
            if (_cached is { } cached
                && string.Equals(cached.System, system, StringComparison.OrdinalIgnoreCase)
                && cached.Radius >= radius
                && _now() - cached.At < CacheLife)
            {
                _logger.LogDebug("Re-using {Count} cached markets near {System}", cached.Markets.Count, system);
                return cached.Markets;
            }
        }

        var markets = new List<MarketSnapshot>();

        for (var page = 0; page < Pages; page++)
        {
            using var content = new StringContent(
                SpanshRequest.Markets(system, radius, PageSize, page), Encoding.UTF8, "application/json");

            using var document = await SendAsync(
                token => _http.PostAsync("api/stations/search", content, token),
                cancellationToken).ConfigureAwait(false);

            if (document is null)
            {
                break;
            }

            var read = SpanshResponse.ReadMarkets(document);

            markets.AddRange(read);

            // A short page is the last page. Asking for the next one costs another second and
            // returns nothing.
            if (read.Count < PageSize)
            {
                break;
            }
        }

        lock (_gate)
        {
            _cached = new Cached(system, radius, _now(), markets);
        }

        return markets;
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
