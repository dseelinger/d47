using System.Collections.Concurrent;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Catalog;
using D47.Core.Configuration;
using D47.Core.Persona;
using D47.Tts;

namespace D47.App.Voice;

/// <summary>Plays voices so they can be judged before they are chosen, and caches the clips already paid for.</summary>
internal sealed class VoiceAuditions
{
    /// <summary>The group auditions play in, so a second one drops the first mid-word.</summary>
    internal const string Group = "voice-audition";

    private const string StandInSaid =
        "That was a stand-in voice, not the one you have chosen — nothing free was available to test with.";

    private const string CovasStandInSaid = "That was a stand-in voice through the COVAS reverb.";

    private readonly ConcurrentDictionary<(string Provider, string Voice), AudioClip> _auditions = new();
    private readonly SpeechClients _speech;
    private readonly AudioArbiter _audio;
    private readonly SettingsService _settings;
    private readonly PersonaHost _personas;

    internal VoiceAuditions(SpeechClients speech, AudioArbiter audio, SettingsService settings, PersonaHost personas)
    {
        _speech = speech;
        _audio = audio;
        _settings = settings;
        _personas = personas;
    }

    /// <summary>Speaks one voice through the slot the role belongs to, billed to that slot.</summary>
    internal async Task AuditionVoiceAsync(string voiceId, VoiceRole role, CancellationToken cancellationToken)
    {
        var group = VoiceGroups.Of(role);

        if (_speech.Speaker(group) is not { } provider)
        {
            throw new InvalidOperationException("No voice provider is selected.");
        }

        // Dropped before the synthesis, so a second press silences the first while the second is fetched.
        _audio.DropGroup(Group);

        var key = (provider.Id, $"{role}:{voiceId}");

        if (!_auditions.TryGetValue(key, out var clip))
        {
            if (LocalVoiceWithEdgeStandIn.Unwrapped(_speech.ClientFor(provider.Id)) is ChatterboxTtsProvider chatterbox
                && !await chatterbox.FetchAsync(voiceId, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException(
                    $"That Chatterbox voice could not be fetched from {ChatterboxCatalog.Host}. Press Play to try again.");
            }

            clip = await provider.SynthesizeAsync(
                role == VoiceRole.ShipAi ? AuditionLine.For(_personas.Current) : AuditionLine.For(role),
                new VoiceSelection(
                    voiceId,
                    SpeechCapability.RateFor(
                        _settings.Current,
                        VoiceGroups.ProviderFor(_settings.Current.Speech, group))),
                cancellationToken).ConfigureAwait(false);

            // Cached after the await, so a cancelled or failed synthesis caches nothing.
            _auditions[key] = clip;
        }

        cancellationToken.ThrowIfCancellationRequested();

        _audio.Enqueue(new AudioRequest
        {
            Channel = AudioChannel.Speech,
            Clip = clip,
            Group = Group,
            Caption = clip.Name,
        });
    }

    /// <summary>Plays a voice's free sample from its provider, which bills nothing. The sample carries no caption.</summary>
    internal async Task AuditionPreviewAsync(string voiceId, VoiceRole role, CancellationToken cancellationToken)
    {
        if (_speech.Speaker(VoiceGroups.Of(role)) is not { } provider)
        {
            throw new InvalidOperationException("No voice provider is selected.");
        }

        _audio.DropGroup(Group);

        var key = (provider.Id, $"sample:{voiceId}");

        if (!_auditions.TryGetValue(key, out var clip))
        {
            clip = await provider.PreviewAsync(voiceId, cancellationToken).ConfigureAwait(false)
                   ?? throw new InvalidOperationException($"{provider.Name} has no free sample of that voice.");

            _auditions[key] = clip;
        }

        cancellationToken.ThrowIfCancellationRequested();

        _audio.Enqueue(new AudioRequest
        {
            Channel = AudioChannel.Speech,
            Clip = clip,
            Group = Group,
        });
    }

    /// <summary>
    /// Plays the Guardian voice treatments currently toggled, on a clip chosen so nothing is billed
    /// (<see cref="GuardianVoiceTest.SourceFor"/>). Returns what the row says when a stand-in played.
    /// </summary>
    internal async Task<string?> GuardianTestAsync(CancellationToken cancellationToken)
    {
        var speech = _settings.Current.Speech;
        var voiceId = SpeechCapability.ShipVoiceFor(_settings.Current, _personas.Current.Id) ?? string.Empty;
        var providerId = VoiceGroups.ProviderFor(speech, VoiceGroup.Aboard);
        var providerInfo = TtsProviderCatalog.Selected(providerId);

        _audio.DropGroup(Group);

        var auditionKey = (providerInfo.Id, $"{VoiceRole.ShipAi}:{voiceId}");
        var sampleKey = (providerInfo.Id, $"sample:{voiceId}");
        var hasFreeSample = providerInfo.OffersFreePreviews && _speech.HasPreviewFor(VoiceGroup.Aboard, voiceId);

        var source = GuardianVoiceTest.SourceFor(
            providerInfo, hasFreeSample, _auditions.ContainsKey(auditionKey));

        // The speaker is read once. It is null where the provider needs a key that is not set, and Test
        // then plays the stand-in rather than failing.
        var speaker = _speech.Speaker(VoiceGroup.Aboard);

        if (source is GuardianVoiceTest.Source.Synthesize or GuardianVoiceTest.Source.FreeSample
            && speaker is null)
        {
            source = GuardianVoiceTest.Source.StandIn;
        }

        // A Chatterbox voice whose clip cannot be fetched is not synthesised and cached under its name.
        if (source is GuardianVoiceTest.Source.Synthesize
            && !_auditions.ContainsKey(auditionKey)
            && LocalVoiceWithEdgeStandIn.Unwrapped(_speech.ClientFor(providerInfo.Id)) is ChatterboxTtsProvider chatterbox
            && !await chatterbox.FetchAsync(voiceId, cancellationToken).ConfigureAwait(false))
        {
            source = GuardianVoiceTest.Source.StandIn;
        }

        AudioClip clip;
        string? said = null;

        switch (source)
        {
            case GuardianVoiceTest.Source.Synthesize:
                if (!_auditions.TryGetValue(auditionKey, out var synthesized))
                {
                    synthesized = await speaker!.SynthesizeAsync(
                        AuditionLine.For(_personas.Current),
                        new VoiceSelection(voiceId, SpeechCapability.RateFor(_settings.Current, providerId)),
                        cancellationToken).ConfigureAwait(false);

                    _auditions[auditionKey] = synthesized;
                }

                clip = synthesized;
                break;

            case GuardianVoiceTest.Source.FreeSample:
                if (!_auditions.TryGetValue(sampleKey, out var sampled))
                {
                    sampled = await speaker!.PreviewAsync(voiceId, cancellationToken).ConfigureAwait(false)
                              ?? throw new InvalidOperationException(
                                  $"{providerInfo.Name} has no free sample of that voice.");

                    _auditions[sampleKey] = sampled;
                }

                clip = sampled;
                break;

            case GuardianVoiceTest.Source.CachedAudition:
                clip = _auditions[auditionKey];
                break;

            default:
                clip = StandInVoice.Clip;
                said = StandInSaid;
                break;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var colour = GuardianVoice.ColourFor(speech, _personas.Current.VoiceHint.Gender);

        _audio.Enqueue(new AudioRequest
        {
            Channel = AudioChannel.Speech,
            Clip = colour is null ? clip : colour(clip),
            Group = Group,
            Caption = clip.Name,
        });

        return said;
    }

    /// <summary>Plays the bundled stand-in through the COVAS reverb, which bills nothing.</summary>
    internal string CovasTest()
    {
        _audio.DropGroup(Group);

        _audio.Enqueue(new AudioRequest
        {
            Channel = AudioChannel.Speech,
            Clip = CovasVoice.Apply(StandInVoice.Clip),
            Group = Group,
            Caption = StandInVoice.Clip.Name,
        });

        return CovasStandInSaid;
    }
}
