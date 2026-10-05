using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>Every third chapter after the beacon must contain a beat of the activity the Commander has done least.</summary>
public sealed class EveryThirdChapterLeavesTheComfortZoneTests
{
    /// <summary>The Commander's own figures for passengers, organics and rescues, beside activities they have done.</summary>
    private const string Statistics = """
        "Combat":{ "Bounties_Claimed":212, "Combat_Bonds":35 }, "Mining":{ "Quantity_Mined":640 },
        "Exobiology":{ "Organic_Data":4 }, "Search_And_Rescue":{ "SearchRescue_Count":9 },
        "Passengers":{ "Passengers_Missions_Delivered":0 }
        """;

    private const string BeatsWithoutPassengers = """
        {"opening": "Again.", "reply": "Here.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Back."},
          {"title": "Any Mission", "function": "turn", "kind": "mission", "count": 2, "mission": null, "line": "Work."},
          {"title": "The Bounties", "function": "resolution", "kind": "bounty", "count": 3, "line": "Paid."}
        ]}
        """;

    private const string BeatsWithPassengers = """
        {"opening": "Again.", "reply": "Here.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Back."},
          {"title": "The Tourists", "function": "turn", "kind": "mission", "count": 2, "mission": "Mission_PassengerVIP", "line": "Seats."},
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

    [Fact]
    public async Task TheThirdChapterAfterTheBeaconIsAskedForTheActivity()
    {
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Saying(NextSpine), RoundScriptedLlmProvider.Saying(NextBeats),
                RoundScriptedLlmProvider.Saying(NextSpine), RoundScriptedLlmProvider.Saying(NextBeats),
                RoundScriptedLlmProvider.Saying(NextSpine), RoundScriptedLlmProvider.Saying(BeatsWithPassengers)),
            card: Card with { Length = "2-weeks" });
        fixtures.Director.Game = () => Loaded(1_000_000, statistics: Statistics);

        // A two-week story's beacon scan is narrated at the pick, so chapter one is the first after it.
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        fixtures.Finish("F1", fixtures.Stories.Current("F1")!.Chapters[^1], Now);
        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddDays(1), CancellationToken.None));
        fixtures.Finish("F1", fixtures.Stories.Current("F1")!.Chapters[^1], Now.AddDays(1));
        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddDays(2), CancellationToken.None));

        Assert.Null(fixtures.Asks[0].Story!.Comfort);
        Assert.Null(fixtures.Asks[1].Story!.Comfort);
        Assert.Equal("passenger mission", fixtures.Asks[2].Story!.Comfort?.Name);
        Assert.Contains(
            "The chapter must contain a \"mission\" objective whose family starts with Mission_Passenger.",
            fixtures.Provider.Requests[4].Prompt.History[0].Text);
    }

    [Fact]
    public async Task AComfortChapterWithoutAPassengerMissionIsRefused()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsWithoutPassengers), RoundScriptedLlmProvider.Saying(BeatsWithoutPassengers));
        var comfort = ChapterFit.LeastDone(Loaded(1_000_000, statistics: Statistics).Statistics);

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy()).GenerateAsync(
            new AdventureAsk(
                AdventureReach.Session,
                AdventureLength.Short,
                Story: new AdventureStory(Id, Card.Title, Card.Describe(), Secret.Secret, 3, 10, 5, Comfort: comfort)),
            Now,
            CancellationToken.None);

        Assert.Contains(
            "This chapter leaves the comfort zone and must contain a \"mission\" objective whose family starts with Mission_Passenger, for passenger mission.",
            outcome.Refusal);
    }
}
