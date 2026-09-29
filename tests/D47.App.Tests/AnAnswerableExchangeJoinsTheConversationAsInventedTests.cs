using D47.App;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The lines of an exchange the Commander may answer join the Conversation page, and every invented
/// speaker is shown there as "{Name} (invented)" — both their overheard lines and their replies (#633).
/// </summary>
public class AnAnswerableExchangeJoinsTheConversationAsInventedTests
{
    private static Announcement Line(NpcChatterKind kind, int exchange) =>
        new(NpcChatter.LineKey, "Nice lines on that hull, Commander.")
        {
            Voice = VoiceRole.Comms,
            Speaker = "Courier Vance",
            CommsChannel = "npc",
            Invented = new NpcChatterHeard(
                new NpcChatterLine("Courier Vance", "Nice lines on that hull, Commander."),
                exchange,
                NpcChatter.MayNotice(kind, exchange)),
        };

    [Fact]
    public void AHailJoinsUnderItsInventedName()
    {
        var hail = Line(NpcChatterKind.Hail, 3);

        Assert.True(AppHost.JoinsConversation(hail));
        Assert.Equal("Courier Vance (invented)", AppHost.ConversationSpeaker(hail));
    }

    [Fact]
    public void AControllerExchangeStaysOut()
    {
        Assert.False(AppHost.JoinsConversation(Line(NpcChatterKind.Controller, 3)));
    }

    [Fact]
    public void AReplyFromAnInventedSpeakerIsDrawnUnderTheSameChip()
    {
        var model = new PanelViewModel();
        var presenter = new TurnPresenter(model);

        presenter.On(new TurnEvent.Addressed(VoiceRole.Comms, "Courier Vance", 1));
        presenter.On(new TurnEvent.TextDelta("Any time, Commander."));

        Assert.Equal(
            "Courier Vance (invented)",
            model.Segments(TranscriptPage.Conversation).Last().Speaker);
    }

    [Fact]
    public void AHiredPilotKeepsTheirOwnName()
    {
        var model = new PanelViewModel();
        var presenter = new TurnPresenter(model);

        presenter.On(new TurnEvent.Addressed(VoiceRole.Crew, "Ana Reyes", 1));
        presenter.On(new TurnEvent.TextDelta("On it."));

        Assert.Equal("Ana Reyes", model.Segments(TranscriptPage.Conversation).Last().Speaker);
    }
}
