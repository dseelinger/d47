using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// <see cref="Announcement.Language"/> is handed to whichever provider speaks the line, and a line in another language
/// keeps its system names (#1752).
/// </summary>
public sealed class AnAnnouncementsLanguageReachesTheVoiceTests
{
    private static (VoicePipeline Voice, Recording Aboard, Recording Pinned) Build()
    {
        var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();
        var aboard = new Recording(TtsProviderCatalog.EdgeId);
        var pinned = new Recording(TtsProviderCatalog.EdgeId);

        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            Tts = aboard,
            SpeakerFor = _ => aboard,
            PinnedFor = _ => pinned,
            Voice = new VoiceSelection("en-US-AriaNeural"),
            CuesEnabled = false,
            BedEnabled = false,
        };

        return (voice, aboard, pinned);
    }

    [Fact]
    public async Task APinnedCastLineCarriesItsLanguage()
    {
        var (voice, _, pinned) = Build();

        await voice.AnnounceAsync(new Announcement("story.clue.test.0", "助けて。")
        {
            Voice = VoiceRole.Comms,
            Speaker = "Mio",
            Pinned = new PinnedVoice(TtsProviderCatalog.EdgeId, "ja-JP-NanamiNeural"),
            Language = "ja",
        });

        Assert.Equal([("ja-JP-NanamiNeural", "ja")], pinned.Asked);
    }

    [Fact]
    public async Task ALineSpokenByItsRoleCarriesItsLanguage()
    {
        var (voice, aboard, _) = Build();

        await voice.AnnounceAsync(new Announcement("story.clue.test.0", "助けて。") { Language = "ja" });

        Assert.Equal([("en-US-AriaNeural", "ja")], aboard.Asked);
    }

    [Fact]
    public async Task AnEnglishLineCarriesNoLanguage()
    {
        var (voice, aboard, _) = Build();

        await voice.AnnounceAsync(new Announcement("story.clue.test.0", "Help."));

        Assert.Equal([("en-US-AriaNeural", (string?)null)], aboard.Asked);
    }

    [Theory]
    [InlineData(VoiceRole.ShipAi, null, true)]
    [InlineData(VoiceRole.ShipAi, "en", true)]
    [InlineData(VoiceRole.ShipAi, "ja", false)]
    [InlineData(VoiceRole.Comms, "ja", false)]
    [InlineData(VoiceRole.Narrator, null, false)]
    public void OnlyAnEnglishLineNotNarratedTakesPronouns(VoiceRole role, string? language, bool takes)
    {
        Assert.Equal(takes, Announcer.TakesPronouns(new Announcement("k", "Sol.") { Voice = role, Language = language }));
    }

    private sealed class Recording(string id) : ITtsProvider
    {
        public List<(string? Voice, string? Language)> Asked { get; } = [];

        public string Id => id;

        public string Name => id;

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Of([]));

        public Task<AudioClip> SynthesizeAsync(string text, VoiceSelection voice, CancellationToken cancellationToken = default)
        {
            lock (Asked)
            {
                Asked.Add((voice.VoiceId, voice.Language));
            }

            return Task.FromResult(new AudioClip(text, new byte[9_600], AudioFormat.Standard));
        }
    }

    private sealed class SilentSink : IAudioSink
    {
        public event Action<long>? Finished;

        public IRenderReferenceTap ReferenceTap { get; } = new NoTap();

        public void Play(PlaybackRequest request) => _ = Finished;

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
