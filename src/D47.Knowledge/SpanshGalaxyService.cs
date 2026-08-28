using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary>
/// <see cref="IGalaxyService"/> against spansh.co.uk.
/// <para>
/// There is no published API. The endpoints and their request shapes were established against
/// the live service on 2026-08-14 and are recorded in <see cref="SpanshRequest"/>; the response
/// reading is deliberately tolerant for the same reason (<see cref="SpanshResponse"/>). An
/// undocumented service can change without telling anybody, so everything here fails into a
/// sentence a Commander can act on rather than an exception.
/// </para>
/// </summary>
public sealed class SpanshGalaxyService : IGalaxyService, IDisposable
{
    /// <summary>
    /// The one host this capability reaches, named here and in
    /// <see cref="D47.Core.Configuration.EgressDisclosure"/> — a destination that exists in code
    /// and not in the disclosure is the failure that section exists to prevent.
    /// </summary>
    public const string Host = "spansh.co.uk";

    private readonly HttpClient _http;

    private readonly ILogger<SpanshGalaxyService> _logger;

    private readonly bool _ownsClient;

    public SpanshGalaxyService(ILogger<SpanshGalaxyService> logger, HttpClient? http = null)
    {
        _logger = logger;
        _ownsClient = http is null;

        _http = http ?? new HttpClient
        {
            // The ceiling rather than the budget. Every call gets <see cref="Ordinary"/> unless it
            // asks for more, and only one does — see ScanForColonisationAsync. A single client
            // timeout would have to be either too short for that call or too long for all the
            // others, so the wait a Commander actually experiences is set per call and this is
            // only the backstop behind it.
            Timeout = TimeSpan.FromSeconds(45),
        };

        _http.BaseAddress ??= new Uri($"https://{Host}/");

        // Identifying rather than anonymous, because a small service run by one person deserves
        // to know who is generating its traffic and how to ask us to stop.
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47/0.1 (+https://github.com/dseelinger/d47)");
        }
    }

    /// <summary>
    /// How long a Commander waits for an answer before being told there is not one.
    /// <para>
    /// Short by the standards of a web client, and deliberately so: this runs while they are
    /// waiting mid-flight, and a lookup that takes half a minute has already failed at being
    /// useful even if it eventually returns.
    /// </para>
    /// </summary>
    private static readonly TimeSpan Ordinary = TimeSpan.FromSeconds(15);

    public async Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
    {
        using var content = new StringContent(SpanshRequest.Search(query), Encoding.UTF8, "application/json");
        using var document = await SendAsync(
            token => _http.PostAsync("api/systems/search", content, token),
            "the galaxy search",
            cancellationToken).ConfigureAwait(false);

        // Only null where a rejection was asked to mean "missing", which this call does not.
        return SpanshResponse.ReadSearch(document!);
    }

    public async Task<StationSearchResult> FindStationsAsync(
        StationQuery query,
        CancellationToken cancellationToken)
    {
        using var content = new StringContent(SpanshRequest.Stations(query), Encoding.UTF8, "application/json");
        using var document = await SendAsync(
            token => _http.PostAsync("api/stations/search", content, token),
            "the station search",
            cancellationToken).ConfigureAwait(false);

        return SpanshResponse.ReadStations(document!);
    }

    public async Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
    {
        using var content = new StringContent(SpanshRequest.Bodies(query), Encoding.UTF8, "application/json");
        using var document = await SendAsync(
            token => _http.PostAsync("api/bodies/search", content, token),
            "the body search",
            cancellationToken).ConfigureAwait(false);

        return SpanshResponse.ReadBodies(document!);
    }

    /// <summary>
    /// The candidate scan. One call, and the only one in this class that is given longer than
    /// <see cref="Ordinary"/>.
    /// <para>
    /// The response is the largest thing d47 fetches from anywhere — the systems search embeds
    /// every body of every system it returns, and the densest fifteen light years measured came
    /// back as 1.49 MB. There is no way to ask for less: <c>fields</c> and <c>_source</c> are both
    /// accepted and silently ignored. Latency does not track the size — 121 KB took 3.8 seconds
    /// and 1.49 MB took 1.9 — so it is the service rather than the payload that takes its time,
    /// and 13.5 seconds was measured against an ordinary 22-system request. Against a 15-second
    /// budget that is a search which fails roughly one time in five for no reason the Commander
    /// can see, which is why this one asks for more.
    /// </para>
    /// </summary>
    public async Task<ColonisationScan> ScanForColonisationAsync(
        ColonisationQuery query,
        CancellationToken cancellationToken)
    {
        using var content = new StringContent(SpanshRequest.Colonisation(query), Encoding.UTF8, "application/json");
        using var document = await SendAsync(
            token => _http.PostAsync("api/systems/search", content, token),
            "the galaxy search",
            cancellationToken,
            budget: TimeSpan.FromSeconds(35)).ConfigureAwait(false);

        return SpanshResponse.ReadColonisation(document!);
    }

    public async Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken)
    {
        var origin = await CoordinatesAsync(from, cancellationToken).ConfigureAwait(false);

        if (origin is null)
        {
            return null;
        }

        var destination = await CoordinatesAsync(to, cancellationToken).ConfigureAwait(false);

        if (destination is null)
        {
            return null;
        }

        // Computed here rather than asked for, so that "how far" has one answer regardless of
        // which endpoint happens to be able to express the question (Phase 14).
        var dx = destination.Value.X - origin.Value.X;
        var dy = destination.Value.Y - origin.Value.Y;
        var dz = destination.Value.Z - origin.Value.Z;

        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    /// <summary>
    /// One system's position, found by name.
    /// <para>
    /// There is no endpoint that turns a name into a record: <c>api/system/name/&lt;name&gt;</c>
    /// is a 404 and <c>api/system/&lt;id64&gt;</c> needs a number nobody says out loud. What does
    /// work is naming the system as a <em>search reference</em> — the response echoes back the
    /// coordinates it resolved to. The search itself is made as narrow as it can be, since the
    /// results are not what is wanted and one system record is 268 KB.
    /// </para>
    /// <para>
    /// A name the service does not know is a 400, which is why this call treats a rejection as
    /// "no such system" rather than as a failure. Nothing else in a search built from a validated
    /// query can be rejected — that is what the local filter vocabulary buys.
    /// </para>
    /// </summary>
    private async Task<(double X, double Y, double Z)?> CoordinatesAsync(
        string system,
        CancellationToken cancellationToken)
    {
        if (!GalaxyQuery.TryParse(system, Nearest, size: 1, out var probe, out _))
        {
            return null;
        }

        using var content = new StringContent(SpanshRequest.Search(probe), Encoding.UTF8, "application/json");

        using var document = await SendAsync(
            token => _http.PostAsync("api/systems/search", content, token),
            "the galaxy search",
            cancellationToken,
            rejectionMeansMissing: true).ConfigureAwait(false);

        return document is null ? null : SpanshResponse.ReadReferenceCoordinates(document);
    }

    /// <summary>
    /// The narrowest search that still resolves a reference: one system, within one light year.
    /// </summary>
    private static readonly Dictionary<string, string> Nearest =
        new(StringComparer.Ordinal) { ["distance"] = "0-1" };

    /// <summary>
    /// One request, with every way it can go wrong turned into a sentence.
    /// <para>
    /// The distinction that matters is between "not right now" and "not ever with this
    /// question": a rate limit or a 502 is worth the Commander trying again, and a 404 is not.
    /// Both are <see cref="GalaxyUnavailableException"/> because both end this call, but the
    /// wording tells them apart.
    /// </para>
    /// </summary>
    /// <param name="rejectionMeansMissing">
    /// Whether a 400 is "there is no such system" rather than a fault. True only where the sole
    /// thing in the request the service can reject is a system name the Commander supplied.
    /// </param>
    /// <param name="budget">
    /// How long to wait, defaulting to <see cref="Ordinary"/>. Enforced here rather than by the
    /// client's own timeout so that one slow call cannot lengthen every other one — the client's
    /// timeout is the backstop behind this and never the number a Commander experiences.
    /// </param>
    private async Task<JsonDocument?> SendAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> send,
        string what,
        CancellationToken cancellationToken,
        bool rejectionMeansMissing = false,
        TimeSpan? budget = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(budget ?? Ordinary);

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
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("{What} answered {Status}", what, (int)response.StatusCode);

                if (rejectionMeansMissing && response.StatusCode == HttpStatusCode.BadRequest)
                {
                    return null;
                }

                throw new GalaxyUnavailableException(response.StatusCode switch
                {
                    HttpStatusCode.NotFound => "The galaxy search has no record of that.",
                    HttpStatusCode.TooManyRequests =>
                        "The galaxy search is rate limiting me. It should clear shortly.",
                    >= HttpStatusCode.InternalServerError =>
                        $"{Capitalise(what)} reported a server error. It should clear shortly.",
                    _ => $"{Capitalise(what)} refused that request.",
                });
            }

            try
            {
                // The deadline rather than the caller's token, because the body is where the time
                // actually goes on the largest of these responses and a read left unbounded would
                // put the whole budget back where it was.
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
                _logger.LogWarning("{What} did not finish answering within the timeout", what);
                throw new GalaxyUnavailableException($"{Capitalise(what)} took too long to answer.");
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "{What} answered with something that was not JSON", what);
                throw new GalaxyUnavailableException($"{Capitalise(what)} answered with something I couldn't read.");
            }
        }
    }

    private static string Capitalise(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }
}
