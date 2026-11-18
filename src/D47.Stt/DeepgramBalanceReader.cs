using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using D47.Core.Conversation;

namespace D47.Stt;

/// <summary>Reads the credit left on each project the Deepgram key reaches.</summary>
public sealed class DeepgramBalanceReader : IAccountReader
{
    private const string Projects = "https://api.deepgram.com/v1/projects";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly Func<string?> _key;
    private readonly HttpClient _http;

    /// <param name="key">Asked for at each read, so a key added or replaced later is used.</param>
    public DeepgramBalanceReader(Func<string?> key, HttpClient? http = null)
    {
        _key = key;
        _http = http ?? new HttpClient();
    }

    public AccountProvider Provider => AccountProvider.Deepgram;

    public async Task<AccountReading> ReadAsync(CancellationToken cancellationToken)
    {
        if (_key() is not { Length: > 0 } key)
        {
            return new AccountReading.NoKey();
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(Timeout);

        try
        {
            var listed = await GetAsync(Projects, key, limit.Token).ConfigureAwait(false);

            if (listed.Failure is { } failed)
            {
                return failed;
            }

            if (ProjectsIn(listed.Body!) is not { } projects)
            {
                return Unreadable;
            }

            var found = new List<DeepgramBalance>();

            foreach (var (id, name) in projects)
            {
                var answer = await GetAsync($"{Projects}/{Uri.EscapeDataString(id)}/balances", key, limit.Token).ConfigureAwait(false);

                if (answer.Failure is { } refused)
                {
                    return refused;
                }

                if (BalancesIn(name, answer.Body!) is not { } balances)
                {
                    return Unreadable;
                }

                found.AddRange(balances);
            }

            return new DeepgramCredit(found);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AccountReading.Unreachable("Deepgram did not answer in time");
        }
        catch (HttpRequestException exception)
        {
            return new AccountReading.Unreachable(exception.Message);
        }
    }

    private static AccountReading.Unreachable Unreadable => new("the reply could not be read");

    private async Task<(string? Body, AccountReading? Failure)> GetAsync(string url, string key, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", key);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return (null, new AccountReading.Refused((int)response.StatusCode, DeepgramTranscriber.MessageFromBody(body)));
        }

        return response.IsSuccessStatusCode
            ? (body, null)
            : (null, new AccountReading.Unreachable($"Deepgram answered {(int)response.StatusCode}"));
    }

    private static List<(string Id, string Name)>? ProjectsIn(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("projects", out var projects)
                || projects.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var found = new List<(string, string)>();

            foreach (var project in projects.EnumerateArray())
            {
                if (project.ValueKind != JsonValueKind.Object || Text(project, "project_id") is not { Length: > 0 } id)
                {
                    return null;
                }

                found.Add((id, Text(project, "name") is { Length: > 0 } name ? name : id));
            }

            return found;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<DeepgramBalance>? BalancesIn(string project, string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("balances", out var balances)
                || balances.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var sums = new Dictionary<string, decimal>();

            foreach (var balance in balances.EnumerateArray())
            {
                if (balance.ValueKind != JsonValueKind.Object
                    || !balance.TryGetProperty("amount", out var amount)
                    || amount.ValueKind != JsonValueKind.Number
                    || !amount.TryGetDecimal(out var value)
                    || Text(balance, "units") is not { Length: > 0 } units)
                {
                    return null;
                }

                sums[units] = sums.GetValueOrDefault(units) + value;
            }

            return [.. sums.Select(sum => new DeepgramBalance(project, sum.Value, sum.Key))];
        }
        catch (Exception exception) when (exception is JsonException or OverflowException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
}
