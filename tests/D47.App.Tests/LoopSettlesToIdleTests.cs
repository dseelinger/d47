using D47.App.Voice;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The face goes back to rest when D47 stops talking.
/// <para>
/// Reported from a running build: the icon stopped on the tick and stayed there, because
/// <see cref="LoopState.Answered"/> was terminal and nothing ever left it. "The last turn
/// succeeded" and "D47 is doing something right now" then looked identical, which is the one
/// distinction a loop-state icon exists to make.
/// </para>
/// </summary>
public class LoopSettlesToIdleTests
{
    private static (VoicePipeline Voice, AudioArbiter Arbiter, List<LoopState> Seen) Build()
    {
        var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();
        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance);
        var seen = new List<LoopState>();

        arbiter.ActivityChanged += voice.Settle;
        voice.StateEntered += seen.Add;

        return (voice, arbiter, seen);
    }

    [Fact]
    public void AnsweringSettlesBackToIdleOnceNothingIsAudible()
    {
        var (voice, _, seen) = Build();

        voice.EnterState(LoopState.Answered, cue: false);
        voice.Settle(new AudioActivity(Channel: null, Caption: null, BedPlaying: false));

        Assert.Equal([LoopState.Answered, LoopState.Idle], seen);
    }

    /// <summary>Not while something is still being said — that is the state it is reporting.</summary>
    [Fact]
    public void ItDoesNotSettleWhileSpeechIsStillPlaying()
    {
        var (voice, _, seen) = Build();

        voice.EnterState(LoopState.Answered, cue: false);
        voice.Settle(new AudioActivity(AudioChannel.Speech, Caption: "still talking", BedPlaying: false));

        Assert.Equal([LoopState.Answered], seen);
    }

    /// <summary>
    /// Thinking is not settled out of by silence. The bed can be off while a turn is still
    /// waiting on a provider, and dropping to idle there would say the turn had finished.
    /// </summary>
    [Fact]
    public void AThinkingTurnIsNotSettledByAQuietArbiter()
    {
        var (voice, _, seen) = Build();

        voice.EnterState(LoopState.Thinking, cue: false);
        voice.Settle(new AudioActivity(Channel: null, Caption: null, BedPlaying: false));

        Assert.Equal([LoopState.Thinking], seen);
    }

    /// <summary>Once, not on every quiet report the arbiter makes afterwards.</summary>
    [Fact]
    public void ItSettlesOnceRatherThanOnEveryQuietReport()
    {
        var (voice, _, seen) = Build();

        voice.EnterState(LoopState.Failed, cue: false);

        var quiet = new AudioActivity(Channel: null, Caption: null, BedPlaying: false);
        voice.Settle(quiet);
        voice.Settle(quiet);
        voice.Settle(quiet);

        Assert.Equal([LoopState.Failed, LoopState.Idle], seen);
    }

    /// <summary>
    /// A sink that accepts everything and finishes nothing, so the arbiter's activity is driven
    /// by this test rather than by a clock. No audio device is involved, which is the point.
    /// </summary>
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
