namespace D47.Core.Listening;

/// <summary>One way of turning speech into words, and what using it sends.</summary>
public sealed record SttProviderInfo
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>How the provider row labels it.</summary>
    public required string Label { get; init; }

    /// <summary>The one model a hosted provider is asked for, or null for the local provider.</summary>
    public string? Model { get; init; }

    /// <summary>The secret store name for this provider's key, or null if it needs none.</summary>
    public string? KeySecretName { get; init; }

    /// <summary>Where the audio goes, for the disclosure's one-line form.</summary>
    public required string Destination { get; init; }

    /// <summary>Exactly what leaves the machine when this provider transcribes.</summary>
    public required string Egress { get; init; }

    /// <summary>Whether the audio leaves this machine.</summary>
    public bool Hosted => Id != SttProviderCatalog.LocalId;
}

/// <summary>The hearing providers d47 ships.</summary>
public static class SttProviderCatalog
{
    public const string LocalId = "local";

    public const string GroqId = "groq";

    public const string OpenAiId = "openai";

    private const string HostedEgress =
        "The audio of every utterance D47 transcribes is sent to {0}, along with your API key and the "
        + "names from your journal used to recognise proper nouns: systems, stations, bodies, your ships "
        + "and the route ahead. In push-to-talk that is what you held the key for. Hands free it is every "
        + "stretch the microphone judged to be speech, including ones not addressed to D47, because "
        + "whether you said its name is decided from the words after they come back. No journal files, "
        + "game state or other keys are sent, and nothing is sent until a key is stored.";

    public static SttProviderInfo Local { get; } = new()
    {
        Id = LocalId,
        Name = "This computer",
        Label = "This computer (Whisper)",
        Destination = "nothing sent",
        Egress = "Speech is turned into words by a Whisper model running on this machine. No audio and "
                 + "no transcript leaves it.",
    };

    public static SttProviderInfo Groq { get; } = new()
    {
        Id = GroqId,
        Name = "Groq",
        Label = "Groq (paid — needs a key)",
        Model = "whisper-large-v3-turbo",
        KeySecretName = "groq.apiKey",
        Destination = "api.groq.com",
        Egress = string.Format(System.Globalization.CultureInfo.InvariantCulture, HostedEgress, "Groq"),
    };

    public static SttProviderInfo OpenAi { get; } = new()
    {
        Id = OpenAiId,
        Name = "OpenAI",
        Label = "OpenAI (paid — needs a key)",
        Model = "gpt-4o-mini-transcribe",

        // The same secret the language model and the OpenAI voice read.
        KeySecretName = "openai.apiKey",
        Destination = "api.openai.com",
        Egress = string.Format(System.Globalization.CultureInfo.InvariantCulture, HostedEgress, "OpenAI"),
    };

    public static IReadOnlyList<SttProviderInfo> All { get; } = [Local, Groq, OpenAi];

    public static IReadOnlyList<string> Ids { get; } = [.. All.Select(provider => provider.Id)];

    /// <summary>The provider a stored id names, or the local one for anything unrecognised.</summary>
    public static SttProviderInfo Selected(string? id) =>
        All.FirstOrDefault(provider => string.Equals(provider.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? Local;

    /// <summary>What d47 says when a press finds the selected provider with no key.</summary>
    public static string NoKey(SttProviderInfo provider) =>
        $"{provider.Name} needs an API key. Add it in Settings.";

    /// <summary>What d47 says when a hosted provider gave no words.</summary>
    public static string Problem(TranscriptionUnavailableException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        var name = failure.Provider;

        return failure.Reason switch
        {
            TranscriptionFailure.Unreachable => $"I couldn't reach {name}. Say it again, or type it.",
            TranscriptionFailure.KeyRejected => $"{name} refused the key. Check it in Settings.",
            TranscriptionFailure.RateLimited => $"{name} is limiting requests. Try again in a moment.",
            _ => $"{name} couldn't transcribe that: {Spoken(failure.Detail ?? failure.Message)}.",
        };
    }

    /// <summary>The longest service message d47 reads out.</summary>
    private const int SpokenDetailCharacters = 160;

    /// <summary>A service's message cut to one line of bounded length, without its closing full stop.</summary>
    private static string Spoken(string detail)
    {
        var line = string.Join(' ', detail.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (line.Length > SpokenDetailCharacters)
        {
            line = line[..SpokenDetailCharacters].TrimEnd() + "…";
        }

        return line.TrimEnd('.');
    }
}
