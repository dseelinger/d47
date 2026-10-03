using D47.Core.Audio;
using D47.Core.Configuration;
using VoiceGender = D47.Core.Persona.VoiceGender;

namespace D47.Core.Stories;

/// <summary>The sound a story gives a cast member: Guardian effects, then a comms link.</summary>
public static class CastVoice
{
    /// <summary>The treatment for a member's voice, or null when it has no link and no effects. The Commander's Guardian Voice settings play no part.</summary>
    public static Func<AudioClip, AudioClip>? Treatment(StorySpeaker speaker)
    {
        var effects = (speaker.Effects ?? [])
            .Select(effect => new GuardianVoiceEffect { Id = effect.Id, Ticked = true, Level = effect.Level })
            .ToList();

        if (effects.Count == 0 && speaker.Link is null)
        {
            return null;
        }

        var basePitch = GuardianVoice.BasePitchHz(VoiceGender.Unspecified);

        return clip =>
        {
            clip = GuardianVoice.Apply(clip, effects, basePitch);
            return speaker.Link is { } link ? RadioVoice.Apply(clip, link) : clip;
        };
    }
}
