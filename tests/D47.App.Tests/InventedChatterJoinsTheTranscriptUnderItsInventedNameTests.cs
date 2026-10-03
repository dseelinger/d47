using D47.App;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Invented speakers are shown as "{Name} (invented)" for their lines and their replies; whether a line
/// can be answered is carried on the announcement and does not decide whether it is shown (#633).
/// </summary>
public class InventedChatterJoinsTheTranscriptUnderItsInventedNameTests
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
    public void AHailIsNamedByItsInventedName()
    {
        var hail = Line(NpcChatterKind.Hail, 3);

        Assert.True(hail.Invented!.Answerable);
        Assert.Equal("Courier Vance (invented)", AppHost.ConversationSpeaker(hail));
    }

    [Fact]
    public void AControllerExchangeIsNamedByItsInventedNameAndCannotBeAnswered()
    {
        var controller = Line(NpcChatterKind.Controller, 3);

        Assert.False(controller.Invented!.Answerable);
        Assert.Equal("Courier Vance (invented)", AppHost.ConversationSpeaker(controller));
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
