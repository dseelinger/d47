using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Persona;
using Microsoft.Extensions.Logging;

namespace D47.Core.Tests.Persona;

/// <summary>Chooses a voice for every core not already paired, through <see cref="VoicePairing.ChooseForAsync"/>.</summary>
internal static class CoreVoiceChoice
{
    public static async Task<IReadOnlyDictionary<string, string>> ChooseAsync(
        IReadOnlyList<VoiceInfo> voices,
        IReadOnlyDictionary<string, string> existing,
        ILlmProvider? provider,
        string? model,
        SpendTracker? spend,
        PriceTable? prices,
        ILogger? logger,
        Random? random = null,
        CancellationToken cancellationToken = default)
    {
        var unpaired = VoicePairing.Cores.Where(slot => !existing.ContainsKey(slot.Id)).ToArray();

        var chosen = await VoicePairing.ChooseForAsync(
            voices, unpaired, existing.Values, provider, model, spend, prices, logger, random, cancellationToken);

        var paired = new Dictionary<string, string>(existing, StringComparer.Ordinal);

        foreach (var (id, voice) in chosen)
        {
            paired[id] = voice;
        }

        return paired;
    }
}
