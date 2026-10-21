using System.Net;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary><see cref="IColonisationPlanService"/> against Raven Colonial's public API.</summary>
public sealed class RavenColonialPlanService : IColonisationPlanService, IDisposable
{
    /// <summary>The one host this service reaches; <see cref="D47.Core.Configuration.EgressDisclosure"/> names it too.</summary>
    public const string Host = "ravencolonial100-awcbdvabgze4c5cq.canadacentral-01.azurewebsites.net";

    private readonly TimeSpan _budget;

    private readonly HttpClient _http;

    private readonly ILogger<RavenColonialPlanService> _logger;

    private readonly bool _ownsClient;

    private readonly Func<DateTimeOffset> _now;

    public RavenColonialPlanService(
        ILogger<RavenColonialPlanService> logger,
        HttpClient? http = null,
        Func<DateTimeOffset>? now = null,
        TimeSpan? budget = null)
    {
        _logger = logger;
        _budget = budget ?? TimeSpan.FromSeconds(15);
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _ownsClient = http is null;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        _http.BaseAddress ??= new Uri($"https://{Host}/");

        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47/0.1 (+https://github.com/dseelinger/d47)");
        }
    }

    public async Task<SystemPlan?> SystemAsync(string systemName, CancellationToken cancellationToken)
    {
        var system = Uri.EscapeDataString(systemName);

        using var sitesDocument = await GetAsync(
            $"api/v2/system/{system}/sites", "the colonisation planner's sites", cancellationToken, missing: true)
            .ConfigureAwait(false);

        if (sitesDocument is null)
        {
            return null;
        }

        using var bodiesDocument = await GetAsync(
            $"api/v2/system/{system}/bodies", "the colonisation planner's bodies", cancellationToken)
            .ConfigureAwait(false);

        var sites = RavenColonialResponse
            .ReadSites(sitesDocument, RavenColonialResponse.ReadBodies(bodiesDocument!))
            .ToList();

        if (sites.Count == 0)
        {
            return null;
        }

        for (var i = 0; i < sites.Count; i++)
        {
            if (sites[i] is { Status: PlanStatus.Build, BuildId: { } buildId } site)
            {
                sites[i] = await ProjectAsync(site, buildId, cancellationToken).ConfigureAwait(false);
            }
        }

        return new SystemPlan(systemName, sites, _now());
    }

    private async Task<PlannedSite> ProjectAsync(PlannedSite site, string buildId, CancellationToken cancellationToken)
    {
        var id = Uri.EscapeDataString(buildId);

        using var project = await GetAsync($"api/project/{id}", "the colonisation planner's project", cancellationToken)
            .ConfigureAwait(false);

        using var carriers = await GetAsync($"api/project/{id}/fc", "the colonisation planner's carriers", cancellationToken)
            .ConfigureAwait(false);

        return RavenColonialResponse.ReadProject(site, project!, carriers);
    }

    private async Task<JsonDocument?> GetAsync(
        string path,
        string what,
        CancellationToken cancellationToken,
        bool missing = false)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_budget);

        try
        {
            using var response = await _http.GetAsync(path, deadline.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("{What} answered {Status}", what, (int)response.StatusCode);

                // The planner answers 400 for a system it does not know and 404 for one it has no record of.
                if (missing && response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
                {
                    return null;
                }

                throw new ColonisationPlanUnavailableException(response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests => "The colonisation planner is rate limiting me. It should clear shortly.",
                    >= HttpStatusCode.InternalServerError =>
                        $"{Capitalise(what)} reported a server error. It should clear shortly.",
                    _ => $"{Capitalise(what)} refused that request.",
                });
            }

            var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);

            return await JsonDocument.ParseAsync(stream, cancellationToken: deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("{What} did not answer within the timeout", what);
            throw new ColonisationPlanUnavailableException($"{Capitalise(what)} took too long to answer.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "{What} could not be reached", what);
            throw new ColonisationPlanUnavailableException($"I couldn't reach {what} — check the network connection.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "{What} answered with something that was not JSON", what);
            throw new ColonisationPlanUnavailableException($"{Capitalise(what)} answered with something I couldn't read.");
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
