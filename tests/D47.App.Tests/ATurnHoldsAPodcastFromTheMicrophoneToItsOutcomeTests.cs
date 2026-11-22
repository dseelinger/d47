using D47.App.Voice;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The loop marks a turn in flight from the microphone opening through transcription and thinking, so a podcast stays paused between them (#2653).</summary>
public class ATurnHoldsAPodcastFromTheMicrophoneToItsOutcomeTests
{
    private static (VoicePipeline Voice, AudioArbiter Arbiter) Build()
    {
        var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();
        return (new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance) { CuesEnabled = false }, arbiter);
    }

    [Theory]
    [InlineData(LoopState.Listening)]
    [InlineData(LoopState.Transcribing)]
    [InlineData(LoopState.Thinking)]
    public void AStateInsideATurnIsInFlight(LoopState state)
    {
        var (voice, arbiter) = Build();

        voice.EnterState(state);

        Assert.True(arbiter.TurnInFlight);
    }

    [Theory]
    [InlineData(LoopState.Idle)]
    [InlineData(LoopState.Answered)]
    [InlineData(LoopState.Unsure)]
    [InlineData(LoopState.Failed)]
    public void AnOutcomeOrIdleIsNot(LoopState state)
    {
        var (voice, arbiter) = Build();
        voice.EnterState(LoopState.Thinking);

        voice.EnterState(state);

        Assert.False(arbiter.TurnInFlight);
    }
}
