using D47.App;
using D47.Core.Audio;
using D47.Core.Callouts;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Every line d47 speaks reaches the Transcript from <c>SayAsync</c>, including in-game comms and
/// overheard chatter, named by <see cref="AppHost.ConversationSpeaker"/>.
/// </summary>
public class EverySpokenLineJoinsTheTranscriptTests
{
    [Fact]
    public void SayAsyncRaisesTheTranscriptLineAfterTheVoiceHasTheAnnouncement()
    {
        var say = AppSource.Method("SayAsync").Text;

        var announce = say.IndexOf("Voice.AnnounceAsync(", StringComparison.Ordinal);
        var raised = say.IndexOf("CalloutSaid?.Invoke(", StringComparison.Ordinal);

        Assert.True(announce >= 0 && raised > announce);
    }

    [Fact]
    public void OnlySayAsyncRaisesTheTranscriptLine()
    {
        Assert.Single(AppSource.CodeLines("CalloutSaid?.Invoke("));
    }

    [Fact]
    public void ATowerLineIsNamedTower()
    {
        var announcement = new Announcement("carrier.departure", "Sacred Fire clear. Safe flying, Commander.")
        {
            Voice = VoiceRole.TowerControl,
        };

        Assert.Equal("Tower", AppHost.ConversationSpeaker(announcement, "COVAS"));
    }

    [Fact]
    public void ARelayIsNamedForItsSender()
    {
        var announcement = new Announcement("message.npc", "Clear skies out there.")
        {
            Voice = VoiceRole.Comms,
            Speaker = "Ilse Bruhn",
            CommsChannel = "npc",
        };

        Assert.Equal("Ilse Bruhn", AppHost.ConversationSpeaker(announcement, "COVAS"));
    }

    [Fact]
    public void ADirectMessageIsNamedForItsSender()
    {
        var announcement = new Announcement("message.player", "Vex says: Meet at the beacon.")
        {
            Voice = VoiceRole.Comms,
            Speaker = "Vex",
            SpeakerIsPlayer = true,
            CommsChannel = "player",
        };

        Assert.Equal("Vex", AppHost.ConversationSpeaker(announcement, "COVAS"));
    }

    [Fact]
    public void ANarratedLineIsNamedTheNarrator()
    {
        var announcement = new Announcement("story.end.x", "The beacon went dark.") { Voice = VoiceRole.Narrator };

        Assert.Equal("Narrator", AppHost.ConversationSpeaker(announcement, "COVAS"));
    }

    [Fact]
    public void AnOrdinaryCalloutWithNoSpeakerIsNamedForTheShipsAi()
    {
        Assert.Equal("COVAS", AppHost.ConversationSpeaker(new Announcement("fuel.low", "Fuel scoop advised."), "COVAS"));
        Assert.Equal("Vesper", AppHost.ConversationSpeaker(new Announcement("fuel.low", "Fuel scoop advised."), "Vesper"));
    }

    [Fact]
    public void AStoryEndingIsSpokenInTheVoiceItWasWrittenFor()
    {
        var lines = AppSource.Method("SpeakStoryLinesAsync").Text;

        Assert.Contains("Voice = voice", lines, StringComparison.Ordinal);
    }
}
