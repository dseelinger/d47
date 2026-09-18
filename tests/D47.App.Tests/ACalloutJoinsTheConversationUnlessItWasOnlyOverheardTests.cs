using D47.App;
using D47.Core.Audio;
using D47.Core.Callouts;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Conversation page carries every callout spoken to the Commander, and stays out of invented
/// chatter and a relay they only overheard (#276).
/// </summary>
public class ACalloutJoinsTheConversationUnlessItWasOnlyOverheardTests
{
    [Fact]
    public void ATowerLineWithNoTranscriptJoins()
    {
        var announcement = new Announcement("carrier.departure", "Sacred Fire clear. Safe flying, Commander.")
        {
            Voice = VoiceRole.TowerControl,
        };

        Assert.True(AppHost.JoinsConversation(announcement));
        Assert.Equal("Tower", AppHost.ConversationSpeaker(announcement));
    }

    [Fact]
    public void InventedChatterNeverJoins()
    {
        var announcement = new Announcement(NpcChatter.LineKey, "Clear to depart.")
        {
            Voice = VoiceRole.Comms,
            Speaker = "Tower",
        };

        Assert.False(AppHost.JoinsConversation(announcement));
    }

    [Theory]
    [InlineData("npc")]
    [InlineData("local")]
    [InlineData("starsystem")]
    [InlineData("wing")]
    [InlineData("squadron")]
    [InlineData("squadleaders")]
    public void ARelayOnAnyChannelButPlayerStaysOut(string channel)
    {
        var announcement = new Announcement($"message.{channel}", "Clear skies out there.")
        {
            Voice = VoiceRole.Comms,
            Speaker = "Ilse Bruhn",
            CommsChannel = channel,
        };

        Assert.False(AppHost.JoinsConversation(announcement));
    }

    [Fact]
    public void ADirectMessageOnThePlayerChannelJoins()
    {
        var announcement = new Announcement("message.player", "Vex says: Meet at the beacon.")
        {
            Voice = VoiceRole.Comms,
            Speaker = "Vex",
            SpeakerIsPlayer = true,
            CommsChannel = "player",
        };

        Assert.True(AppHost.JoinsConversation(announcement));
        Assert.Equal("Vex", AppHost.ConversationSpeaker(announcement));
    }

    [Fact]
    public void TheCarrierCannedRelayStaysOutUnlessTheChannelIsPlayer()
    {
        var overheard = new Announcement(IncomingMessages.CarrierCannedKey, "Docking granted.")
        {
            Voice = VoiceRole.TowerControl,
            CommsChannel = "npc",
        };

        var direct = overheard with { CommsChannel = "player" };

        Assert.False(AppHost.JoinsConversation(overheard));
        Assert.True(AppHost.JoinsConversation(direct));
    }

    [Fact]
    public void AnOrdinaryCalloutWithNoSpeakerIsNamedD47()
    {
        var announcement = new Announcement("fuel.low", "Fuel scoop advised.");

        Assert.True(AppHost.JoinsConversation(announcement));
        Assert.Equal("D47", AppHost.ConversationSpeaker(announcement));
    }
}
