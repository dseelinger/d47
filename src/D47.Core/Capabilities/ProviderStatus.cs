using D47.Core.Audio;
using D47.Core.Listening;

namespace D47.Core.Capabilities;

/// <summary>The status line under a hearing or voice provider: where it runs, and whether it needs a key it has.</summary>
public static class ProviderStatus
{
    public static ChoiceStatus Of(SttProviderInfo provider, Func<string, bool> keyStored) =>
        Of(provider.KeySecretName, onThisComputer: !provider.Hosted, keyStored);

    /// <summary>Null for None, which speaks through nothing.</summary>
    public static ChoiceStatus? Of(TtsProviderInfo provider, Func<string, bool> keyStored) =>
        provider.Speaks
            ? Of(provider.KeySecretName, onThisComputer: provider.Id == TtsProviderCatalog.KokoroId, keyStored)
            : null;

    private static ChoiceStatus Of(string? keySecretName, bool onThisComputer, Func<string, bool> keyStored) =>
        keySecretName is not null
            ? keyStored(keySecretName)
                ? new ChoiceStatus("PAID · KEY STORED", ChoiceTone.Yellow)
                : new ChoiceStatus("PAID · NEEDS KEY", ChoiceTone.Grey)
            : new ChoiceStatus(onThisComputer ? "THIS COMPUTER · FREE" : "FREE", ChoiceTone.Grey);
}
