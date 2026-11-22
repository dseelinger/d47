using D47.App.Voice;
using Xunit;

namespace D47.App.Tests.Books;

public sealed class ABookIsReadOnlyWithTheChosenLocalVoiceTests
{
    [Fact]
    public void OnlyTheChosenLocalVoiceMakesTheSentences()
    {
        var rig = BookRig.InMemory(provider: "chatterbox");

        rig.Play();
        rig.Finish(BookRig.First);
        rig.Finish(BookRig.Second);
        rig.Finish(BookRig.Third);
        rig.Finish(BookRig.Fourth);

        Assert.Equal([BookRig.First, BookRig.Second, BookRig.Third, BookRig.Fourth], rig.Voices["chatterbox"].Said);
        Assert.Empty(rig.Voices["kokoro"].Said);
        Assert.Empty(rig.Voices["elevenlabs"].Said);
        Assert.All(rig.Asked, id => Assert.Equal("chatterbox", id));
        Assert.Equal([BookRig.First, BookRig.Second, BookRig.Third, BookRig.Fourth], rig.Sink.Names);
    }

    [Fact]
    public void AVoiceThatIsNotLocalMakesNothingAndSaysWhy()
    {
        var rig = BookRig.InMemory(provider: "elevenlabs");

        rig.Play();

        Assert.Empty(rig.Asked);
        Assert.All(rig.Voices.Values, voice => Assert.Empty(voice.Said));
        Assert.Empty(rig.Sink.Names);
        Assert.True(rig.Player.Now!.IsPaused);
        Assert.Contains("elevenlabs", rig.Player.Now.Problem, StringComparison.Ordinal);
        Assert.Equal(BookRig.First, rig.Player.Now.Text);
    }

    [Fact]
    public void AVoiceThatIsNotInstalledMakesNothingAndSaysSo()
    {
        var rig = BookRig.InMemory();
        rig.Installed = false;

        rig.Play();

        Assert.All(rig.Voices.Values, voice => Assert.Empty(voice.Said));
        Assert.Empty(rig.Sink.Names);
        Assert.Equal(StoryRunPause.Commander, rig.Player.Now!.Pause);
        Assert.Equal("Kokoro is not installed.", rig.Player.Now.Problem);
    }

    [Fact]
    public void AFewSentencesAreMadeAheadOfTheOnePlaying()
    {
        var rig = BookRig.InMemory();

        rig.Play();

        Assert.Equal([BookRig.First, BookRig.Second, BookRig.Third, BookRig.Fourth], rig.Voices["kokoro"].Said);
        Assert.Equal([BookRig.First], rig.Sink.Names);
    }
}
