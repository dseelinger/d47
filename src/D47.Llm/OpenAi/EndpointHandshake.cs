using System.Text.Json;

namespace D47.Llm.OpenAi;

/// <summary>
/// What an endpoint says it serves, and whether it answered at all (list.md Phase 29,
/// "What the endpoint can actually do").
/// <para>
/// The three states are kept apart on purpose, the same way the key check keeps <em>rejected</em>
/// and <em>unreachable</em> apart: telling a Commander their address is wrong when the machine is
/// simply not running yet sends them to edit a setting that was already correct.
/// </para>
/// </summary>
public enum EndpointReach
{
    /// <summary>Nothing answered. The address, the port, or a server that is not started.</summary>
    Unreachable,

    /// <summary>Something answered and refused. A key that is wrong, or missing where one is wanted.</summary>
    Refused,

    /// <summary>
    /// Something answered and spoke the protocol. The model list may still be empty — a gateway
    /// is entitled to keep its catalogue to itself — and that is a success, not a failure.
    /// </summary>
    Answered,
}

/// <summary>What the handshake found. <paramref name="Detail"/> is the endpoint's own words.</summary>
public sealed record EndpointModels(EndpointReach Reach, IReadOnlyList<string> Ids, string? Detail)
{
    public static EndpointModels Unreachable(string? detail) => new(EndpointReach.Unreachable, [], detail);

    public static EndpointModels Refused(string? detail) => new(EndpointReach.Refused, [], detail);
}

/// <summary>
/// Asking an endpoint what it is, before a turn depends on the answer.
/// <para>
/// <c>GET /models</c> does two jobs and neither is a guess. It confirms the thing at that address
/// <b>speaks the protocol at all</b> — which is what a keyless endpoint has instead of a key
/// check, since "is this key good" is not the question when there is no key — and it
/// <b>fills the model picker</b>, which is empty for a custom endpoint today by deliberate
/// design.
/// </para>
/// <para>
/// That design is not overturned here. <c>LlmProviderInfo.ModelsFor</c> still returns nothing for
/// an address d47 does not recognise, and it is still right to: a model id belongs to its
/// endpoint's namespace, and offering <c>claude-opus-5</c> to somebody else's gateway is a stale
/// selection waiting to fail at the first turn. What changes is that there is now somebody else
/// to ask, so the list becomes <em>the endpoint's own</em> rather than this provider's or nothing.
/// </para>
/// <para>
/// <b>What a model list cannot answer is learned from the first failure.</b> Whether tool calls
/// work, whether reasoning effort is accepted, whether usage comes back — none of that is in a
/// catalogue, and none of it is assumed. See <see cref="EndpointDemotions"/>.
/// </para>
/// </summary>
internal static class EndpointHandshake
{
    /// <summary>
    /// A handshake is not a turn. It has to fail quickly enough that the settings panel stays
    /// answerable, and a server that needs longer than this to list its own models is one the
    /// Commander wants to hear about rather than wait for.
    /// </summary>
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

                // A refusal is the endpoint working and disagreeing; a 5xx or a rate limit is the
                // endpoint not answering yet. The Commander needs different things from those.
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

    /// <summary>
    /// The ids out of a model list, sorted so the picker does not reshuffle between visits.
    /// <para>
    /// A reply that is not the documented shape counts as <em>answered with nothing</em> rather
    /// than as a failure. Something is at that address and it returned JSON; the picker degrades
    /// to free text, which it already supports, and the Commander types what they loaded.
    /// </para>
    /// </summary>
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
            // Something is there and it is not speaking this protocol. That is worth telling the
            // Commander plainly, because it is the one failure the address itself explains.
            return new EndpointModels(
                EndpointReach.Refused,
                [],
                "The endpoint answered, but not with an OpenAI-shaped model list. Check the address.");
        }

        ids.Sort(StringComparer.OrdinalIgnoreCase);

        return new EndpointModels(EndpointReach.Answered, ids, null);
    }
}
