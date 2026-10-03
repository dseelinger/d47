using D47.Core.Audio;
using D47.Core.Configuration;
using VoiceGender = D47.Core.Persona.VoiceGender;

namespace D47.Core.Stories;

/// <summary>
/// A story line's own voice: the provider and voice that speak it whatever the slot settings say, and the sound the
/// story gives its speaker.
/// </summary>
public sealed record PinnedVoice(string ProviderId, string VoiceId)
{
    /// <summary>Signal strength, 0 to 1, of the comms link the speaker is heard through; null for none.</summary>
    public double? Link { get; init; }

    /// <summary>Guardian effects the speaker's voice passes through, in order.</summary>
    public IReadOnlyList<StorySpeakerEffect>? Effects { get; init; }
}

/// <summary>The sound a story gives a cast member: Guardian effects, then a comms link.</summary>
public static class CastVoice
{
    /// <summary>The treatment for a member's voice, or null when it has no link and no effects. The Commander's Guardian Voice settings play no part.</summary>
    public static Func<AudioClip, AudioClip>? Treatment(StorySpeaker speaker)
    {
        ArgumentNullException.ThrowIfNull(speaker);

        return Treatment(speaker.Link, speaker.Effects);
    }

    /// <summary>The treatment a pinned voice carries, or null when it has no link and no effects.</summary>
    public static Func<AudioClip, AudioClip>? Treatment(PinnedVoice pinned)
    {
        ArgumentNullException.ThrowIfNull(pinned);

        return Treatment(pinned.Link, pinned.Effects);
    }

    private static Func<AudioClip, AudioClip>? Treatment(double? link, IReadOnlyList<StorySpeakerEffect>? listed)
    {
        var effects = (listed ?? [])
            .Select(effect => new GuardianVoiceEffect { Id = effect.Id, Ticked = true, Level = effect.Level })
            .ToList();

        if (effects.Count == 0 && link is null)
        {
            return null;
        }

        var basePitch = GuardianVoice.BasePitchHz(VoiceGender.Unspecified);

        return clip =>
        {
            clip = GuardianVoice.Apply(clip, effects, basePitch);
            return link is { } strength ? RadioVoice.Apply(clip, strength) : clip;
        };
    }
}
