using D47.App.Voice;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The woken path's cue: heard when cues are on, and the loop stays where it is either way.</summary>
public class ACuePlaysWithoutMovingTheLoopTests
{
    [Fact]
    public void TheListeningCuePlaysWhenCuesAreOn()
    {
        var (voice, played, entered) = Build(cuesEnabled: true);

        voice.Cue(LoopState.Listening);

        var request = Assert.Single(played);
        Assert.Equal(CueLibrary.Load().For(LoopState.Listening).Name, request.Name);
        Assert.Empty(entered);
    }

    [Fact]
    public void NothingPlaysWhenCuesAreOff()
    {
        var (voice, played, entered) = Build(cuesEnabled: false);

        voice.Cue(LoopState.Listening);

        Assert.Empty(played);
        Assert.Empty(entered);
    }

    private static (VoicePipeline Voice, List<PlaybackRequest> Played, List<LoopState> Entered) Build(bool cuesEnabled)
    {
        var sink = new RecordingSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();

        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            CuesEnabled = cuesEnabled,
        };

        var entered = new List<LoopState>();
        voice.StateEntered += entered.Add;

        return (voice, sink.Played, entered);
    }

    /// <summary>Plays nothing and keeps what it was asked to play.</summary>
    private sealed class RecordingSink : IAudioSink
    {
        public List<PlaybackRequest> Played { get; } = [];

        public event Action<long>? Finished;

        public IRenderReferenceTap ReferenceTap { get; } = new NoTap();

        public void Play(PlaybackRequest request) => Played.Add(request);

        public void Stop(long playbackId) => _ = Finished;

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
