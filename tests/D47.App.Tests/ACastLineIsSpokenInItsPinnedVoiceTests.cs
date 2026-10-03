using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A story line by a cast member is synthesised by the member's pinned provider and voice whatever the slots are on, and
/// is treated with the member's own link and effects and nothing else (#714).
/// </summary>
public sealed class ACastLineIsSpokenInItsPinnedVoiceTests
{
    private sealed record Built(VoicePipeline Voice, List<AudioClip> Played, Recording Aboard, Recording Npcs, Recording Kokoro);

    private static Built Build()
    {
        var sink = new CollectingSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var aboard = new Recording(TtsProviderCatalog.EdgeId);
        var npcs = new Recording(TtsProviderCatalog.EdgeId);
        var kokoro = new Recording(TtsProviderCatalog.KokoroId);

        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            Tts = aboard,
            SpeakerFor = group => group == VoiceGroup.Aboard ? aboard : npcs,
            PinnedFor = id => id == TtsProviderCatalog.KokoroId ? kokoro : null,
            Voice = new VoiceSelection("the-persona's-voice"),
            CuesEnabled = false,
            BedEnabled = false,
            GuardianColour = clip => clip with { Name = $"{clip.Name} (guardian)" },
        };

        return new Built(voice, sink.Played, aboard, npcs, kokoro);
    }

    private static Announcement CastLine(string text, PinnedVoice pinned) =>
        new("story.clue.the-test-story.0", text) { Voice = VoiceRole.Crew, Speaker = "Juno", Pinned = pinned };

    [Fact]
    public async Task AKokoroMemberIsSpokenByKokoroWhileEverySlotIsOnEdge()
    {
        var built = Build();

        var kept = await built.Voice.AnnounceAsync(CastLine("The beacon hums.", new PinnedVoice(StorySpeaker.Kokoro, "am_michael")));

        Assert.Equal([("The beacon hums.", "am_michael")], built.Kokoro.Asked);
        Assert.Empty(built.Aboard.Asked);
        Assert.Empty(built.Npcs.Asked);
        Assert.Equal(TtsProviderCatalog.KokoroId, kept!.Provider);
        Assert.Equal("am_michael", kept.VoiceId);
    }

    [Fact]
    public async Task AShipLineUsesTheAboardSlotAndThePersonasVoice()
    {
        var built = Build();

        await built.Voice.AnnounceAsync(new Announcement("story.clue.the-test-story.0", "The beacon hums."));

        Assert.Equal([("The beacon hums.", "the-persona's-voice")], built.Aboard.Asked);
        Assert.Empty(built.Kokoro.Asked);
    }

    [Fact]
    public async Task AMemberWithNoLinkOrEffectsIsUntreatedEvenWithTheGuardianVoiceOn()
    {
        var built = Build();

        await built.Voice.AnnounceAsync(CastLine("Juno here.", new PinnedVoice(StorySpeaker.Kokoro, "af_heart")));

        Assert.Equal("Juno here.", Assert.Single(built.Played).Name);
    }

    [Fact]
    public async Task AMemberWithALinkIsHeardThroughItAtItsStrength()
    {
        var harrow = Build();
        var teller = Build();

        await harrow.Voice.AnnounceAsync(CastLine("Harrow here.", new PinnedVoice(StorySpeaker.Kokoro, "bm_george") { Link = 0.3 }));
        await teller.Voice.AnnounceAsync(CastLine("Harrow here.", new PinnedVoice(StorySpeaker.Kokoro, "bm_george") { Link = 1 }));

        var weak = Assert.Single(harrow.Played);
        var strong = Assert.Single(teller.Played);

        Assert.Equal("Harrow here. (radio)", weak.Name);
        Assert.Equal("Harrow here. (radio)", strong.Name);
        Assert.False(weak.Pcm.Span.SequenceEqual(strong.Pcm.Span));
    }

    [Fact]
    public async Task APinnedProviderThatCannotBeBuiltSaysNothing()
    {
        var built = Build();

        Assert.Null(await built.Voice.AnnounceAsync(CastLine("Nobody hears this.", new PinnedVoice(StorySpeaker.Chatterbox, "own"))));
        Assert.Empty(built.Played);
        Assert.Empty(built.Aboard.Asked);
    }

    /// <summary>Synthesis with the network taken out, keeping what it was asked to say and in which voice.</summary>
    private sealed class Recording(string id) : ITtsProvider
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
