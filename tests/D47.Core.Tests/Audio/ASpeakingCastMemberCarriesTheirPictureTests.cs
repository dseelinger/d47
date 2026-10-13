using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>What is audible names the picture of the story cast member speaking, for exactly as long as the line plays (#1139).</summary>
public class ASpeakingCastMemberCarriesTheirPictureTests
{
    private static AudioClip Clip(string name) =>
        new(name, new byte[(int)(AudioFormat.Standard.SampleRate * 0.2) * 2], AudioFormat.Standard);

    private static (AudioArbiter Arbiter, RecordingAudioSink Sink, List<AudioActivity> Heard) Build()
    {
        var sink = new RecordingAudioSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var heard = new List<AudioActivity>();
        arbiter.ActivityChanged += heard.Add;
        return (arbiter, sink, heard);
    }

    private static AudioRequest Line(string name, string? picture, string group = "announcement") =>
        new() { Channel = AudioChannel.Speech, Clip = Clip(name), Group = group, Caption = name, Picture = picture };

    [Fact]
    public void ThePictureIsThereFromTheLinesStartToItsFinish()
    {
        var (arbiter, sink, heard) = Build();

        arbiter.Enqueue(Line("Keep your head down.", "story.cast.vasquez"));

        Assert.Equal("story.cast.vasquez", heard[^1].Picture);
        Assert.Equal("story.cast.vasquez", arbiter.Activity.Picture);

        sink.CompleteCurrent();

        Assert.Null(heard[^1].Picture);
        Assert.Null(arbiter.Activity.Picture);
    }

    [Fact]
    public void TheNextLineCarriesItsOwnPicture()
    {
        var (arbiter, sink, heard) = Build();

        arbiter.Enqueue(Line("First.", "story.cast.vasquez"));
        arbiter.Enqueue(Line("Second.", null));

        sink.CompleteCurrent();

        Assert.Equal("Second.", heard[^1].Caption);
        Assert.Null(heard[^1].Picture);
    }

    [Fact]
    public void AnAlertCuttingTheLineOffTakesThePictureWithIt()
    {
        var (arbiter, _, heard) = Build();

        arbiter.Enqueue(Line("Keep your head down.", "story.cast.vasquez"));
        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Alert, Clip = Clip("interdiction") });

        Assert.Equal(AudioChannel.Alert, heard[^1].Channel);
        Assert.Null(heard[^1].Picture);
    }

    [Fact]
    public void SilenceTakesThePictureWithIt()
    {
        var (arbiter, _, heard) = Build();

        arbiter.Enqueue(Line("Keep your head down.", "story.cast.vasquez"));
        arbiter.Silence();

        Assert.Null(heard[^1].Picture);
    }

    [Fact]
    public void DroppingTheGroupTakesThePictureWithIt()
    {
        var (arbiter, _, heard) = Build();

        arbiter.Enqueue(Line("Keep your head down.", "story.cast.vasquez", group: "turn-3"));
        arbiter.DropGroup("turn-3");

        Assert.Null(heard[^1].Picture);
    }

    [Fact]
    public async Task APipelineCarriesThePictureOnEverySentenceEvenUncaptioned()
    {
        var sink = new RecordingAudioSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var heard = new List<AudioActivity>();
        arbiter.ActivityChanged += heard.Add;

        await using (var pipeline = new SpeechPipeline(
            arbiter,
            new FakeTtsProvider(),
            VoiceSelection.Default,
            "announcement",
            NullLogger.Instance,
            captioned: false,
            picture: "story.cast.vasquez"))
        {
            pipeline.Push("Keep your head down. They are watching the station.");
            await pipeline.CompleteAsync();
        }

        Assert.Equal("story.cast.vasquez", arbiter.Activity.Picture);
        Assert.Null(arbiter.Activity.Caption);

        sink.CompleteCurrent();

        Assert.Equal("story.cast.vasquez", arbiter.Activity.Picture);

        sink.CompleteCurrent();

        Assert.Null(arbiter.Activity.Picture);
        Assert.All(heard.Where(activity => activity.Channel == AudioChannel.Speech), activity => Assert.Equal("story.cast.vasquez", activity.Picture));
    }
}
