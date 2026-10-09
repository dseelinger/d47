using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Journal;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A turn records its speaker's picture name when it is written: a reply keeps the core that answered after a core
/// swap, and a callout, an addressed pilot or a cast line each carry their own (#770).
/// </summary>
public sealed class EachTurnKeepsThePictureOfWhoSaidItTests
{
    private static PanelViewModel Model() => new() { ShipPictureSource = () => SpeakerPictures.Core("warden") };

    [Fact]
    public void AReplyKeepsTheCoreThatAnsweredAfterASwap()
    {
        var aboard = SpeakerPictures.Core("warden");
        var model = new PanelViewModel { ShipPictureSource = () => aboard };
        model.Append("Hello", voice: TranscriptVoice.Commander);

        var presenter = new TurnPresenter(model, model.ShipPicture);
        presenter.On(new TurnEvent.TextDelta("Warden here."));

        aboard = SpeakerPictures.Core("cora");
        presenter.On(new TurnEvent.TextDelta(" Still Warden."));

        Assert.Equal("core.warden", model.Segments(TranscriptPage.Conversation)[^1].Picture);

        model.Append("Cora now.");

        Assert.Equal(["core.warden", "core.cora"], model.Segments(TranscriptPage.Conversation).Skip(1).Select(segment => segment.Picture));
    }

    [Fact]
    public void TwoShipLinesWithDifferentPicturesStayTwoRuns()
    {
        var model = Model();

        model.Append("One.", picture: "core.warden");
        model.Append("Two.", picture: "core.cora");

        Assert.Equal(2, model.Segments(TranscriptPage.Conversation).Count);
    }

    [Fact]
    public void AnAddressedPilotsReplyCarriesTheirCrewPicture()
    {
        var model = Model();
        var crew = new ShipCrew { Members = [new CrewMember("Ava Ross", 4521)] };

        var presenter = new TurnPresenter(model, model.ShipPicture, addressed => Announcer.CrewPicture(addressed.Name, crew));
        presenter.On(new TurnEvent.Addressed(VoiceRole.Crew, "Ava Ross", 1));
        presenter.On(new TurnEvent.TextDelta("On it, Commander."));

        Assert.Equal("crew.4521", model.Segments(TranscriptPage.Conversation)[^1].Picture);
    }

    [Fact]
    public void InventedCommsCarryNoPicture()
    {
        var model = Model();

        var presenter = new TurnPresenter(model, model.ShipPicture, _ => "crew.1");
        presenter.On(new TurnEvent.Addressed(VoiceRole.Comms, "Courier Vance", 1));
        presenter.On(new TurnEvent.TextDelta("Copy that."));

        Assert.Null(model.Segments(TranscriptPage.Conversation)[^1].Picture);
    }

    [Theory]
    [InlineData(VoiceRole.ShipAi, null, "core.warden")]
    [InlineData(VoiceRole.Narrator, null, "narrator")]
    [InlineData(VoiceRole.Crew, "Ava Ross", "crew.4521")]
    [InlineData(VoiceRole.Crew, "Nobody Hired", null)]
    [InlineData(VoiceRole.Comms, "Courier Vance", null)]
    public void ACalloutCarriesItsSpeakersPicture(VoiceRole role, string? speaker, string? expected)
    {
        var crew = new ShipCrew { Members = [new CrewMember("Ava Ross", 4521)] };
        var callout = new Announcement("some.key", "Line.") { Voice = role, Speaker = speaker };

        Assert.Equal(expected, Announcer.ConversationPicture(callout, "warden", _ => VoiceGender.Unlabelled, crew));
    }

    [Fact]
    public void ACastLineCarriesItsOwnPicture()
    {
        var line = new Announcement("story.line", "Line.") { Voice = VoiceRole.Comms, Speaker = "Juno", Picture = "the-test-story.juno" };

        Assert.Equal("the-test-story.juno", Announcer.ConversationPicture(line, "warden", _ => VoiceGender.Unlabelled, null));
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void EarlierWardenRepliesStillShowWardenAfterSwitchingToCora()
    {
        var fixture = new SpeakerPictureFixture("d47-each-turn-keeps-its-picture");
        fixture.Ship("core.warden", Colors.OrangeRed);
        fixture.Ship("core.cora", Colors.SteelBlue);

        var (_, panel) = fixture.Open();

        fixture.Model.Append("Warden replying.", picture: "core.warden");
        fixture.Model.Append("Where am I?", voice: TranscriptVoice.Commander);
        fixture.Model.Append("Cora replying.", picture: "core.cora");
        Dispatcher.UIThread.RunJobs();

        var pictures = SpeakerPictureFixture.Pictures(panel);

        Assert.Equal(2, pictures.Count);
        Assert.Same(fixture.Portraits.For("core.warden"), pictures[0].Source);
        Assert.Same(fixture.Portraits.For("core.cora"), pictures[1].Source);
    }
}
