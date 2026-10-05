using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>The same queue rules the clip versions in <see cref="AudioArbiterTests"/> check, for a clip still arriving.</summary>
public class AnArrivingClipIsQueuedLikeAClipTests
{
    private static AudioClip Clip(string name) =>
        new(name, new byte[(int)(AudioFormat.Standard.SampleRate * 0.2) * 2], AudioFormat.Standard);

    private static (AudioArbiter Arbiter, RecordingAudioSink Sink) Build()
    {
        var sink = new RecordingAudioSink();
        return (new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start(), sink);
    }

    private static AudioRequest Arriving(string name, string? group = null) =>
        new() { Channel = AudioChannel.Speech, Arriving = new ArrivingClip(name), Group = group, Caption = name };

    private static AudioRequest Speech(string name, string? group = null) =>
        new() { Channel = AudioChannel.Speech, Clip = Clip(name), Group = group, Caption = name };

    [Fact]
    public void ItPlaysInQueueOrderBehindAClip()
    {
        var (arbiter, sink) = Build();
        var arriving = Arriving("second", group: "turn-1");

        arbiter.Enqueue(Speech("first", group: "turn-1"));
        arbiter.Enqueue(arriving);

        Assert.Equal("first", Assert.Single(sink.Started).Name);

        sink.CompleteCurrent();

        Assert.Equal(2, sink.Started.Count);
        Assert.Equal("second", sink.Started[1].Name);
        Assert.Same(arriving.Arriving, sink.Started[1].Arriving);
        Assert.Null(sink.Started[1].Clip);
        Assert.False(sink.Started[1].Loop);
    }

    [Fact]
    public void AnAlertSupersedesIt()
    {
        var (arbiter, sink) = Build();

        arbiter.Enqueue(Arriving("a long answer"));
        var speechId = sink.Started[0].Id;

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Alert, Clip = Clip("interdiction") });

        Assert.Contains(speechId, sink.Stopped);
        Assert.Equal("interdiction", sink.Started[1].Name);

        sink.CompleteCurrent();
        Assert.Equal(2, sink.Started.Count);
    }

    [Fact]
    public void DroppingItsGroupDropsIt()
    {
        var (arbiter, sink) = Build();

        arbiter.Enqueue(Arriving("old one", group: "turn-1"));
        var playing = sink.Started[0].Id;
        arbiter.Enqueue(Arriving("old two", group: "turn-1"));
        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Alert, Clip = Clip("hull damage"), Group = "alerts" });

        arbiter.DropGroup("turn-1");

        Assert.Contains(playing, sink.Stopped);
        Assert.Equal("hull damage", sink.Started[^1].Name);

        sink.PlayOutAll();
        Assert.DoesNotContain(sink.Started, request => request.Name == "old two");
    }

    [Fact]
    public void SilenceDropsIt()
    {
        var (arbiter, sink) = Build();

        arbiter.Enqueue(Arriving("one"));
        arbiter.Enqueue(Arriving("two"));

        var startedBefore = sink.Started.Count;
        arbiter.Silence();

        Assert.Equal(1, sink.StopAllCount);
        Assert.False(arbiter.IsSpeaking);

        sink.CompleteCurrent();
        Assert.Equal(startedBefore, sink.Started.Count);
    }

    [Fact]
    public void AClosedGroupRefusesIt()
    {
        var (arbiter, sink) = Build();

        arbiter.CloseGroup("chatter");
        arbiter.Enqueue(Arriving("refused", group: "chatter"));

        Assert.Empty(sink.Started);
    }

    [Fact]
    public void ARequestCarryingBothOrNeitherIsRefused()
    {
        var (arbiter, _) = Build();

        Assert.Throws<ArgumentException>(() => arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Speech }));
        Assert.Throws<ArgumentException>(() => arbiter.Enqueue(new AudioRequest
        {
            Channel = AudioChannel.Speech,
            Clip = Clip("both"),
            Arriving = new ArrivingClip("both"),
        }));
    }
}
