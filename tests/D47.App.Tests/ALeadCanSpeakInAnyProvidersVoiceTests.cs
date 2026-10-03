using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A story character the Commander moved to another provider speaks in that provider's voice, of either gender, with
/// the character's own treatment; a sentence the chosen provider fails is spoken in the story's pinned voice, and the
/// failure is reported against the character's key (#737).
/// </summary>
public sealed class ALeadCanSpeakInAnyProvidersVoiceTests
{
    private const string Key = "ride-along.juno";

    private sealed record Built(VoicePipeline Voice, List<AudioClip> Played, Recording Chosen, Recording Kokoro, List<(string Key, string Reason)> Failures);

    private static Built Build(bool chosenFails = false, bool chosenMissing = false)
    {
        var sink = new CollectingSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var edge = new Recording(TtsProviderCatalog.EdgeId);
        var chosen = new Recording(TtsProviderCatalog.ElevenLabsId, chosenFails);
        var kokoro = new Recording(TtsProviderCatalog.KokoroId);
        var failures = new List<(string, string)>();

        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            Tts = edge,
            SpeakerFor = _ => edge,
            PinnedFor = id => id switch
            {
                TtsProviderCatalog.KokoroId => kokoro,
                TtsProviderCatalog.ElevenLabsId when !chosenMissing => chosen,
                _ => null,
            },
            CastVoiceFailed = (key, reason) => failures.Add((key, reason)),
            CuesEnabled = false,
            BedEnabled = false,
        };

        return new Built(voice, sink.Played, chosen, kokoro, failures);
    }

    private static readonly PinnedVoice Juno = new(TtsProviderCatalog.ElevenLabsId, "a-male-voice")
    {
        Key = Key,
        Fallback = new PinnedVoice(StorySpeaker.Kokoro, "af_river"),
    };

    private static Announcement Line(string text, PinnedVoice pinned) =>
        new("story.clue.ride-along.0", text) { Voice = VoiceRole.Crew, Speaker = "Juno", Pinned = pinned };

    [Fact]
    public async Task AChosenVoiceIsSpokenByItsProvider()
    {
        var built = Build();

        var kept = await built.Voice.AnnounceAsync(Line("You did not see me.", Juno));

        Assert.Equal([("You did not see me.", "a-male-voice")], built.Chosen.Asked);
        Assert.Empty(built.Kokoro.Asked);
        Assert.Equal(TtsProviderCatalog.ElevenLabsId, kept!.Provider);
        Assert.Empty(built.Failures);
    }

    [Fact]
    public async Task AFailedSentenceIsSpokenInThePinnedVoiceAndTheReasonIsKept()
    {
        var built = Build(chosenFails: true);

        await built.Voice.AnnounceAsync(Line("You did not see me.", Juno));

        Assert.Equal([("You did not see me.", "af_river")], built.Kokoro.Asked);
        Assert.Single(built.Played);
        Assert.Equal([(Key, "The key was refused.")], built.Failures);
    }

    [Fact]
    public async Task AChosenProviderWithNoClientSpeaksInThePinnedVoice()
    {
        var built = Build(chosenMissing: true);

        await built.Voice.AnnounceAsync(Line("Still here.", Juno));

        Assert.Equal([("Still here.", "af_river")], built.Kokoro.Asked);
        Assert.Equal(Key, Assert.Single(built.Failures).Key);
    }

    [Fact]
    public async Task AChosenVoiceKeepsTheCharactersLink()
    {
        var built = Build();

        await built.Voice.AnnounceAsync(Line("Juno here.", Juno with { Link = 0.5 }));

        Assert.Equal("Juno here. (radio)", Assert.Single(built.Played).Name);
    }

    /// <summary>Synthesis with the network taken out, keeping what it was asked to say and in which voice.</summary>
    private sealed class Recording(string id, bool fails = false) : ITtsProvider
    {
        public List<(string Text, string? Voice)> Asked { get; } = [];

        public string Id => id;

        public string Name => id;

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Of([]));

        public Task<AudioClip> SynthesizeAsync(string text, VoiceSelection voice, CancellationToken cancellationToken = default)
        {
            lock (Asked)
            {
                Asked.Add((text, voice.VoiceId));
            }

            if (fails)
            {
                throw new TtsException("The key was refused.", fault: TtsFault.KeyRejected);
            }

            const int Samples = 4_800;
            var pcm = new byte[Samples * 2];

            for (var index = 0; index < Samples; index++)
            {
                var value = (short)(Math.Sin(2 * Math.PI * 900 * index / 48_000.0) * 0.4 * short.MaxValue);

                pcm[index * 2] = (byte)(value & 0xFF);
                pcm[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
            }

            return Task.FromResult(new AudioClip(text, pcm, AudioFormat.Standard));
        }
    }

    /// <summary>Keeps what it was asked to play.</summary>
    private sealed class CollectingSink : IAudioSink
    {
        public List<AudioClip> Played { get; } = [];

        public event Action<long>? Finished;

        public IRenderReferenceTap ReferenceTap { get; } = new NoTap();

        public void Play(PlaybackRequest request)
        {
            Played.Add(request.Clip!);
            _ = Finished;
        }

        public void Stop(long playbackId)
        {
        }

        public void StopAll()
        {
        }

        public void Pause(long playbackId)
        {
        }

        public void Resume(long playbackId)
        {
        }

        public void SetGain(long playbackId, float gain)
        {
        }

        private sealed class NoTap : IRenderReferenceTap
        {
            public event Action<RenderReferenceFrame>? Rendered;

            public void Dispose() => _ = Rendered;
        }
    }
}
