using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

public class TheShipsAiIsNamedForThePersonaTests
{
    private static IReadOnlyList<string?> Speakers(PanelViewModel model) =>
        [.. model.Segments(TranscriptPage.Conversation).Select(segment => segment.Speaker)];

    [Fact]
    public void ReplyErrorAndProposalCardsAreAllNamedForTheShipsAi()
    {
        var model = new PanelViewModel { ShipNameSource = () => "Cora" };

        model.Append("Standing by, Commander.");
        model.AppendError("The provider did not answer.");
        model.AppendProposal("p1", "Raise the volume to forty percent.");

        Assert.Equal(["Cora", "Cora", "Cora"], Speakers(model));
    }

    [Fact]
    public void ATurnAlreadyWrittenKeepsItsNameWhenTheShipIsRenamed()
    {
        var name = "COVAS";
        var model = new PanelViewModel { ShipNameSource = () => name };

        model.Append("Standing by, Commander.");
        name = "Vesper";
        model.Append("Holding at Fixture Anchorage.");

        Assert.Equal(["COVAS", "Vesper"], Speakers(model));
    }

    [Fact]
    public void ACallerNamedSpeakerIsNotReplacedByTheShipsName()
    {
        var model = new PanelViewModel { ShipNameSource = () => "Cora" };

        model.Append("Sacred Fire clear.", speaker: "Tower");
        model.Append("where am I", voice: TranscriptVoice.Commander);

        Assert.Equal(["Tower", "CMDR"], Speakers(model));
    }
}
