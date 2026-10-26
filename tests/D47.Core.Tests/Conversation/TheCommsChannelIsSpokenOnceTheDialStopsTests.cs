using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Conversation;

public class TheCommsChannelIsSpokenOnceTheDialStopsTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    private static (CommsChannels Channels, List<string> Said, CommsStepSpeaker Speaker) Setup()
    {
        var channels = new CommsChannels();
        channels.Update(new CommsInputs { CarrierOwned = true });
        var said = new List<string>();
        return (channels, said, new CommsStepSpeaker(channels, said.Add));
    }

    [Fact]
    public void ThreeStepsThenAPauseSpeakOneLineNamingTheLastChannel()
    {
        var (channels, said, speaker) = Setup();

        for (var i = 0; i < 3; i++)
        {
            speaker.Step(CommsCapability.Current, CommsCapability.Next, Start.AddMilliseconds(100 * i));
            speaker.Poll(Start.AddMilliseconds(100 * i));
        }

        Assert.Empty(said);

        speaker.Poll(Start.AddMilliseconds(200 + 399));
        Assert.Empty(said);

        speaker.Poll(Start.AddMilliseconds(200 + 400));
        speaker.Poll(Start.AddMilliseconds(200 + 800));

        Assert.Equal([CommsCapability.Say(channels.Selected!)], said);
    }

    [Fact]
    public void ASinglePressIsSpokenFourHundredMillisecondsLater()
    {
        var (channels, said, speaker) = Setup();

        speaker.Step(CommsCapability.Current, CommsCapability.Next, Start);
        speaker.Poll(Start.AddMilliseconds(399));
        Assert.Empty(said);

        speaker.Poll(Start.AddMilliseconds(400));
        Assert.Single(said);
        Assert.False(speaker.Waiting);
    }

    [Fact]
    public void ARefusedStepIsSpokenAtOnce()
    {
        var said = new List<string>();
        var speaker = new CommsStepSpeaker(null, said.Add);

        speaker.Step(CommsCapability.Next, null, Start);

        Assert.Single(said);
        Assert.False(speaker.Waiting);
    }
}
