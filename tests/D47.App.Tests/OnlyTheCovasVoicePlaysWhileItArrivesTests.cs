using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The stock COVAS reverb runs as a filter, so the ship AI is queued while it arrives; the radio, the Guardian
/// effects and a story cast member's treatment are still queued whole.
/// </summary>
public sealed class OnlyTheCovasVoicePlaysWhileItArrivesTests
{
    private static readonly SpeechSettings CylonTicked = new()
    {
        GuardianVoice = new GuardianVoiceSettings
        {
            Effects = [.. GuardianVoice.Defaults.Select(effect => effect with { Ticked = effect.Id == "cylon" })],
        },
    };

    private static (VoicePipeline Voice, List<PlaybackRequest> Played) Build(Persona core, SpeechSettings speech)
    {
        var sink = new RequestSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var tts = new SlowlyArriving();

        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            Tts = tts,
            PinnedFor = _ => tts,
            CuesEnabled = false,
            BedEnabled = false,
            GuardianColour = GuardianVoice.ColourFor(speech, core),
            GuardianRunning = GuardianVoice.RunningColourFor(speech, core),
        };

        return (voice, sink.Played);
    }

    [Fact]
    public async Task TheStockCovasIsQueuedWhileItArrives()
    {
        var (voice, played) = Build(PersonaCatalog.Covas, new SpeechSettings());

        await voice.AnnounceAsync(new Announcement("routine.scan", "Scanning."));

        Assert.NotNull(Assert.Single(played).Arriving);
    }

    [Fact]
    public async Task AStockCovasReplyIsQueuedWhileItArrives()
    {
        var (voice, played) = Build(PersonaCatalog.Covas, new SpeechSettings());

        await voice.RunAsync(Reply("Scanning."), cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(Assert.Single(played).Arriving);
    }

    [Fact]
    public async Task AnOverTheAirRoleIsQueuedWhole()
    {
        var (voice, played) = Build(PersonaCatalog.Covas, new SpeechSettings());

        await voice.AnnounceAsync(new Announcement("line.comms", "Scanning.") { Voice = VoiceRole.Comms });

        Assert.NotNull(Assert.Single(played).Clip);
    }

    [Fact]
    public async Task AGuardianCoreIsQueuedWhole()
    {
        var (voice, played) = Build(PersonaCatalog.Warden, CylonTicked);

        await voice.AnnounceAsync(new Announcement("routine.scan", "Scanning."));

        Assert.NotNull(Assert.Single(played).Clip);
    }

    [Fact]
    public async Task AStoryCastMemberWithATreatmentIsQueuedWhole()
    {
        var (voice, played) = Build(PersonaCatalog.Covas, new SpeechSettings());

        await voice.AnnounceAsync(new Announcement("story.clue.the-test-story.0", "Harrow here.")
        {
            Voice = VoiceRole.ShipAi,
            Speaker = "Harrow",
            Pinned = new PinnedVoice(StorySpeaker.Kokoro, "bm_george") { Link = 1 },
        });

        Assert.NotNull(Assert.Single(played).Clip);
    }

    private static async IAsyncEnumerable<TurnEvent> Reply(string text)
    {
        yield return new TurnEvent.TextDelta(text);
        yield return new TurnEvent.Completed(new TurnResult(TurnOutcome.Answered, TurnRoute.Model, text, null, null));

        await Task.CompletedTask;
    }

    /// <summary>A streaming provider whose clip completes a tenth of a second after it is returned.</summary>
    private sealed class SlowlyArriving : ITtsProvider
    {
        public string Id => "slowly-arriving";

        public string Name => "Slowly arriving";

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Of([]));

        public Task<AudioClip> SynthesizeAsync(string text, VoiceSelection voice, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AudioClip(text, Tone(), AudioFormat.Standard));

        public Task<ArrivingClip> StreamAsync(string text, VoiceSelection voice, CancellationToken cancellationToken = default)
        {
            var arriving = new ArrivingClip(text);
            arriving.Append(Tone());

            _ = Task.Delay(100, CancellationToken.None).ContinueWith(_ => arriving.Complete(), TaskScheduler.Default);

            return Task.FromResult(arriving);
        }

        private static byte[] Tone()
        {
            const int Samples = 4_800;
            var pcm = new byte[Samples * 2];

            for (var index = 0; index < Samples; index++)
            {
                var value = (short)(Math.Sin(2 * Math.PI * 900 * index / 48_000.0) * 0.4 * short.MaxValue);

                pcm[index * 2] = (byte)(value & 0xFF);
                pcm[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
            }

            return pcm;
        }
    }

    /// <summary>Keeps every request it was asked to play.</summary>
    private sealed class RequestSink : IAudioSink
    {
        public List<PlaybackRequest> Played { get; } = [];

        public event Action<long>? Finished;

        public IRenderReferenceTap ReferenceTap { get; } = new NoTap();

        public void Play(PlaybackRequest request)
        {
            Played.Add(request);
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
