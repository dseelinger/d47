using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class ChatterTalksAboutTheSystemTests
{
    private static readonly JournalLocation Place = JournalLocation.Unknown with
    {
        Population = 5_000_000_000,
        ControllingPower = "Jerome Archer",
        PowerplayState = "Stronghold",
    };

    private static readonly SceneBeat Arrived = new(SceneBeatKind.Arrived, 1, "Camp", "Faction", "Democracy", "Planet");

    private static Announcement Remark(int variant) =>
        new($"{AmbientCallout.KeyPrefix}docked", "Quiet in here.") { Variant = variant };

    private static IEnumerable<int> Indices => Enumerable.Range(0, 300);

    [Fact]
    public void PopulationIsPutInWords()
    {
        Assert.Equal("a system of about 5 billion people", SystemTopics.Describe(5_000_000_000));
        Assert.Equal("a small colony of a few thousand", SystemTopics.Describe(4_200));
        Assert.Equal("a system of about 1.5 million people", SystemTopics.Describe(1_500_000));
    }

    [Fact]
    public void ThePowerAndItsStateAreTopics()
    {
        var known = SystemTopics.Known(Place);

        Assert.Contains("living under Jerome Archer", known);
        Assert.Contains("being in a Stronghold system", known);
        Assert.Contains("living in a system of about 5 billion people", known);
        Assert.Contains(
            "being in an Unoccupied system nobody has claimed",
            SystemTopics.Known(JournalLocation.Unknown with { PowerplayState = "Unoccupied" }));
    }

    [Fact]
    public void AllegianceGovernmentSecuritySecondEconomyAndFactionAreTopics()
    {
        var known = SystemTopics.Known(JournalLocation.Unknown with
        {
            SystemAllegiance = "Independent",
            SystemGovernment = "Democracy",
            SystemSecurity = "High Security",
            SystemSecondEconomy = "Extraction",
            SystemFaction = "C.O.N.T.R.A.I.L",
        });

        Assert.Contains("the Independent stance here", known);
        Assert.Contains("life under a Democracy", known);
        Assert.Contains("being in a High Security system", known);
        Assert.Contains("the Extraction side of the economy", known);
        Assert.Contains("how C.O.N.T.R.A.I.L runs things", known);
        Assert.Contains("life under an Anarchy", SystemTopics.Known(JournalLocation.Unknown with { SystemGovernment = "Anarchy" }));
    }

    [Fact]
    public void AFieldThatIsNullOrNoneGivesNoTopic()
    {
        Assert.Empty(SystemTopics.Known(JournalLocation.Unknown));
        Assert.Empty(SystemTopics.Known(JournalLocation.Unknown with { ControllingPower = "None", PowerplayState = "None", Population = 0 }));
    }

    [Fact]
    public void AnIdleShipRemarkTouchesOnTheSystemOnOneIndexInThreeAndNeverOtherwise()
    {
        var withTopic = Indices.Where(index =>
            FlavourBriefs.For(Remark(index), true, place: Place)!.Instruction.Contains("may touch on", StringComparison.Ordinal)).ToList();

        Assert.InRange(withTopic.Count, 60, 140);

        foreach (var index in Indices.Except(withTopic))
        {
            var brief = FlavourBriefs.For(Remark(index), true, place: Place)!;
            Assert.Equal(FlavourBriefs.For(Remark(index), true)!.Instruction, brief.Instruction);
        }
    }

    [Fact]
    public void AnIdleShipRemarkIsNotTiedToTheStoryIndex()
    {
        var topicAndStory = Indices.Count(index =>
            CommanderStory.TellsStory(index)
            && FlavourBriefs.For(Remark(index), true, place: Place)!.Instruction.Contains("may touch on", StringComparison.Ordinal));

        Assert.True(topicAndStory > 0);
    }

    [Fact]
    public void ASettlementArrivalTalksAboutTheSystemOnOneIndexInThreeAndNeverOtherwise()
    {
        var withTopic = Indices.Where(index =>
            NpcChatter.SceneInstruction(Arrived, "scenario", exchangeIndex: index, place: Place)
                .Contains("This time the remark is about", StringComparison.Ordinal)).ToList();

        Assert.InRange(withTopic.Count, 60, 140);

        foreach (var index in Indices.Except(withTopic))
        {
            Assert.Equal(
                NpcChatter.SceneInstruction(Arrived, "scenario", exchangeIndex: index),
                NpcChatter.SceneInstruction(Arrived, "scenario", exchangeIndex: index, place: Place));
        }
    }

    [Fact]
    public void OtherSettlementBeatsCarryNoSystemFact()
    {
        foreach (var kind in new[] { SceneBeatKind.Spotted, SceneBeatKind.CommanderDown, SceneBeatKind.Gone })
        {
            foreach (var index in Indices)
            {
                var beat = Arrived with { Kind = kind };

                Assert.DoesNotContain(
                    "This time the remark is about",
                    NpcChatter.SceneInstruction(beat, "scenario", exchangeIndex: index, place: Place),
                    StringComparison.Ordinal);
            }
        }
    }
}
