using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Storage;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A Commander who changes the same activity away five times is not asked for it again, in any story.</summary>
public sealed class FiveChangedObjectivesBarAnActivityTests
{
    private const string Courier = "mission:Mission_Courier";

    private const string NoPlace = """{"home": false, "system": null}""";

    private const string ABountyAndADock = """
        {"opening": "x", "reply": "A bounty does the same.", "beats": [
          {"title": "Another Way", "function": "midpoint", "kind": "bounty", "count": 2, "line": "Two more."},
          {"title": "Home Again", "function": "finale", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Home."}
        ]}
        """;

    private static ActivityBars Bars() => new(null, new MemoryFileSystem(), NullLogger.Instance);

    private static void Change(ActivityBars bars, string commander, string key, int times)
    {
        for (var i = 0; i < times; i++)
        {
            bars.Count(commander, key);
        }
    }

    [Fact]
    public void FourChangesBarNothingAndFiveBarTheActivity()
    {
        var bars = Bars();

        Change(bars, "F1", Courier, 4);
        Assert.Empty(bars.Barred("F1"));

        bars.Count("F1", Courier);
        Assert.Equal([Courier], bars.Barred("F1"));
    }

    [Fact]
    public void AnotherCommandersCountsAreSeparate()
    {
        var bars = Bars();

        Change(bars, "F1", Courier, 5);

        Assert.Equal([Courier], bars.Barred("F1"));
        Assert.Empty(bars.Barred("F2"));
        Assert.Equal(0, bars.Changed("F2", Courier));
    }

    [Fact]
    public void ABarOnAMissionFamilyRefusesEveryMissionWhoseFamilyStartsWithIt()
    {
        var bars = Bars();

        Change(bars, "F1", Courier, 5);

        Assert.True(RefusedActivities.Refuses(bars.Barred("F1"), TriggerKind.Mission, "Mission_Courier_Boom"));
        Assert.False(RefusedActivities.Refuses(bars.Barred("F1"), TriggerKind.Mission, "Mission_Delivery"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void TheCountsAndTheBarSurviveARestart()
    {
        var folder = Path.Combine(Path.GetTempPath(), "d47-bars-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "activity-bars.json");

        try
        {
            Directory.CreateDirectory(folder);

            var first = new ActivityBars(path, new DiskFileSystem(), NullLogger.Instance);
            Change(first, "F1", Courier, 5);
            Change(first, "F1", "bounty", 2);

            var second = new ActivityBars(path, new DiskFileSystem(), NullLogger.Instance);
            second.Load();

            Assert.Equal([Courier], second.Barred("F1"));
            Assert.Equal(2, second.Changed("F1", "bounty"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task AnAppliedReplacementCountsTheActivityItReplacedAndAKeptOneCountsNothing()
    {
        using var fixtures = AtTheBond();
        var bars = Bars();
        fixtures.Director.Bars = bars;

        await fixtures.Director.ReviseObjectiveAsync("F1", "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);
        fixtures.Director.DiscardObjective("F1");
        Assert.Equal(0, bars.Changed("F1", "bond"));

        await fixtures.Director.ReviseObjectiveAsync("F1", "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);
        Assert.Null(fixtures.Director.ApplyObjective("F1"));
        Assert.Equal(1, bars.Changed("F1", "bond"));
    }

    [Fact]
    public async Task ABarIsListedAsRefusedInTheNextChapterAskWithoutBeingSavedOnTheStory()
    {
        using var fixtures = AtTheBond();
        var bars = Bars();
        Change(bars, "F1", "bond", 5);
        fixtures.Director.Bars = bars;

        await fixtures.Director.ReviseObjectiveAsync("F1", "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);

        var prompt = fixtures.Provider.Requests[1].Prompt.History[0].Text;

        Assert.Contains("no objective may ask for any of them: earn combat kill bonds", prompt);
        Assert.Empty(fixtures.Stories.Current("F1")!.Refused);
    }

    [Fact]
    public async Task ANewStoryChapterIsAskedWithTheBarAndDrawnNothingBarred()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying("x"), RoundScriptedLlmProvider.Saying("x")));
        var bars = Bars();
        Change(bars, "F1", "bounty", 5);
        fixtures.Director.Bars = bars;

        fixtures.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            PickedAt = Now.AddDays(-30),
            BeaconScanAt = Now.AddDays(-20),
        });

        await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None);

        Assert.NotEmpty(fixtures.Asks);
        Assert.All(fixtures.Asks, ask =>
        {
            Assert.Contains("bounty", ask.Story!.Refused!);
            Assert.DoesNotContain(ask.Story.Slots ?? [], slot => slot.Kind == TriggerKind.Bounty);
        });
        Assert.Empty(fixtures.Stories.Current("F1")!.Refused);
    }

    [Fact]
    public async Task NeverAskThisCountsNothing()
    {
        using var fixtures = AtTheBond(ABountyAndADock, NothingObjected);
        var bars = Bars();
        fixtures.Director.Bars = bars;

        await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken);

        Assert.Equal(0, bars.Changed("F1", "bond"));
        Assert.Equal(["bond"], fixtures.Stories.Current("F1")!.Refused);
    }

    /// <summary>A story on chapter two, waiting on the kill bonds; with no answers given, scripted for two changed objectives.</summary>
    private static StoryFixtures AtTheBond(params string[] answers)
    {
        var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            (answers.Length > 0 ? answers : [NoPlace, ABountyAndADock, NothingObjected, NoPlace, ABountyAndADock, NothingObjected])
                .Select(answer => RoundScriptedLlmProvider.Saying(answer)).ToArray()));

        fixtures.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            PickedAt = Now.AddDays(-30),
            BeaconScanAt = Now.AddDays(-20),
            Chapters = ["chapter-one", "the-hard-way"],
            CluesGiven = 2,
        });

        Assert.Null(fixtures.Book.Write("F1", new Adventure
        {
            Key = "the-hard-way",
            Name = "The Hard Way",
            Source = AdventureSource.Generated,
            Written = Now,
            Spine = new AdventureSpine { Premise = "A debt.", Want = "To pay it.", Stake = "Whether it can be.", Turn = "It is owed to you.", Ending = "It is forgiven." },
            Opening = "Go.",
            StoryId = Id,
            Beats =
            [
                Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
                Beat("First Blood", "catalyst", new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 1 }, "One."),
                Beat("The Wing", "midpoint", new AdventureTrigger { Kind = TriggerKind.Bond, Count = 3 }, "Three bonds."),
                Beat("Home", "finale", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Home, System = "Tavell's Reach" }, "End."),
            ],
        }));
        Assert.Null(fixtures.Book.Begin("F1", "the-hard-way", Now));
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(1)), "F1");
        fixtures.Book.Observe(AdventureFixtures.Event($$"""{ "timestamp":"{{Stamp(Now.AddMinutes(2))}}", "event":"Bounty", "TotalReward":100 }"""), "F1");
        fixtures.Director.Clock = () => Now.AddHours(1);
        return fixtures;
    }
}
