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
        await fixtures.Director.WriteNextAsync("F1", Now.AddDays(2), CancellationToken.None);

        var first = fixtures.Asks[2].Story!.Slots![0];

        Assert.Equal(TriggerKind.Mission, first.Kind);
        Assert.StartsWith("Mission_Passenger", first.Family);
    }

    [Fact]
    public async Task AComfortChaptersPassengerMissionIsAnObjectiveOfItsPlan()
    {
        var passengers = new DrawnActivity(TriggerKind.Mission, "Mission_PassengerVIP", 2);
        var ask = PlannedChapterWorld.Ask(PlannedChapterWorld.Story with { Slots = [passengers] });
        var plan = await PlannedChapterWorld.PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(PlannedChapterWorld.Lines(plan.Objectives.Count)), RoundScriptedLlmProvider.Saying(PlannedChapterWorld.NoObjections));

        var outcome = await PlannedChapterWorld.Generator(provider).GenerateAsync(ask, Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains(outcome.Draft!.Beats.SelectMany(beat => beat.Steps), step => step.Trigger is { Kind: TriggerKind.Mission, MissionFamily: "Mission_PassengerVIP", Count: 2 });
    }
}
