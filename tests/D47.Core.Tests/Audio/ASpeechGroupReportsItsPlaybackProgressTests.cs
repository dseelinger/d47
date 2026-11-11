using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

public class ASpeechGroupReportsItsPlaybackProgressTests
{
    private const string Group = "story-run-playback";

    private static readonly TrackProgress Known = new(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(11));

    private readonly RecordingAudioSink _sink = new();
    private readonly AudioArbiter _audio;

    public ASpeechGroupReportsItsPlaybackProgressTests()
    {
        _audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
    }

    private static AudioRequest Line(string group) =>
        new() { Channel = AudioChannel.Speech, Clip = new AudioClip("line", new byte[9600], AudioFormat.Standard), Group = group };

    [Fact]
    public void ALineReportsWhatTheSinkCountsOnlyWhileItPlays()
    {
        Assert.Null(_audio.SpeechProgress(Group));

        _audio.Enqueue(Line(Group));
        _sink.Progresses[_sink.Started[0].Id] = Known;

        Assert.Equal(Known, _audio.SpeechProgress(Group));

        _sink.CompleteCurrent();

        Assert.Null(_audio.SpeechProgress(Group));
    }

    [Fact]
    public void ALineQueuedBehindAnotherHasNoProgressYet()
    {
        _audio.Enqueue(Line("other"));
        _audio.Enqueue(Line(Group));

        Assert.Null(_audio.SpeechProgress(Group));
    }

    [Fact]
    public void ALineInAnotherGroupIsNotReported()
    {
        _audio.Enqueue(Line("other"));
        _sink.Progresses[_sink.Started[0].Id] = Known;

        Assert.Null(_audio.SpeechProgress(Group));
    }
}
