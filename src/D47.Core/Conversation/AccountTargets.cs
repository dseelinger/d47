using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Listening;

namespace D47.Core.Conversation;

/// <summary>Which providers' accounts the current settings use.</summary>
public static class AccountTargets
{
    /// <summary>
    /// The providers to ask, ElevenLabs first. Voices resolve as the egress disclosure resolves them, so the two
    /// agree on what is in use.
    /// </summary>
    public static IReadOnlyList<AccountProvider> InUse(D47Settings settings, IReadOnlyList<RunningStoryVoice> runningStory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(runningStory);

        var hearing = SttProviderCatalog.Selected(settings.Listening.Provider).Id;

        var elevenLabs =
            EgressDisclosure.SpeakingSlots(settings).Any(pair => pair.Provider.Id == TtsProviderCatalog.ElevenLabsId)
            || EgressDisclosure.StoryCast(settings, runningStory).Any(line => line.Provider.Id == TtsProviderCatalog.ElevenLabsId)
            || hearing == SttProviderCatalog.ElevenLabsId;

        var inUse = new List<AccountProvider>(2);

        if (elevenLabs)
        {
            inUse.Add(AccountProvider.ElevenLabs);
        }

        if (hearing == SttProviderCatalog.DeepgramId)
        {
            inUse.Add(AccountProvider.Deepgram);
        }

        return inUse;
    }
}
