using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary><see cref="IGalaxyService"/> against spansh.co.uk.</summary>
public sealed class SpanshGalaxyService : IGalaxyService, IDisposable
{
    /// <summary>
    /// The one host this capability reaches, named here and in <see
    /// cref="D47.Core.Configuration.EgressDisclosure"/> — a destination that exists in code and not in
    /// the disclosure is the failure that section exists to prevent.
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
            // The ceiling rather than the budget.
            Timeout = TimeSpan.FromSeconds(45),
        };

        _http.BaseAddress ??= new Uri($"https://{Host}/");

        // Identifying rather than anonymous, because a small service run by one person deserves to know who
        // is generating its traffic and how to ask us to stop.
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47/0.1 (+https://github.com/dseelinger/d47)");
        }
    }

    /// <summary>How long a Commander waits for an answer before being told there is not one.</summary>
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

    /// <summary>The candidate scan.</summary>
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

        // Computed here rather than asked for, so that "how far" has one answer regardless of which endpoint
        // happens to be able to express the question (Phase 14).
        var dx = destination.Value.X - origin.Value.X;
        var dy = destination.Value.Y - origin.Value.Y;
        var dz = destination.Value.Z - origin.Value.Z;

        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    /// <summary>One system's position, found by name.</summary>
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

    /// <summary>The narrowest search that still resolves a reference: one system, within one light year.</summary>
    private static readonly Dictionary<string, string> Nearest =
        new(StringComparer.Ordinal) { ["distance"] = "0-1" };

    /// <summary>One request, with every way it can go wrong turned into a sentence.</summary>
    /// <param name="rejectionMeansMissing">
    /// Whether a 400 is "there is no such system" rather than a fault.
    /// </param>
    /// <param name="budget">How long to wait, defaulting to <see cref="Ordinary"/>.</param>
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
                // The deadline rather than the caller's token, because the body is where the time actually
                // goes on the largest of these responses and a read left unbounded would put the whole budget
                // back where it was.
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
