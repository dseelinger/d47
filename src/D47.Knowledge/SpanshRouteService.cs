using System.Globalization;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary>
/// <see cref="IRouteService"/> against spansh.co.uk.
/// <para>
/// A different protocol from the searches, which is why it is a different class. A plot is
/// submitted as a form post, answered with <c>202</c> and a job id, and then polled at
/// <c>api/results/&lt;job&gt;</c> until it stops saying <c>queued</c>. Measured on 2026-08-14:
/// Sol to Colonia came back on the third poll, a Road to Riches loop on the first, and a four-hop
/// trade route took forty-eight seconds.
/// </para>
/// <para>
/// <b>The route endpoints echo back the parameters they understood, and only those.</b> That is a
/// local oracle the search endpoints do not offer, and it is how the parameter names here were
/// established rather than guessed: <c>capital</c>, <c>cargo_capacity</c>, <c>capacity</c> and
/// <c>loop</c> all vanish from a trade plot's echo, while <c>starting_capital</c> and
/// <c>max_cargo</c> survive. A dropped key is not an error — the plot runs with the parameter at
/// its default, which for capital means nothing is affordable and the route comes back empty.
/// </para>
/// </summary>
public sealed class SpanshRouteService : IRouteService, IDisposable
{
    /// <summary>
    /// The same host the galaxy search reaches, named here too rather than shared, so that
    /// grepping for the destination finds every class that talks to it.
    /// </summary>
    public const string Host = SpanshGalaxyService.Host;

    private readonly HttpClient _http;

    private readonly ILogger<SpanshRouteService> _logger;

    private readonly bool _ownsClient;

    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    /// <param name="delay">
    /// How to wait between polls. Injected so a test can drive the whole poll loop without a
    /// second of real time passing — the same reason nothing in Core reads the clock.
    /// </param>
    public SpanshRouteService(
        ILogger<SpanshRouteService> logger,
        HttpClient? http = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _logger = logger;
        _ownsClient = http is null;
        _delay = delay ?? ((wait, token) => Task.Delay(wait, token));

        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        _http.BaseAddress ??= new Uri($"https://{Host}/");

        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47/0.1 (+https://github.com/dseelinger/d47)");
        }
    }

    /// <summary>
    /// How long a plot may take before d47 gives up on it.
    /// <para>
    /// Longer than the fifteen seconds a search gets, and deliberately: a Commander asking for a
    /// route to Colonia knows they have asked for arithmetic, and a search that takes half a
    /// minute has failed at being useful where a plot that does has not. Bounded all the same,
    /// because a job that is never coming back has to become a sentence eventually.
    /// </para>
    /// </summary>
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

        return result is null ? null : SpanshResponse.ReadRoute(result.Value);
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

        return result is null ? null : SpanshResponse.ReadRiches(result.Value);
    }

    /// <summary>
    /// The exobiology plotter (list.md Phase 18, "Find the exobiology").
    /// <para>
    /// <b>Two traps here that the other four do not have, both established live rather than
    /// guessed.</b> <c>from</c> is required, works, and comes home under a <em>different name</em> —
    /// the echo re-emits it as <c>source</c> — so a caller checking its own parameters against the
    /// echo would conclude the origin was ignored and "fix" it into something that really is. And
    /// <c>use_mapping_value</c>, which the Road to Riches plotter honours, is silently dropped here.
    /// </para>
    /// </summary>
    public async Task<ExobiologyRoute?> PlotExobiologyAsync(
        ExobiologyQuery query,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            "api/exobiology/route",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // The endpoint says what it requires outright, which none of the others do:
                // "from, range, radius and max_results are required".
                ["from"] = query.From,
                ["range"] = Number(query.JumpRange),
                ["radius"] = Number(query.Radius),
                ["max_results"] = query.MaxResults.ToString(CultureInfo.InvariantCulture),
                ["min_value"] = query.MinimumValue.ToString(CultureInfo.InvariantCulture),
                ["max_distance"] = Number(query.MaxDistanceToArrival),
                ["loop"] = query.Loop ? "1" : "0",
            },
            "the exobiology plotter",
            cancellationToken).ConfigureAwait(false);

        return result is null ? null : SpanshResponse.ReadExobiology(result.Value);
    }

    public async Task<TradeRoute?> PlotTradeAsync(TradeQuery query, CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            "api/trade/route",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["system"] = query.System,
                ["station"] = query.Station,

                // Not `capital` and not `cargo_capacity`. Both are silently dropped, and a trade
                // plot with no capital finds nothing affordable and reports an empty route — a
                // wrong answer that looks exactly like a correct one about a poor Commander.
                ["starting_capital"] = query.Capital.ToString(CultureInfo.InvariantCulture),
                ["max_cargo"] = query.CargoCapacity.ToString(CultureInfo.InvariantCulture),
                ["max_hops"] = query.MaxHops.ToString(CultureInfo.InvariantCulture),
                ["max_hop_distance"] = Number(query.MaxHopDistance),
                ["max_system_distance"] = Number(query.MaxSystemDistance),
                ["max_price_age"] = query.MaxPriceAge.ToString(CultureInfo.InvariantCulture),
                ["requires_large_pad"] = query.LargePadOnly ? "1" : "0",
                ["unique"] = query.Unique ? "1" : "0",
            },
            "the trade planner",
            cancellationToken).ConfigureAwait(false);

        return result is null ? null : SpanshResponse.ReadTrade(result.Value);
    }

    /// <summary>
    /// Submit, then poll until it stops being queued.
    /// <para>
    /// Null means the plotter answered and had no route, which is different from it not
    /// answering: "Unable to find route" arrives as a <em>completed</em> job whose status is
    /// <c>failed</c>, and treating that as an outage would tell the Commander to try again at
    /// something that will fail identically every time.
    /// </para>
    /// </summary>
    private async Task<JsonElement?> RunAsync(
        string path,
        Dictionary<string, string> parameters,
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

        // A fixed cadence rather than a backoff. The wait is bounded and short, the jobs that
        // finish quickly finish on the first or second poll anyway, and a backoff would mean the
        // forty-eight-second case waits well past finishing before anybody notices.
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
    /// <para>
    /// The plotters reject a bad request loudly — a system nobody has heard of is a 400 reading
    /// "Could not find finishing system", and a jump range under ten is "range must be greater
    /// than 10 LY" — so unlike the searches, the message is worth reading back rather than
    /// replacing. It is still quoted rather than trusted: it is third-party text on its way to a
    /// model, so it is trimmed to one short sentence.
    /// </para>
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

    /// <summary>
    /// The <c>error</c> string out of a refusal, bounded. Third-party text on its way to a model,
    /// so it is length-capped and single-lined rather than passed through — the plotters' messages
    /// are one short clause today and nothing guarantees they stay that way.
    /// </summary>
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
