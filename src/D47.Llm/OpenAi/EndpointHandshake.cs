using System.Text.Json;

namespace D47.Llm.OpenAi;

/// <summary>
/// What an endpoint says it serves, and whether it answered at all (Phase 29, "What the endpoint can
/// actually do").
/// </summary>
public enum EndpointReach
{
    /// <summary>Nothing answered.</summary>
    Unreachable,

    /// <summary>Something answered and refused.</summary>
    Refused,

    /// <summary>Something answered and spoke the protocol.</summary>
    Answered,
}

/// <summary>What the handshake found.</summary>
public sealed record EndpointModels(EndpointReach Reach, IReadOnlyList<string> Ids, string? Detail)
{
    public static EndpointModels Unreachable(string? detail) => new(EndpointReach.Unreachable, [], detail);

    public static EndpointModels Refused(string? detail) => new(EndpointReach.Refused, [], detail);
}

/// <summary>Asking an endpoint what it is, before a turn depends on the answer.</summary>
internal static class EndpointHandshake
{
    /// <summary>A handshake is not a turn.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(10);

    public static async Task<EndpointModels> ListModelsAsync(
        OpenAiEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(Budget);

        HttpResponseMessage response;

        try
        {
            response = await endpoint.GetAsync("/models", budget.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return EndpointModels.Unreachable(endpoint.DescribeUnreachable(ex));
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var (message, transient) = endpoint.Describe(response.StatusCode, detail: null);

                // A refusal is the endpoint working and disagreeing; a 5xx or a rate limit is the endpoint
                // not answering yet.
                return transient ? EndpointModels.Unreachable(message) : EndpointModels.Refused(message);
            }

            string body;

            try
            {
                body = await response.Content.ReadAsStringAsync(budget.Token).ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                return EndpointModels.Unreachable("The endpoint answered but the reply could not be read.");
            }

            return Read(body);
        }
    }

    /// <summary>The ids out of a model list, sorted so the picker does not reshuffle between visits.</summary>
    internal static EndpointModels Read(string body)
    {
        var ids = new List<string>();

        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in data.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.Object
                        && entry.TryGetProperty("id", out var id)
                        && id.ValueKind == JsonValueKind.String
                        && id.GetString() is { Length: > 0 } value)
                    {
                        ids.Add(value);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Something is there and it is not speaking this protocol.
            return new EndpointModels(
                EndpointReach.Refused,
                [],
                "The endpoint answered, but not with an OpenAI-shaped model list. Check the address.");
        }

        ids.Sort(StringComparer.OrdinalIgnoreCase);

        return new EndpointModels(EndpointReach.Answered, ids, null);
    }
}
