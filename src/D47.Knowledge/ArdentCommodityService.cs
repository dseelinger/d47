using System.Globalization;
using System.Net;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary>Where one commodity is bought and sold, read from a live EDDN-fed index (#350).</summary>
public sealed class ArdentCommodityService : IDisposable
{
    /// <summary>The host, named here so grepping for where d47 goes finds it.</summary>
    public const string Host = "api.ardent-insight.com";

    /// <summary>
    /// How many rows one reply carries at most, measured by asking questions with far more answers than
    /// this and counting exactly this many back every time.
    /// </summary>
    public const int RowCeiling = 1_000;

    /// <summary>
    /// How stale a quote may be to come back at all, in whole days, from the bound the Commander
    /// actually set.
    /// </summary>
    public static int DaysAgo(int hours) => Math.Max(1, (int)Math.Ceiling(hours / 24.0));

    private const string Nearby = "v2/system/name/{0}/commodity/name/{1}/nearby/{2}";

    private const string Market = "v2/market/{0}/commodity/name/{1}";

    /// <summary>One call's wait.</summary>
    private static readonly TimeSpan CallBudget = TimeSpan.FromSeconds(20);

    private readonly HttpClient _http;

    private readonly ILogger<ArdentCommodityService> _logger;

    private readonly bool _ownsClient;

    private readonly Lock _gate = new();

    private readonly Dictionary<string, (double X, double Y, double Z)?> _systems =
        new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<string>? _catalogue;

    public ArdentCommodityService(ILogger<ArdentCommodityService> logger, HttpClient? http = null)
    {
        _logger = logger;
        _ownsClient = http is null;

        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(45) };

        _http.BaseAddress ??= new Uri($"https://{Host}/");

        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47/0.1 (+https://github.com/dseelinger/d47)");
        }
    }

    /// <summary>One call's answer, in the shape the planner's own sweep already returns.</summary>
    /// <param name="Reference">The coordinates of the system the search was made from.</param>
    /// <param name="Truncated">
    /// Whether the reply came back on <see cref="RowCeiling"/> — the top of a longer list, with no page
    /// to ask for it and no promise that what arrived is the nearest of it.
    /// </param>
    public sealed record Reply(
        IReadOnlyList<MarketSnapshot> Markets,
        (double X, double Y, double Z)? Reference,
        bool Truncated);

    /// <summary>Every station within the radius that has the commodity, or wants it.</summary>
    public async Task<Reply> FindAsync(CommoditySearch search, CancellationToken cancellationToken)
    {
        var query = search.Query;

        if (await SymbolAsync(query.Commodity, cancellationToken).ConfigureAwait(false) is not { } symbol)
        {
            _logger.LogWarning("The market index lists no commodity called {Commodity}", query.Commodity);

            throw new GalaxyUnavailableException(
                $"The market index has no commodity called \"{query.Commodity}\".");
        }

        // Sent as asked. #157's ruling — do not cap the search distance — and the service's own 500 light
        // year horizon is the service's to apply and d47's to report, not to copy.
        var radius = Math.Max(1, query.MaxDistance);

        var url = string.Format(
                CultureInfo.InvariantCulture,
                Nearby,
                Uri.EscapeDataString(search.System),
                Uri.EscapeDataString(symbol),
                query.Side == TradeSide.Selling ? "imports" : "exports")
            + $"?minVolume={Math.Max(1, query.MinAvailable ?? 1)}"
            + $"&maxDistance={(int)Math.Ceiling(radius)}"
            + $"&maxDaysAgo={DaysAgo(search.MaxPriceAge)}"

            // Omitted rather than true when carriers are wanted, and that is not a tidy-up: measured
            // 2026-09-06, fleetCarriers=true returns carriers and *nothing else* — 225 rows, every one of
            // them a FleetCarrier — while leaving it off returns both, 422 rows with 23 carriers among them.
            + (query.IncludeCarriers ? string.Empty : "&fleetCarriers=false");

        using var document = await SendAsync(url, cancellationToken).ConfigureAwait(false);

        // The cut is counted off the reply, not off what survived reading it. A row missing a station
        // name or a coordinate is skipped below, and one such row in a capped thousand would leave 999
        // markets — under the ceiling, so the answer would call itself complete and name a "nearest" out of a
        // list the index had already cut.
        var rows = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.GetArrayLength()
            : 0;

        var markets = Read(document, query.Commodity);

        var reference = await CoordinatesAsync(search.System, cancellationToken).ConfigureAwait(false);

        return new Reply(markets, reference, rows >= RowCeiling);
    }

    /// <summary>
    /// What one station last reported about one commodity, or null where the index has no report for
    /// that market (#116).
    /// </summary>
    public async Task<StationQuote?> QuoteAsync(
        long marketId,
        string commodity,
        CancellationToken cancellationToken)
    {
        if (await SymbolAsync(commodity, cancellationToken).ConfigureAwait(false) is not { } symbol)
        {
            _logger.LogWarning("The market index lists no commodity called {Commodity}", commodity);

            throw new GalaxyUnavailableException(
                $"The market index has no commodity called \"{commodity}\".");
        }

        // The catalogue's answer, folded. This endpoint matches on letters and digits alone, case
        // ignored: "lowtemperaturediamond" is found and "low_temperature_diamond" is not. Folding the
        // spelling the Commander used instead would ask for "lowtemperaturediamonds", which the index
        // spells singular and answers 404 for.
        var url = string.Format(CultureInfo.InvariantCulture, Market, marketId, Fold(symbol));

        using var document =
            await SendAsync(url, missingIsNull: true, cancellationToken).ConfigureAwait(false);

        if (document?.RootElement is not { ValueKind: JsonValueKind.Object } row)
        {
            return null;
        }

        return new StationQuote(
            Whole(row, "stock"),
            Whole(row, "stockBracket"),
            Whole(row, "buyPrice"),
            Whole(row, "demand"),
            Whole(row, "sellPrice"),
            Moment(row, "updatedAt"));
    }

    /// <summary>The rows, as <see cref="MarketSnapshot"/> reads them.</summary>
    private static IReadOnlyList<MarketSnapshot> Read(JsonDocument document, string commodity)
    {
        var markets = new List<MarketSnapshot>();

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return markets;
        }

        foreach (var row in document.RootElement.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object
                || Text(row, "stationName") is not { Length: > 0 } station
                || Text(row, "systemName") is not { Length: > 0 } system
                || Number(row, "systemX") is not { } x
                || Number(row, "systemY") is not { } y
                || Number(row, "systemZ") is not { } z)
            {
                continue;
            }

            var quote = new MarketQuote(commodity)
            {
                BuyPrice = Whole(row, "buyPrice"),
                SellPrice = Whole(row, "sellPrice"),
                Supply = Whole(row, "stock"),
                Demand = Whole(row, "demand"),
            };

            markets.Add(new MarketSnapshot
            {
                Station = station,
                System = system,

                // The reply's own `distance` is rounded to a whole light year — 64 and 63 for the two
                // stations #350 measured at 63.5 and 63.1 — so it is never read.
                X = x,
                Y = y,
                Z = z,
                DistanceToArrival = Number(row, "distanceToArrival"),

                // 3 is a large pad, 2 medium, 1 small.
                HasLargePad = Whole(row, "maxLandingPadSize") >= 3,
                Type = StationType(Text(row, "stationType")),
                UpdatedAt = Moment(row, "updatedAt"),
                Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
                {
                    [commodity] = quote,
                },
            });
        }

        return markets;
    }

    /// <summary>Ardent's word for a station in the words <see cref="MarketSnapshot"/>'s own rules speak.</summary>
    private static string? StationType(string? type) => type switch
    {
        null or "" or "None" => null,
        "AsteroidBase" => "Asteroid base",
        "Bernal" => "Ocellus Starport",
        "Coriolis" => "Coriolis Starport",
        "CraterOutpost" => "Planetary Outpost",
        "CraterPort" => "Planetary Port",
        "Dodec" => "Dodec Starport",
        "FleetCarrier" => "Drake-Class Carrier",
        "MegaShip" => "Mega ship",
        "Ocellus" => "Ocellus Starport",
        "OnFootSettlement" => "Settlement",
        "Orbis" => "Orbis Starport",
        "PlanetaryConstructionDepot" => "Planetary Construction Depot",
        "SpaceConstructionDepot" => "Space Construction Depot",
        "StrongholdCarrier" => "Stronghold Carrier",
        "SurfaceStation" => "Surface Settlement",
        _ => type,
    };

    /// <summary>Where the search is being made from.</summary>
    private async Task<(double X, double Y, double Z)?> CoordinatesAsync(
        string system,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_systems.TryGetValue(system, out var known))
            {
                return known;
            }
        }

        using var document = await SendAsync(
            $"v2/system/name/{Uri.EscapeDataString(system)}", cancellationToken).ConfigureAwait(false);

        var root = document.RootElement;

        (double, double, double)? found =
            root.ValueKind == JsonValueKind.Object
            && Number(root, "systemX") is { } x
            && Number(root, "systemY") is { } y
            && Number(root, "systemZ") is { } z
                ? (x, y, z)
                : null;

        lock (_gate)
        {
            _systems[system] = found;
        }

        return found;
    }

    /// <summary>The index's own name for a commodity the Commander spelled out.</summary>
    private async Task<string?> SymbolAsync(string commodity, CancellationToken cancellationToken)
    {
        var wanted = Fold(commodity);

        if (wanted.Length == 0)
        {
            return null;
        }

        var names = await CatalogueAsync(cancellationToken).ConfigureAwait(false);

        foreach (var name in names)
        {
            if (string.Equals(Fold(name), wanted, StringComparison.Ordinal))
            {
                return name;
            }
        }

        var singular = Singular(wanted);

        foreach (var name in names)
        {
            if (string.Equals(Singular(Fold(name)), singular, StringComparison.Ordinal))
            {
                return name;
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<string>> CatalogueAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_catalogue is not null)
            {
                return _catalogue;
            }
        }

        using var document = await SendAsync("v2/commodities", cancellationToken).ConfigureAwait(false);

        var names = new List<string>();

        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in document.RootElement.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Object && Text(row, "commodityName") is { Length: > 0 } name)
                {
                    names.Add(name);
                }
            }
        }

        // Only an answer worth keeping is kept.
        if (names.Count > 0)
        {
            lock (_gate)
            {
                _catalogue = names;
            }
        }

        return names;
    }

    /// <summary>
    /// Whether two spellings name the same good, by the same rule <see cref="SymbolAsync"/> resolves
    /// one by: the fold first, then the fold with a trailing plural set aside.
    /// </summary>
    internal static bool SameCommodity(string one, string other)
    {
        var folded = Fold(one);
        var against = Fold(other);

        return folded.Length > 0
               && (string.Equals(folded, against, StringComparison.Ordinal)
                   || string.Equals(Singular(folded), Singular(against), StringComparison.Ordinal));
    }

    private static string Fold(string name) =>
        string.Concat(name.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private static string Singular(string folded) =>
        folded.EndsWith('s') ? folded[..^1] : folded;

    private static string? Text(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? Number(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    private static int Whole(JsonElement row, string name) =>
        Number(row, name) is { } number ? (int)Math.Clamp(number, int.MinValue, int.MaxValue) : 0;

    private static DateTimeOffset? Moment(JsonElement row, string name) =>
        Text(row, name) is { } when
        && DateTimeOffset.TryParse(
            when, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var moment)
            ? moment
            : null;

    /// <summary>One call, with every way it can go wrong turned into a sentence.</summary>
    private async Task<JsonDocument> SendAsync(string url, CancellationToken cancellationToken) =>
        (await SendAsync(url, missingIsNull: false, cancellationToken).ConfigureAwait(false))!;

    /// <summary>
    /// The same call, where <paramref name="missingIsNull"/> reads a 404 or an empty body as the index
    /// having no report rather than as a failure. Null only ever comes back under that flag.
    /// </summary>
    private async Task<JsonDocument?> SendAsync(
        string url,
        bool missingIsNull,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(CallBudget);

        HttpResponseMessage response;

        try
        {
            response = await _http.GetAsync(url, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("The market index did not answer within the timeout");
            throw new GalaxyUnavailableException("The market search took too long to answer.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "The market index could not be reached");
            throw new GalaxyUnavailableException(
                "I couldn't reach the market search — check the network connection.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                if (missingIsNull && response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                _logger.LogWarning("The market index answered {Status}", (int)response.StatusCode);

                throw new GalaxyUnavailableException(response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests =>
                        "The market search is rate limiting me. It should clear shortly.",
                    >= HttpStatusCode.InternalServerError =>
                        "The market search reported a server error. It should clear shortly.",
                    _ => "The market search refused that request.",
                });
            }

            // The reply is read whole before this is asked, so the length is the body's own even where
            // the wire carried no Content-Length header to state it.
            if (missingIsNull && response.Content.Headers.ContentLength is 0)
            {
                return null;
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
                _logger.LogWarning("The market index did not finish answering within the timeout");
                throw new GalaxyUnavailableException("The market search took too long to answer.");
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "The market index answered with something that was not JSON");
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
