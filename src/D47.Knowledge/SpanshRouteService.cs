using System.Globalization;
using System.Text;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary><see cref="IRouteService"/> against spansh.co.uk.</summary>
public sealed class SpanshRouteService : IRouteService, IDisposable
{
    /// <summary>
    /// The same host the galaxy search reaches, named here too rather than shared, so that grepping for
    /// the destination finds every class that talks to it.
    /// </summary>
    public const string Host = SpanshGalaxyService.Host;

    private readonly HttpClient _http;

    private readonly ILogger<SpanshRouteService> _logger;

    private readonly bool _ownsClient;

    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    /// <summary><param name="delay"> How to wait between polls.</summary>
    /// <param name="delay">How to wait between polls.</param>
    public SpanshRouteService(
        ILogger<SpanshRouteService> logger,
        HttpClient? http = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _logger = logger;
        _ownsClient = http is null;
        _delay = delay ?? ((wait, token) => Task.Delay(wait, token));

        _http = KnowledgeHttp.Client(http, Host, TimeSpan.FromSeconds(15));
    }

    /// <summary>How long a plot may take before d47 gives up on it.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(90);

    public async Task<PlottedRoute?> PlotAsync(RouteQuery query, CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            "api/route",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["from"] = query.From,
                ["to"] = query.To,
                ["range"] = Number(query.JumpRange),
                ["efficiency"] = query.Efficiency.ToString(CultureInfo.InvariantCulture),
            },
            "the route plotter",
            cancellationToken).ConfigureAwait(false);

        if (result is null || SpanshResponse.ReadRoute(result.Value) is not { } route)
        {
            return null;
        }

        var classes = await StarClassesAsync(
            Addresses(
                route.Start?.SystemAddress,
                route.Waypoints.Where(waypoint => !waypoint.IsNeutron).Select(waypoint => waypoint.SystemAddress)),
            cancellationToken).ConfigureAwait(false);

        return classes.Count == 0
            ? route
            : route with
            {
                Start = route.Start is { } start ? start with { StarClass = ClassOf(classes, start.SystemAddress) } : null,
                Waypoints =
                [
                    .. route.Waypoints.Select(waypoint => waypoint.IsNeutron
                        ? waypoint
                        : waypoint with { StarClass = ClassOf(classes, waypoint.SystemAddress) }),
                ],
            };
    }

    public async Task<RichesRoute?> PlotRichesAsync(RichesQuery query, CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            "api/riches/route",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["from"] = query.From,
                ["range"] = Number(query.JumpRange),
                ["radius"] = Number(query.Radius),
                ["max_results"] = query.MaxResults.ToString(CultureInfo.InvariantCulture),
                ["min_value"] = query.MinimumValue.ToString(CultureInfo.InvariantCulture),
                ["max_distance"] = Number(query.MaxDistanceToArrival),
                ["use_mapping_value"] = "1",
                ["loop"] = query.Loop ? "1" : "0",
            },
            "the Road to Riches plotter",
            cancellationToken).ConfigureAwait(false);

        if (result is null || SpanshResponse.ReadRiches(result.Value) is not { } route)
        {
            return null;
        }

        var classes = await StarClassesAsync(
            Addresses(
                route.Start?.SystemAddress,
                route.Stops.Select(stop => stop.SystemAddress),
                route.End?.SystemAddress),
            cancellationToken).ConfigureAwait(false);

        return classes.Count == 0
            ? route
            : route with
            {
                Start = route.Start is { } start ? start with { StarClass = ClassOf(classes, start.SystemAddress) } : null,
                End = route.End is { } end ? end with { StarClass = ClassOf(classes, end.SystemAddress) } : null,
                Stops = [.. route.Stops.Select(stop => stop with { StarClass = ClassOf(classes, stop.SystemAddress) })],
            };
    }

    /// <summary>The exobiology plotter (Phase 18, "Find the exobiology").</summary>
    public async Task<ExobiologyRoute?> PlotExobiologyAsync(
        ExobiologyQuery query,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            "api/exobiology/route",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // The endpoint says what it requires outright, which none of the others do: "from, range,
                // radius and max_results are required".
                ["from"] = query.From,
                ["range"] = Number(query.JumpRange),
                ["radius"] = Number(query.Radius),
                ["max_results"] = query.MaxResults.ToString(CultureInfo.InvariantCulture),
                ["min_value"] = query.MinimumValue.ToString(CultureInfo.InvariantCulture),
                ["max_distance"] = Number(query.MaxDistanceToArrival),
                ["avoid_thargoids"] = "1",
                ["loop"] = query.Loop ? "1" : "0",
            },
            "the exobiology plotter",
            cancellationToken).ConfigureAwait(false);

        if (result is null || SpanshResponse.ReadExobiology(result.Value) is not { } route)
        {
            return null;
        }

        var classes = await StarClassesAsync(
            Addresses(
                route.Start?.SystemAddress,
                route.Stops.Select(stop => stop.SystemAddress),
                route.End?.SystemAddress),
            cancellationToken).ConfigureAwait(false);

        return classes.Count == 0
            ? route
            : route with
            {
                Start = route.Start is { } start ? start with { StarClass = ClassOf(classes, start.SystemAddress) } : null,
                End = route.End is { } end ? end with { StarClass = ClassOf(classes, end.SystemAddress) } : null,
                Stops = [.. route.Stops.Select(stop => stop with { StarClass = ClassOf(classes, stop.SystemAddress) })],
            };
    }

    public async Task<CarrierRoute?> PlotCarrierAsync(CarrierRouteQuery query, CancellationToken cancellationToken)
    {
        List<KeyValuePair<string, string>> form =
        [
            new("source", query.Source),
            .. query.Destinations.Select(destination => new KeyValuePair<string, string>("destinations", destination)),
            new("capacity_used", query.CapacityUsed.ToString(CultureInfo.InvariantCulture)),
            new("tritium_stored", query.TritiumStored.ToString(CultureInfo.InvariantCulture)),
            new("fuel_loaded", query.FuelLoaded.ToString(CultureInfo.InvariantCulture)),
            new("capacity", query.Capacity.ToString(CultureInfo.InvariantCulture)),
        ];

        var result = await RunAsync(
            "api/fleetcarrier/route",
            form,
            "the carrier route plotter",
            cancellationToken).ConfigureAwait(false);

        return result is null ? null : SpanshResponse.ReadCarrier(result.Value);
    }

    private const int MaxLookupRequests = 3;

    private static List<long> Addresses(long? start, IEnumerable<long?> stops, long? end = null) =>
        [.. new[] { start }.Concat(stops).Append(end).OfType<long>().Distinct()];

    private static string? ClassOf(Dictionary<long, string> classes, long? address) =>
        address is { } key && classes.TryGetValue(key, out var starClass) ? starClass : null;

    /// <summary>
    /// The main star class of each system, by id64, from at most <see cref="MaxLookupRequests"/> searches.
    /// A refused or unreadable search leaves its systems out.
    /// </summary>
    private async Task<Dictionary<long, string>> StarClassesAsync(
        List<long> addresses,
        CancellationToken cancellationToken)
    {
        var classes = new Dictionary<long, string>();

        foreach (var chunk in addresses.Chunk(GalaxyQuery.MaxSize).Take(MaxLookupRequests))
        {
            try
            {
                using var content = new StringContent(
                    SpanshRequest.SystemsByAddress(chunk), Encoding.UTF8, "application/json");
                using var document = await SpanshGalaxyService.SendAsync(
                    _logger,
                    token => _http.PostAsync("api/systems/search", content, token),
                    "the galaxy search",
                    cancellationToken).ConfigureAwait(false);

                foreach (var (address, starClass) in SpanshResponse.ReadMainStarClasses(document!))
                {
                    classes[address] = starClass;
                }
            }
            catch (GalaxyUnavailableException)
            {
                // SendAsync has logged the status; the plan keeps unknown classes.
            }
        }

        return classes;
    }

    /// <summary>Submit, then poll until it stops being queued.</summary>
    private async Task<JsonElement?> RunAsync(
        string path,
        IEnumerable<KeyValuePair<string, string>> parameters,
        string what,
        CancellationToken cancellationToken)
    {
        string job;

        using (var content = new FormUrlEncodedContent(parameters))
        using (var submitted = await SendAsync(
                   () => _http.PostAsync(path, content, cancellationToken), what, cancellationToken)
                   .ConfigureAwait(false))
        {
            if (!submitted.RootElement.TryGetProperty("job", out var id) || id.GetString() is not { } found)
            {
                _logger.LogWarning("{What} accepted the request without giving a job id", what);
                throw new GalaxyUnavailableException($"{Capitalise(what)} accepted that and then lost it.");
            }

            job = found;
        }

        // A fixed cadence rather than a backoff.
        var interval = TimeSpan.FromSeconds(1.5);
        var deadline = Budget;

        while (deadline > TimeSpan.Zero)
        {
            using var document = await SendAsync(
                () => _http.GetAsync($"api/results/{job}", cancellationToken), what, cancellationToken)
                .ConfigureAwait(false);

            var root = document.RootElement;
            var status = root.TryGetProperty("status", out var value) ? value.GetString() : null;

            if (status is not "queued")
            {
                if (status is not "ok" || !root.TryGetProperty("result", out var result))
                {
                    _logger.LogInformation(
                        "{What} finished job {Job} without a route: {Status}", what, job, status);

                    return null;
                }

                return result.Clone();
            }

            await _delay(interval, cancellationToken).ConfigureAwait(false);
            deadline -= interval;
        }

        _logger.LogWarning("{What} was still working on job {Job} after {Budget}", what, job, Budget);

        throw new GalaxyUnavailableException(
            $"{Capitalise(what)} is still working after {Budget.TotalSeconds:N0} seconds. "
            + "A shorter route, fewer hops or a smaller radius will come back faster.");
    }

    /// <summary>
    /// One request, with every way it can go wrong turned into a sentence a Commander can act on.
    /// </summary>
    private async Task<JsonDocument> SendAsync(
        Func<Task<HttpResponseMessage>> send,
        string what,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await send().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("{What} did not answer within the timeout", what);
            throw new GalaxyUnavailableException($"{Capitalise(what)} took too long to answer.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "{What} could not be reached", what);
            throw new GalaxyUnavailableException($"I couldn't reach {what} — check the network connection.");
        }

        using (response)
        {
            string body;

            try
            {
                body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "{What} closed the connection mid-answer", what);
                throw new GalaxyUnavailableException($"{Capitalise(what)} closed the connection mid-answer.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("{What} answered {Status}", what, (int)response.StatusCode);

                throw new GalaxyUnavailableException(Refusal(what, response.StatusCode, body));
            }

            try
            {
                return JsonDocument.Parse(body);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "{What} answered with something that was not JSON", what);
                throw new GalaxyUnavailableException($"{Capitalise(what)} answered with something I couldn't read.");
            }
        }
    }

    private static string Refusal(string what, System.Net.HttpStatusCode status, string body)
    {
        if (status == System.Net.HttpStatusCode.TooManyRequests)
        {
            return $"{Capitalise(what)} is rate limiting me. It should clear shortly.";
        }

        if (status >= System.Net.HttpStatusCode.InternalServerError)
        {
            return $"{Capitalise(what)} reported a server error. It should clear shortly.";
        }

        return Reason(body) is { } reason
            ? $"{Capitalise(what)} refused that: {reason}"
            : $"{Capitalise(what)} refused that request.";
    }

    /// <summary>The <c>error</c> string out of a refusal, bounded.</summary>
    private static string? Reason(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if (!document.RootElement.TryGetProperty("error", out var error)
                || error.ValueKind != JsonValueKind.String
                || error.GetString() is not { } message)
            {
                return null;
            }

            var single = message.ReplaceLineEndings(" ").Trim();

            return single.Length switch
            {
                0 => null,
                > 160 => single[..160] + "…",
                _ => single,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Number(double value) =>
        value == Math.Floor(value) && Math.Abs(value) < 1e15
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Capitalise(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }
}
