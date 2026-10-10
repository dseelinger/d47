using D47.Core.Audio;

namespace D47.Core.Speech;

/// <summary>Edge Neural speaks for a local voice whose model is not on disk.</summary>
public static class LocalVoiceStandIn
{
    /// <summary>The one Edge voice every speaker uses while it stands in.</summary>
    public const string EdgeVoice = "en-GB-SoniaNeural";

    private static volatile Func<string, bool> _installed = _ => true;

    /// <summary>Answers whether a local provider's model is on disk. The app sets it at startup.</summary>
    public static Func<string, bool> Installed
    {
        get => _installed;
        set => _installed = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Whether Edge speaks for this provider's lines right now.</summary>
    public static bool Applies(string? providerId) =>
        TtsProviderCatalog.IsLocal(providerId) && !_installed(providerId!);

    /// <summary>The provider as it behaves now: its disclosure names Edge while Edge stands in.</summary>
    public static TtsProviderInfo Live(TtsProviderInfo provider)
    {
        if (!Applies(provider.Id))
        {
            return provider;
        }

        var edge = TtsProviderCatalog.Edge;

        return provider with
        {
            Destination = edge.Destination,
            Egress = $"Until the {provider.Name} model is downloaded, the text D47 speaks goes to Microsoft "
                + $"({edge.Destination}) and is spoken by Edge Neural. {edge.Egress} Once the model is "
                + "downloaded, nothing is sent.",
        };
    }
}
