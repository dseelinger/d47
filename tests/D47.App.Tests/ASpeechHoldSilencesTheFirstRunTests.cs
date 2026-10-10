using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>While the first-run wizard is open nothing reaches a TTS provider, and what was held back is not said afterwards.</summary>
public class ASpeechHoldSilencesTheFirstRunTests
{
    private static (VoicePipeline Voice, RecordingProvider Provider) Build()
    {
        var provider = new RecordingProvider();
        var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();

        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            Tts = provider,
            CuesEnabled = false,
            BedEnabled = false,
            Held = true,
        };

        return (voice, provider);
    }

    private static async IAsyncEnumerable<TurnEvent> Reply(string text)
    {
        yield return new TurnEvent.TextDelta(text);
        yield return new TurnEvent.Completed(
            new TurnResult(TurnOutcome.Answered, TurnRoute.Model, text, null, null));

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ACalloutDuringTheHoldReachesNoProvider()
    {
        var (voice, provider) = Build();

        await voice.AnnounceAsync(new Announcement("fuel", "Bolones is class Y — no fuel there.") { Cue = AlertCue.Interdiction });

        Assert.Empty(provider.Texts);
    }

    [Fact]
    public async Task AnAnnouncementDuringTheHoldReachesNoProvider()
    {
        var (voice, provider) = Build();

        await voice.AnnounceAsync("I have no language model right now.");

        Assert.Empty(provider.Texts);
    }

    [Fact]
    public async Task ATurnAnswerDuringTheHoldReachesNoProvider()
    {
        var (voice, provider) = Build();

        await voice.RunAsync(Reply("Fuel is at ninety percent."), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(provider.Texts);
    }

    [Fact]
    public async Task ALineHeldBackIsNotSpokenOnceTheHoldIsReleased()
    {
        var (voice, provider) = Build();

        await voice.AnnounceAsync("Bolones is class Y — no fuel there.");
        voice.Held = false;
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Empty(provider.Texts);
    }

    [Fact]
    public async Task ALineAfterTheHoldIsReleasedIsSpoken()
    {
        var (voice, provider) = Build();

        voice.Held = false;
        await voice.AnnounceAsync("Fuel is at ninety percent.");

        Assert.Contains("Fuel is at ninety percent.", provider.Texts);
    }

    private sealed class RecordingProvider : ITtsProvider
    {
        private readonly List<string> _texts = [];

        public IReadOnlyList<string> Texts
        {
            get
            {
                lock (_texts)
                {
                    return [.. _texts];
                }
            }
        }

        public string Id => "recording";

        public string Name => "Recording";

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Of([new VoiceInfo("recording", "Recording", "en-GB")]));

        public Task<AudioClip> SynthesizeAsync(
            string text,
            VoiceSelection voice,
            CancellationToken cancellationToken = default)
        {
            lock (_texts)
            {
                _texts.Add(text);
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
    }

    private sealed class NoTap : IRenderReferenceTap
    {
        public event Action<RenderReferenceFrame>? Rendered;

        public void Dispose() => _ = Rendered;
    }
}
