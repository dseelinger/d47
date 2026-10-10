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
/// The stock COVAS reverb, the radio link and the Guardian effects run as filters, so the ship AI, a voice over the air
/// and a story cast member are queued while they arrive. A Guardian chain with an effect that needs the whole clip
/// releases nothing until its source completes.
/// </summary>
public sealed class TheCovasAndTheRadioPlayWhileTheyArriveTests
{
    private static readonly SpeechSettings WhisperTicked = new()
    {
        GuardianVoice = new GuardianVoiceSettings
        {
            Effects = [.. GuardianVoice.Defaults.Select(effect => effect with { Ticked = effect.Id == "whisper" })],
        },
    };

    private static readonly SpeechSettings DamagedCore = new()
    {
        GuardianVoice = new GuardianVoiceSettings
        {
            Effects = [.. GuardianPresets.EffectsFor(GuardianPresets.Find("damaged-core")!)],
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
    public async Task AnOverTheAirRoleIsQueuedWhileItArrives()
    {
        var (voice, played) = Build(PersonaCatalog.Covas, new SpeechSettings());

        await voice.AnnounceAsync(new Announcement("line.comms", "Scanning.") { Voice = VoiceRole.Comms });

        Assert.NotNull(Assert.Single(played).Arriving);
    }

    [Fact]
    public async Task AnOverTheAirRoleIsQueuedBeforeItsSourceClipCompletes()
    {
        var (voice, played) = Build(PersonaCatalog.Covas, new SpeechSettings());
        var held = new HeldOpen();
        voice.Tts = held;

        var announced = voice.AnnounceAsync(new Announcement("line.comms", "Scanning.") { Voice = VoiceRole.Comms });

        for (var waited = 0; played.Count == 0 && waited < 5_000; waited += 10)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.NotNull(Assert.Single(played).Arriving);
        Assert.False(held.Source!.IsComplete);

        held.Source.Complete();
        await announced;
    }

    [Fact]
    public async Task AnOverTheAirReplyIsQueuedWhileItArrives()
    {
        var (voice, played) = Build(PersonaCatalog.Covas, new SpeechSettings());
        voice.SpeakingAs = VoiceRole.Comms;

        await voice.RunAsync(Reply("Docking granted."), cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(Assert.Single(played).Arriving);
    }

    [Fact]
    public async Task ADamagedCoreIsQueuedBeforeItsSourceClipCompletes()
    {
        var (voice, played) = Build(PersonaCatalog.Warden, DamagedCore);
        var held = new HeldOpen(seconds: 1);
        voice.Tts = held;

        var announced = voice.AnnounceAsync(new Announcement("routine.scan", "Scanning."));

        for (var waited = 0; (played.Count == 0 || played[0].Arriving!.Length == 0) && waited < 5_000; waited += 10)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        var treated = Assert.Single(played).Arriving;

        Assert.NotNull(treated);
        Assert.True(treated.Length > 0, "nothing of the treated clip arrived before its source completed");
        Assert.False(held.Source!.IsComplete);

        held.Source.Complete();
        await announced;
    }

    [Fact]
    public async Task AChainThatNeedsTheWholeClipCompletesOnlyAfterItsSource()
    {
        var (voice, played) = Build(PersonaCatalog.Warden, WhisperTicked);
        var held = new HeldOpen(seconds: 1);
        voice.Tts = held;

        var announced = voice.AnnounceAsync(new Announcement("routine.scan", "Scanning."));

        for (var waited = 0; played.Count == 0 && waited < 5_000; waited += 10)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        var treated = Assert.Single(played).Arriving;

        Assert.NotNull(treated);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(treated.IsComplete);
        Assert.Equal(0, treated.Length);

        held.Source!.Complete();
        await treated.Whole.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.True(treated.Length > 0);
        await announced;
    }

    [Fact]
    public async Task AStoryCastMemberOnALinkIsQueuedWhileItArrives()
    {
        var (voice, played) = Build(PersonaCatalog.Covas, new SpeechSettings());

        await voice.AnnounceAsync(new Announcement("story.clue.the-test-story.0", "Harrow here.")
        {
            Voice = VoiceRole.ShipAi,
            Speaker = "Harrow",
            Pinned = new PinnedVoice(StorySpeaker.Kokoro, "bm_george") { Link = 1 },
        });

        Assert.NotNull(Assert.Single(played).Arriving);
    }

    [Fact]
    public async Task AStoryCastMemberWithEffectsIsQueuedWhileItArrives()
    {
        var (voice, played) = Build(PersonaCatalog.Covas, new SpeechSettings());

        await voice.AnnounceAsync(new Announcement("story.clue.the-test-story.0", "Harrow here.")
        {
            Voice = VoiceRole.ShipAi,
            Speaker = "Harrow",
            Pinned = new PinnedVoice(StorySpeaker.Kokoro, "bm_george") { Link = 1, Effects = [new StorySpeakerEffect("comb", 12)] },
        });

        Assert.NotNull(Assert.Single(played).Arriving);
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
            Task.FromResult(new AudioClip(text, Tone(0.1), AudioFormat.Standard));

        public Task<ArrivingClip> StreamAsync(string text, VoiceSelection voice, CancellationToken cancellationToken = default)
        {
            var arriving = new ArrivingClip(text);
            arriving.Append(Tone(0.1));

            _ = Task.Delay(100, CancellationToken.None).ContinueWith(_ => arriving.Complete(), TaskScheduler.Default);

            return Task.FromResult(arriving);
        }
    }

    /// <summary>A 900 Hz tone at 0.4 of full scale.</summary>
    private static byte[] Tone(double seconds)
    {
        var samples = (int)(seconds * 48_000);
        var pcm = new byte[samples * 2];

        for (var index = 0; index < samples; index++)
        {
            var value = (short)(Math.Sin(2 * Math.PI * 900 * index / 48_000.0) * 0.4 * short.MaxValue);

            pcm[index * 2] = (byte)(value & 0xFF);
            pcm[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return pcm;
    }

    /// <summary>A streaming provider whose clip stays open until the test completes it.</summary>
    private sealed class HeldOpen(double seconds = 0.1) : ITtsProvider
    {
        public ArrivingClip? Source { get; private set; }

        public string Id => "held-open";

        public string Name => "Held open";

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Of([]));

        public Task<AudioClip> SynthesizeAsync(string text, VoiceSelection voice, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ArrivingClip> StreamAsync(string text, VoiceSelection voice, CancellationToken cancellationToken = default)
        {
            Source = new ArrivingClip(text);
            Source.Append(Tone(seconds));

            return Task.FromResult(Source);
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
