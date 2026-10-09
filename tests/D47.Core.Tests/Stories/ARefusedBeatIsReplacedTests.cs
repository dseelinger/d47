using D47.Core.Adventures;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>The Commander refuses the beat a story chapter waits on; it and the beats after it are written again, and the activity is remembered.</summary>
public sealed class ARefusedBeatIsReplacedTests
{
    private const string ChapterKey = "the-hard-way";

    private static readonly DateTimeOffset RefusedAt = Now.AddHours(1);

    private const string BountyAndADock = """
        {"opening": "x", "reply": "ok", "beats": [
          {"title": "Another Way", "function": "midpoint", "kind": "bounty", "count": 2, "line": "Two more."},
          {"title": "Home Again", "function": "finale", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Home."}
        ]}
        """;

    private const string ArriveAgainThenABounty = """
        {"opening": "x", "reply": "ok", "beats": [
          {"title": "Back Again", "function": "midpoint", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Again."},
          {"title": "Home Again", "function": "finale", "kind": "bounty", "count": 1, "line": "Home."}
        ]}
        """;

    private static AdventureTrigger Counted(TriggerKind kind, int count, string? family = null) =>
        new() { Kind = kind, Count = count, MissionFamily = family };

    /// <summary>Five beats: an arrival, two bounties, three kill bonds and an arrival.</summary>
    private static Adventure Chapter() => new()
    {
        Key = ChapterKey,
        Name = "The Hard Way",
        Source = AdventureSource.Generated,
        Written = Now,
        Spine = new AdventureSpine { Premise = "A debt.", Want = "To pay it.", Stake = "Whether it can be.", Turn = "It is owed to you.", Ending = "It is forgiven." },
        Opening = "Go.",
        StoryId = Id,
        Beats =
        [
            Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
            Beat("First Blood", "catalyst", Counted(TriggerKind.Bounty, 1), "One."),
            Beat("Second Blood", "debate", Counted(TriggerKind.Bounty, 1), "Two."),
            Beat("The Wing", "midpoint", Counted(TriggerKind.Bond, 3), "Three bonds."),
            Beat("Home", "finale", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Home, System = "Tavell's Reach" }, "End."),
        ],
    };

    /// <summary>The first three beats' events, in order.</summary>
    private static readonly IReadOnlyList<D47.Core.Journal.JournalEvent> BeforeTheRefusal =
    [
        Jump(Lantern, Now.AddMinutes(1)),
        AdventureFixtures.Event($$"""{ "timestamp":"{{Stamp(Now.AddMinutes(2))}}", "event":"Bounty", "TotalReward":100 }"""),
        AdventureFixtures.Event($$"""{ "timestamp":"{{Stamp(Now.AddMinutes(3))}}", "event":"Bounty", "TotalReward":100 }"""),
    ];

    private static StoryFixtures Fixtures(params string[] answers) =>
        new(new RoundScriptedLlmProvider(answers.Select(answer => RoundScriptedLlmProvider.Saying(answer)).ToArray()));

    /// <summary>A story on chapter two with two clues given, the chapter begun, and the first three beats flown.</summary>
    private static StoryFixtures AtTheBond(params string[] answers)
    {
        var fixtures = Fixtures(answers);

        fixtures.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            PickedAt = Now.AddDays(-30),
            BeaconScanAt = Now.AddDays(-20),
            Chapters = ["chapter-one", ChapterKey],
            CluesGiven = 2,
        });

        Assert.Null(fixtures.Book.Write("F1", Chapter()));
        Assert.Null(fixtures.Book.Begin("F1", ChapterKey, Now));

        foreach (var journalEvent in BeforeTheRefusal)
        {
            fixtures.Book.Observe(journalEvent, "F1");
        }

        fixtures.Director.Clock = () => RefusedAt;
        return fixtures;
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task RefusingABondBeatKeepsTheBeatsDoneAndReplacesTheRest()
    {
        using var fixtures = AtTheBond(BountyAndADock);

        Assert.Equal(3, fixtures.Book.Standing("F1", ChapterKey)!.Fired.Count);
        Assert.Null(await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken));

        var chapter = fixtures.Book.Store.Find("F1", ChapterKey)!;

        Assert.Equal(["The Lantern", "First Blood", "Second Blood", "Another Way", "Home Again"], chapter.Beats.Select(beat => beat.Title));
        Assert.Equal(TriggerKind.Bounty, chapter.Beats[3].Trigger.Kind);
        Assert.Equal(3, chapter.RewrittenFrom);
        Assert.Equal(RefusedAt, chapter.RewrittenAt);

        var story = fixtures.Stories.Current("F1")!;

        Assert.Equal(["chapter-one", ChapterKey], story.Chapters);
        Assert.Equal(2, story.CluesGiven);
        Assert.Equal(["bond"], story.Refused);

        var standing = fixtures.Book.Standing("F1", ChapterKey)!;

        Assert.Equal(3, standing.Fired.Count);
        Assert.Equal("Another Way", standing.CurrentBeat!.Title);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheWriterIsToldWhatWasDoneAndWhatWasRefused()
    {
        using var fixtures = AtTheBond(BountyAndADock);

        Assert.Null(await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken));

        var prompt = fixtures.Provider.Requests[0].Prompt.History[0].Text;

        Assert.Contains("The objectives the Commander has already done:", prompt);
        Assert.Contains("1. The Lantern", prompt);
        Assert.Contains("3. Second Blood", prompt);
        Assert.Contains("The objective the Commander refused: The Wing", prompt);
        Assert.Contains("exactly 2 objectives, which replace objective 4 to the end of the chapter", prompt);
        Assert.Contains("refused these activities for this story, and no objective may ask for any of them: earn combat kill bonds", prompt);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AReplacementArriveBeatWaitsForTheNextArrivalAfterTheRefusal()
    {
        using var fixtures = AtTheBond(ArriveAgainThenABounty);

        // The Commander is at the Lantern when they refuse.
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(10)), "F1");

        Assert.Null(await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken));
        Assert.Equal(3, fixtures.Book.Standing("F1", ChapterKey)!.Fired.Count);

        fixtures.Book.Observe(Jump(Lantern, RefusedAt.AddMinutes(30)), "F1");

        Assert.Equal(4, fixtures.Book.Standing("F1", ChapterKey)!.Fired.Count);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ReplayingTheJournalFromAcceptanceGivesTheStandingLiveGave()
    {
        using var fixtures = AtTheBond(ArriveAgainThenABounty);

        var events = BeforeTheRefusal.Append(Jump(Lantern, Now.AddMinutes(10))).ToList();

        fixtures.Book.Observe(events[^1], "F1");
        Assert.Null(await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken));

        events.Add(Jump(Lantern, RefusedAt.AddMinutes(30)));
        events.Add(AdventureFixtures.Event($$"""{ "timestamp":"{{Stamp(RefusedAt.AddMinutes(40))}}", "event":"Bounty", "TotalReward":100 }"""));
        fixtures.Book.Observe(events[^2], "F1");
        fixtures.Book.Observe(events[^1], "F1");

        var live = fixtures.Book.Standing("F1", ChapterKey)!;
        var replayed = events.Aggregate(
            AdventureFold.Start(fixtures.Book.Store.Find("F1", ChapterKey)!),
            AdventureFold.Apply);

        Assert.Equal(live.Fired, replayed.Fired);
        Assert.Equal(live.Counted, replayed.Counted);
        Assert.Equal(5, replayed.Fired.Count);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task APartlyCountedBeatStartsFromNothingWhenItsReplacementIsCounted()
    {
        using var fixtures = AtTheBond(BountyAndADock);

        fixtures.Book.Observe(AdventureFixtures.Event($$"""{ "timestamp":"{{Stamp(Now.AddMinutes(20))}}", "event":"FactionKillBond", "Reward":5000, "AwardingFaction":"F", "VictimFaction":"V" }"""), "F1");
        Assert.Equal(1, fixtures.Book.Standing("F1", ChapterKey)!.Counted);

        Assert.Null(await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken));
        Assert.Equal(0, fixtures.Book.Standing("F1", ChapterKey)!.Counted);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task RefusingADockBeatRemembersNothing()
    {
        using var fixtures = Fixtures(ArriveAgainThenABounty);

        fixtures.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            PickedAt = Now.AddDays(-30),
            BeaconScanAt = Now.AddDays(-20),
            Chapters = ["chapter-one", ChapterKey],
        });

        Assert.Null(fixtures.Book.Write("F1", Chapter() with
        {
            Beats =
            [
                Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
                Beat("The Anchorage", "catalyst", new AdventureTrigger { Kind = TriggerKind.Dock, MarketId = Anchorage, System = "Dyson's Hollow", Station = "Maren Anchorage" }, "To one name."),
            ],
        }));
        Assert.Null(fixtures.Book.Begin("F1", ChapterKey, Now));
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(1)), "F1");
        fixtures.Director.Clock = () => RefusedAt;

        Assert.Equal(TriggerKind.Dock, fixtures.Director.RefusableBeat("F1")!.Trigger.Kind);
        Assert.Null(await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken));
        Assert.Empty(fixtures.Stories.Current("F1")!.Refused);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AFailedWriteLeavesTheBeatAndRemembersNothing()
    {
        using var fixtures = AtTheBond("this is not a story");

        var refusal = await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken);

        Assert.NotNull(refusal);

        var chapter = fixtures.Book.Store.Find("F1", ChapterKey)!;

        Assert.Equal(TriggerKind.Bond, chapter.Beats[3].Trigger.Kind);
        Assert.Null(chapter.RewrittenAt);
        Assert.Empty(fixtures.Stories.Current("F1")!.Refused);
        Assert.False(fixtures.Director.IsRewriting("F1"));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheBeaconScanThatEndsActOneCannotBeRefused()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, TestContext.Current.CancellationToken));

        var key = Assert.Single(fixtures.Stories.Current("F1")!.Chapters);
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(1)), "F1");
        fixtures.Book.Observe(Docked(Anchorage, Now.AddMinutes(2)), "F1");

        Assert.Equal(TriggerKind.Beacon, fixtures.Book.Standing("F1", key)!.CurrentBeat!.Trigger.Kind);
        Assert.Null(fixtures.Director.RefusableBeat("F1"));
        Assert.NotNull(await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheRefusedActivitiesAreKeptOnDisk()
    {
        using var fixtures = AtTheBond(BountyAndADock);

        Assert.Null(await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken));

        var reread = StoryStore.Open(fixtures.StoryPath, NullLogger<StoryStore>.Instance);

        Assert.Equal(["bond"], reread.Current("F1")!.Refused);
    }

    [Fact]
    public async Task TheModelCannotRefuseABeatAndTheCommanderCan()
    {
        var refused = 0;
        var registry = CapabilityRegistry.Build([AdventureCapability.Create(beatRefusal: new AdventureCapability.BeatRefusal
        {
            Refuse = _ =>
            {
                refused++;
                return Task.FromResult<string?>(null);
            },
        })]);

        var model = await registry.InvokeAsync(
            AdventureCapability.RefuseBeatTool, ToolArguments.Empty, caller: ToolCaller.Model, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(model.IsError);
        Assert.Equal(0, refused);

        var commander = await registry.InvokeAsync(
            AdventureCapability.RefuseBeatTool, ToolArguments.Empty, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(commander.IsError);
        Assert.Equal(1, refused);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheRefusedAdventureRoundTripsThroughTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "d47-refused-beat", Guid.NewGuid().ToString("N"), "adventures.json");

        try
        {
            var store = new AdventureStore(path, NullLogger<AdventureStore>.Instance);
            Assert.Null(store.Save("F1", Chapter() with { AcceptedAt = Now, RewrittenAt = RefusedAt, RewrittenFrom = 3 }));

            var reread = new AdventureStore(path, NullLogger<AdventureStore>.Instance);
            reread.Poll();

            var back = Assert.Single(reread.For("F1"));

            Assert.Equal(RefusedAt, back.RewrittenAt);
            Assert.Equal(3, back.RewrittenFrom);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
