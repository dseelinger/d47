using System.Runtime.CompilerServices;
using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The voice pipeline says when a reply from someone other than the ship's AI has finished playing, once, and not
/// for the ship's own reply or a reply that was cut off (#1181).
/// </summary>
public class AnotherSpeakersReplyOpensTheWakeWindowTests
{
    [Fact]
    public async Task AnAddressedReplyEndsWhenItStopsPlaying()
    {
        var (voice, arbiter, sink, ended) = Build(cues: true);

        await voice.RunAsync(Turn(addressed: true), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(arbiter.IsSpeaking);
        Assert.Equal(0, ended.Value);

        Drain(arbiter, sink);

        Assert.Equal(1, ended.Value);
    }

    [Fact]
    public async Task TheShipAisOwnReplyOpensNoWindow()
    {
        var (voice, arbiter, sink, ended) = Build(cues: true);

        await voice.RunAsync(Turn(addressed: false), cancellationToken: TestContext.Current.CancellationToken);
        Drain(arbiter, sink);

        Assert.Equal(0, ended.Value);
    }

    [Fact]
    public async Task AReplyWithNothingToPlayEndsAtOnce()
    {
        var (voice, _, _, ended) = Build(cues: false);

        await voice.RunAsync(Turn(addressed: true), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, ended.Value);
    }

    [Fact]
    public async Task ATurnStartedDuringTheReplyForgetsIt()
    {
        var (voice, arbiter, sink, ended) = Build(cues: true);

        await voice.RunAsync(Turn(addressed: true), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(arbiter.IsSpeaking);

        await voice.RunAsync(Turn(addressed: false), cancellationToken: TestContext.Current.CancellationToken);
        Drain(arbiter, sink);

        Assert.Equal(0, ended.Value);
    }

    [Fact]
    public async Task AReplyCutOffOpensNoWindow()
    {
        var (voice, arbiter, _, ended) = Build(cues: true);
        arbiter.Silenced += voice.ReplyCut;

        await voice.RunAsync(Turn(addressed: true), cancellationToken: TestContext.Current.CancellationToken);
        arbiter.Silence();

        Assert.Equal(0, ended.Value);
    }

    private static async IAsyncEnumerable<TurnEvent> Turn(bool addressed)
    {
        await Task.Yield();

        if (addressed)
        {
            yield return new TurnEvent.Addressed(VoiceRole.CarrierCaptain, "Captain", Signal: 1);
        }

        yield return new TurnEvent.Routed(TurnRoute.Model, Effort: null);
        yield return new TurnEvent.TextDelta("Aye, Commander.");
        yield return new TurnEvent.Completed(new TurnResult(
            TurnOutcome.Answered, TurnRoute.Model, "Aye, Commander.", Effort: null, Cost: null));
    }

    /// <summary>Finishes whatever is playing until nothing is.</summary>
    private static void Drain(AudioArbiter arbiter, RecordingSink sink)
    {
        for (var i = 0; arbiter.IsSpeaking && i < 20; i++)
        {
            sink.Finish(sink.Played.Last(request => !request.Loop).Id);
        }

        Assert.False(arbiter.IsSpeaking);
    }

    private static (VoicePipeline Voice, AudioArbiter Arbiter, RecordingSink Sink, StrongBox<int> Ended) Build(bool cues)
    {
        var sink = new RecordingSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();

        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            CuesEnabled = cues,
            BedEnabled = false,
        };

        arbiter.ActivityChanged += voice.Settle;

        var ended = new StrongBox<int>(0);
        voice.AddressedReplyEnded += () => ended.Value++;

        return (voice, arbiter, sink, ended);
    }

    /// <summary>Plays nothing until told a playback finished.</summary>
    private sealed class RecordingSink : IAudioSink
    {
        public List<PlaybackRequest> Played { get; } = [];

        public event Action<long>? Finished;

        public IRenderReferenceTap ReferenceTap { get; } = new NoTap();

        public void Finish(long playbackId) => Finished?.Invoke(playbackId);

        public void Play(PlaybackRequest request) => Played.Add(request);

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
