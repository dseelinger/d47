using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Llm.OpenAi;

namespace D47.Llm;

/// <summary>Tries a language-model key by listing the provider's models.</summary>
public static class LlmKeyCheck
{
    public static SecretCheck Verdict(LlmProviderInfo provider, EndpointModels asked)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(asked);

        return asked.Reach switch
        {
            EndpointReach.Refused => SecretCheck.Rejected(asked.Detail ?? $"{provider.Name} refused the request."),

            EndpointReach.Answered when asked.Ids.Count > 0 => SecretCheck.Works(
                $"{provider.Name} answered — {asked.Ids.Count} models."),

            // An empty catalogue is a gateway's choice, not a fault.
            EndpointReach.Answered => SecretCheck.Works(
                $"{provider.Name} answered, but lists no models. Type the model name yourself."),

            _ => SecretCheck.Unreachable(asked.Detail ?? $"{provider.Name} could not be reached."),
        };
    }

    /// <summary>Lists models only: free, and no completion is run, so nothing is added to Spend.</summary>
    public static async Task<SecretCheck> RunAsync(
        LlmProviderInfo provider,
        string? key,
        string? endpoint,
        TimeSpan budget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var client = LlmProviderFactory.Create(provider, key, endpoint);

        if (client is null)
        {
            return SecretCheck.Unreachable(LlmProviderFactory.ReasonForNoClient(provider));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(budget);

        var lister = LlmProviderFactory.ModelLister(client);
        var asked = lister is null
            ? EndpointModels.Unreachable(null)
            : await lister(timeout.Token).ConfigureAwait(false);

        return Verdict(provider, asked);
    }
}
