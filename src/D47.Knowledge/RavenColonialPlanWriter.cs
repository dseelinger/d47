using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Knowledge;

/// <summary><see cref="IColonisationPlanWriter"/> against Raven Colonial's API.</summary>
public sealed class RavenColonialPlanWriter : IColonisationPlanWriter, IDisposable
{
    private const string NoKey = "No Raven Colonial key is set.";

    private readonly TimeSpan _budget = TimeSpan.FromSeconds(15);

    private readonly HttpClient _http;

    private readonly Func<string?> _key;

    private readonly bool _ownsClient;

    public RavenColonialPlanWriter(Func<string?> key, HttpClient? http = null)
    {
        _key = key;
        _ownsClient = http is null;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        _http.BaseAddress ??= new Uri($"https://{RavenColonialPlanService.Host}/");

        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47/0.1 (+https://github.com/dseelinger/d47)");
        }
    }

    public async Task<PlanCommander> CommanderAsync(CancellationToken cancellationToken)
    {
        var (result, body) = await SendAsync(HttpMethod.Get, "api/cmdr/", null, cancellationToken).ConfigureAwait(false);

        if (result.Outcome != PlanWriteOutcome.Sent)
        {
            return new PlanCommander(result.Outcome, null);
        }

        return ReadName(body) is { } name
            ? new PlanCommander(PlanWriteOutcome.Sent, name)
            : new PlanCommander(PlanWriteOutcome.Failed, null);
    }

    public async Task<PlanWriteResult> NeedsAsync(
        string buildId,
        IReadOnlyDictionary<string, int> remaining,
        int maxNeed,
        CancellationToken cancellationToken) =>
        (await SendAsync(
            HttpMethod.Post,
            $"api/project/{Uri.EscapeDataString(buildId)}",
            new Dictionary<string, object> { ["buildId"] = buildId, ["commodities"] = Fold(remaining), ["maxNeed"] = maxNeed },
            cancellationToken).ConfigureAwait(false)).Result;

    public async Task<PlanWriteResult> ContributeAsync(
        string buildId,
        string commander,
        IReadOnlyDictionary<string, int> delivered,
        CancellationToken cancellationToken) =>
        (await SendAsync(
            HttpMethod.Post,
            $"api/project/{Uri.EscapeDataString(buildId)}/contribute/{Uri.EscapeDataString(commander)}",
            Fold(delivered),
            cancellationToken).ConfigureAwait(false)).Result;

    public async Task<PlanWriteResult> CompleteAsync(string buildId, CancellationToken cancellationToken) =>
        (await SendAsync(
            HttpMethod.Post, $"api/project/{Uri.EscapeDataString(buildId)}/complete", null, cancellationToken)
            .ConfigureAwait(false)).Result;

    public async Task<PlanWriteResult> CarrierRegisteredAsync(long marketId, CancellationToken cancellationToken) =>
        (await SendAsync(HttpMethod.Get, $"api/FC/{marketId}", null, cancellationToken).ConfigureAwait(false)).Result;

    public async Task<PlanWriteResult> CarrierMovedAsync(
        long marketId,
        IReadOnlyDictionary<string, int> moved,
        CancellationToken cancellationToken) =>
        (await SendAsync(HttpMethod.Patch, $"api/FC/{marketId}/cargo", Fold(moved), cancellationToken).ConfigureAwait(false)).Result;

    private static Dictionary<string, int> Fold(IReadOnlyDictionary<string, int> tonnes)
    {
        var folded = new Dictionary<string, int>();

        foreach (var (name, count) in tonnes)
        {
            if (JournalJson.Symbol(name) is { } symbol)
            {
                folded[symbol] = folded.GetValueOrDefault(symbol) + count;
            }
        }

        return folded;
    }

    private static string? ReadName(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.String)
            {
                return root.GetString() is { Length: > 0 } text ? text : null;
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in new[] { "cmdr", "name", "commander" })
                {
                    if (root.TryGetProperty(property, out var value)
                        && value.ValueKind == JsonValueKind.String
                        && value.GetString() is { Length: > 0 } name)
                    {
                        return name;
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private async Task<(PlanWriteResult Result, string? Body)> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        if (_key() is not { Length: > 0 } key)
        {
            return (new PlanWriteResult(PlanWriteOutcome.KeyRefused, NoKey), null);
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_budget);

        try
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.TryAddWithoutValidation("rcc-key", key);

            if (body is not null)
            {
                request.Content = JsonContent.Create(body, body.GetType());
            }

            using var response = await _http.SendAsync(request, deadline.Token).ConfigureAwait(false);
            var status = $"{(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd();

            var outcome = response.StatusCode switch
            {
                _ when response.IsSuccessStatusCode => PlanWriteOutcome.Sent,
                HttpStatusCode.NotFound => PlanWriteOutcome.NotOnPlanner,
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => PlanWriteOutcome.KeyRefused,
                _ => PlanWriteOutcome.Failed,
            };

            var text = outcome == PlanWriteOutcome.Sent
                ? await response.Content.ReadAsStringAsync(deadline.Token).ConfigureAwait(false)
                : null;

            return (new PlanWriteResult(outcome, status), text);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return (new PlanWriteResult(PlanWriteOutcome.Failed, "Timed out."), null);
        }
        catch (HttpRequestException)
        {
            return (new PlanWriteResult(PlanWriteOutcome.Failed, "Could not be reached."), null);
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
