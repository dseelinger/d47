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

    /// <summary>The <see cref="Configuration.D47Settings.StoryVoices"/> key of the member, when the Commander chose this voice.</summary>
    public string? Key { get; init; }

    /// <summary>The voice the story pinned, spoken when the chosen one fails; null when this is the pinned voice.</summary>
    public PinnedVoice? Fallback { get; init; }
}

/// <summary>
/// A chosen cast voice that speaks a sentence in the story's pinned voice when the chosen provider throws a
/// <see cref="TtsException"/>, and reports why.
/// </summary>
public sealed class FallingBackTtsProvider(ITtsProvider chosen, ITtsProvider pinned, string pinnedVoice, Action<string> failed)
    : ITtsProvider
{
    /// <summary>Whether every sentence goes to the pinned voice without asking the chosen one.</summary>
    public bool AlwaysFallBack { get; init; }

    public string Id => chosen.Id;

    public string Name => chosen.Name;

    public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
        chosen.ListVoicesAsync(cancellationToken);

    public string Billable(string text) => chosen.Billable(text);

    public string? Phonemes(string text, VoiceSelection voice) => chosen.Phonemes(text, voice);

    public bool ReadsAudioTags => chosen.ReadsAudioTags;

    public bool Performs(string tag) => chosen.Performs(tag);

    public int GroupsSentencesUpTo => chosen.GroupsSentencesUpTo;

    public async Task<AudioClip> SynthesizeAsync(string text, VoiceSelection voice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(voice);

        if (AlwaysFallBack)
        {
            return await pinned.SynthesizeAsync(text, voice with { VoiceId = pinnedVoice, Name = null }, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return await chosen.SynthesizeAsync(text, voice, cancellationToken).ConfigureAwait(false);
        }
        catch (TtsException ex)
        {
            failed(ex.Message);
            return await pinned.SynthesizeAsync(text, voice with { VoiceId = pinnedVoice, Name = null }, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<ArrivingClip> StreamAsync(string text, VoiceSelection voice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(voice);

        if (AlwaysFallBack)
        {
            return await pinned.StreamAsync(text, voice with { VoiceId = pinnedVoice, Name = null }, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return await chosen.StreamAsync(text, voice, cancellationToken).ConfigureAwait(false);
        }
        catch (TtsException ex)
        {
            failed(ex.Message);
            return await pinned.StreamAsync(text, voice with { VoiceId = pinnedVoice, Name = null }, cancellationToken).ConfigureAwait(false);
        }
    }
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

    /// <summary><see cref="Treatment(PinnedVoice)"/> as a running filter factory, null in the same cases.</summary>
    public static Func<IPcmFilter>? Running(PinnedVoice pinned)
    {
        ArgumentNullException.ThrowIfNull(pinned);

        var effects = Ticked(pinned.Effects);
        var link = pinned.Link;

        if (effects.Count == 0)
        {
            return link is { } alone ? () => RadioVoice.Filter(alone, overheard: false) : null;
        }

        var basePitch = GuardianVoice.BasePitchHz(VoiceGender.Unspecified);

        return () =>
        {
            var guardian = GuardianVoice.Filter(effects, basePitch);
            return link is { } strength ? new SeriesPcmFilter(guardian, RadioVoice.Filter(strength, overheard: false)) : guardian;
        };
    }

    private static Func<AudioClip, AudioClip>? Treatment(double? link, IReadOnlyList<StorySpeakerEffect>? listed)
    {
        var effects = Ticked(listed);

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

    private static List<GuardianVoiceEffect> Ticked(IReadOnlyList<StorySpeakerEffect>? listed) =>
        [.. (listed ?? []).Select(effect => new GuardianVoiceEffect { Id = effect.Id, Ticked = true, Level = effect.Level })];
}
