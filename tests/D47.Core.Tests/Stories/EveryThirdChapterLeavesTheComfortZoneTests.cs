using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>Every third chapter after the beacon has the activity the Commander has done least as its first drawn activity.</summary>
public sealed class EveryThirdChapterLeavesTheComfortZoneTests
{
    /// <summary>The Commander's own figures for passengers, organics and rescues, beside activities they have done.</summary>
    private const string Statistics = """
        "Combat":{ "Bounties_Claimed":212, "Combat_Bonds":35 }, "Mining":{ "Quantity_Mined":640 },
        "Exobiology":{ "Organic_Data":4 }, "Search_And_Rescue":{ "SearchRescue_Count":9 },
        "Passengers":{ "Passengers_Missions_Delivered":0 }
        """;

    /// <summary>A passenger cabin, which the passenger missions are drawn only for.</summary>
    private const string Cabin = """{"Slot":"Slot02_Size3","Item":"int_passengercabin_size3_class1","On":true}""";

    private const string BeatsWithoutPassengers = """
        {"opening": "Again.", "reply": "Here.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Back."},
          {"title": "Any Mission", "function": "turn", "kind": "mission", "count": 2, "mission": null, "line": "Work."},
          {"title": "The Bounties", "function": "resolution", "kind": "bounty", "count": 3, "line": "Paid."}
        ]}
        """;

    [Fact]
    public void TheCommandersFiguresPickPassengerMissions()
    {
        var picked = ChapterFit.LeastDone(Loaded(1_000_000, statistics: Statistics).Statistics);

        Assert.Equal("passenger mission", picked?.Name);
        Assert.Equal(TriggerKind.Mission, picked?.Kind);
    }

    [Fact]
    public void ATieGoesToTheEarlierRowAndNoStatisticsPicksNothing()
    {
        Assert.Equal("bounty", ChapterFit.LeastDone(Loaded(1, statistics: "\"Combat\":{ \"Bounties_Claimed\":0 }").Statistics)?.Name);
        Assert.Null(ChapterFit.LeastDone(CareerStatistics.Empty));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheThirdChapterAfterTheBeaconIsAskedForTheActivity()
    {
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Saying(NextSpine), RoundScriptedLlmProvider.Saying(NextBeats), RoundScriptedLlmProvider.Saying(NothingObjected),
                RoundScriptedLlmProvider.Saying(NextSpine), RoundScriptedLlmProvider.Saying(NextBeats), RoundScriptedLlmProvider.Saying(NothingObjected),
                RoundScriptedLlmProvider.Saying(NextSpine), RoundScriptedLlmProvider.Saying(NextBeats), RoundScriptedLlmProvider.Saying(NothingObjected)),
            card: Card with { Length = "2-weeks" });
        CommanderGameState? game = null;
        fixtures.Director.Game = () => game;

        // A two-week story's beacon scan is narrated at the pick, so chapter one is the first after it.
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        fixtures.Finish("F1", fixtures.Stories.Current("F1")!.Chapters[^1], Now);
        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddDays(1), CancellationToken.None));
        fixtures.Finish("F1", fixtures.Stories.Current("F1")!.Chapters[^1], Now.AddDays(1));
        game = Loaded(1_000_000, statistics: Statistics, modules: Cabin);
        fixtures.WritesDrawnChapters = true;
        await fixtures.Director.WriteNextAsync("F1", Now.AddDays(2), CancellationToken.None);

        var first = fixtures.Asks[2].Story!.Slots![0];

        Assert.Equal(TriggerKind.Mission, first.Kind);
        Assert.StartsWith("Mission_Passenger", first.Family);
        Assert.Contains($"whose family is exactly {first.Family}", fixtures.Provider.Requests[6].Prompt.History[0].Text);
    }

    [Fact]
    public async Task AComfortChapterWithoutAPassengerMissionIsRefused()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsWithoutPassengers), RoundScriptedLlmProvider.Saying(Repairs.Replacing(BeatsWithoutPassengers, 1)), RoundScriptedLlmProvider.Saying(Repairs.Replacing(BeatsWithoutPassengers, 1)));
        var passengers = new DrawnActivity(TriggerKind.Mission, "Mission_PassengerVIP", 2);

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy()).GenerateAsync(
            new AdventureAsk(
                AdventureReach.Session,
                AdventureLength.Short,
                Story: new AdventureStory(Id, Card.Title, Card.Describe(), Secret.Secret, 3, 10, 5, Slots: [passengers])),
            Now,
            CancellationToken.None);

        Assert.Contains(
            "has no objective for this one: ",
            outcome.Refusal);
        Assert.Contains("a \"mission\" objective whose family is exactly Mission_PassengerVIP, count 2", outcome.Refusal);
    }
}
