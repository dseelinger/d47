using D47.Core.Storage;
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

    /// <summary>The new objectives' words, then the fidelity check's answer.</summary>
    private static readonly string[] NewWords = [PlannedChapterWorld.NewWords(), PlannedChapterWorld.NoObjections];

    /// <summary>A rewrite that puts an arrival at the Lantern and one bounty after the objectives kept, as the generator would.</summary>
    private static AdventureOutcome ArriveAgainThenABounty(AdventureAsk ask) =>
        new(
            ask.Rewrite!.Chapter with
            {
                Beats =
                [
                    .. ask.Rewrite.Chapter.Beats.Take(ask.Rewrite.From),
                    Beat("Back Again", "midpoint", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Again."),
                    Beat("Home Again", "finale", Counted(TriggerKind.Bounty, 1), "Home."),
                ],
            },
            "ok",
            null,
            []);

    private static AdventureTrigger Counted(TriggerKind kind, int count, string? family = null) =>
        new() { Kind = kind, Count = count, MissionFamily = family };

    /// <summary>Five beats: an arrival, two bounties, three kill bonds and an arrival.</summary>
    private static Adventure Chapter() => StoryFixtures.GeneratedChapter(ChapterKey, "The Hard Way", Id, DebtSpine,
        Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
        Beat("First Blood", "catalyst", Counted(TriggerKind.Bounty, 1), "One."),
        Beat("Second Blood", "debate", Counted(TriggerKind.Bounty, 1), "Two."),
        Beat("The Wing", "midpoint", Counted(TriggerKind.Bond, 3), "Three bonds."),
        Beat("Home", "finale", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Home, System = "Tavell's Reach" }, "End."));

    /// <summary>The first three beats' events, in order.</summary>
    private static readonly IReadOnlyList<D47.Core.Journal.JournalEvent> BeforeTheRefusal =
    [
        Jump(Lantern, Now.AddMinutes(1)),
        AdventureFixtures.Event($$"""{ "timestamp":"{{Stamp(Now.AddMinutes(2))}}", "event":"Bounty", "TotalReward":100 }"""),
        AdventureFixtures.Event($$"""{ "timestamp":"{{Stamp(Now.AddMinutes(3))}}", "event":"Bounty", "TotalReward":100 }"""),
    ];

    /// <summary>A fixture whose galaxy has the positions a rewrite's plan is measured by, answering each of <paramref name="answers"/> in turn.</summary>
    private static StoryFixtures Fixtures(params string[] answers)
    {
        var fixtures = StoryFixtures.ScriptedWith(answers);

        fixtures.Galaxy.Positioned = true;
        return fixtures;
    }

    /// <summary>A story on chapter two with two clues given, the chapter begun, and the first three beats flown.</summary>
    private static StoryFixtures AtTheBond(params string[] answers)
    {
        var fixtures = Fixtures(answers);

        fixtures.SaveRunning(["chapter-one", ChapterKey], 2);

        Assert.Null(fixtures.Book.Write("F1", Chapter()));
        Assert.Null(fixtures.Book.Begin("F1", ChapterKey, Now));

        foreach (var journalEvent in BeforeTheRefusal)
        {
            fixtures.Book.Observe(journalEvent, "F1");
        }

        fixtures.Director.Clock = () => RefusedAt;
        return fixtures;
    }

    [Fact]
    public async Task RefusingABondBeatKeepsTheBeatsDoneAndReplacesTheRest()
    {
        using var fixtures = AtTheBond(NewWords);

        Assert.Equal(3, fixtures.Book.Standing("F1", ChapterKey)!.Fired.Count);
        Assert.Null(await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken));

        var chapter = fixtures.Book.Store.Find("F1", ChapterKey)!;

        Assert.Equal(["The Lantern", "First Blood", "Second Blood"], chapter.Beats.Take(3).Select(beat => beat.Title));
        Assert.Equal("New One", chapter.Beats[3].Title);
        Assert.DoesNotContain(chapter.Beats.Skip(3), beat => beat.Trigger.Kind == TriggerKind.Bond);
        Assert.Equal(3, chapter.RewrittenFrom);
        Assert.Equal(RefusedAt, chapter.RewrittenAt);

        var story = fixtures.Stories.Current("F1")!;

        Assert.Equal(["chapter-one", ChapterKey], story.Chapters);
        Assert.Equal(2, story.CluesGiven);
        Assert.Equal(["bond"], story.Refused);

        var standing = fixtures.Book.Standing("F1", ChapterKey)!;

        Assert.Equal(3, standing.Fired.Count);
        Assert.Equal("New One", standing.CurrentBeat!.Title);
    }

    [Fact]
    public async Task TheWriterIsToldWhatWasDoneAndWhatWasRefused()
    {
        using var fixtures = AtTheBond(NewWords);

        Assert.Null(await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken));

        var prompt = fixtures.Provider.Requests[0].Prompt.History[0].Text;

        Assert.Contains("They refused the objective they were on", prompt);
        Assert.Contains("The objectives the Commander has already done:", prompt);
        Assert.Contains("1. The Lantern", prompt);
        Assert.Contains("3. Second Blood", prompt);
        Assert.Contains("The objective replaced: The Wing", prompt);
        Assert.Contains("The new objectives, in order and numbered from 1", prompt);
    }

    [Fact]
    public async Task AReplacementArriveBeatWaitsForTheNextArrivalAfterTheRefusal()
    {
        using var fixtures = AtTheBond();

        fixtures.Answers = ArriveAgainThenABounty;

        // The Commander is at the Lantern when they refuse.
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(10)), "F1");

        Assert.Null(await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken));
        Assert.Equal(3, fixtures.Book.Standing("F1", ChapterKey)!.Fired.Count);

        fixtures.Book.Observe(Jump(Lantern, RefusedAt.AddMinutes(30)), "F1");

        Assert.Equal(4, fixtures.Book.Standing("F1", ChapterKey)!.Fired.Count);
    }

    [Fact]
    public async Task ReplayingTheJournalFromAcceptanceGivesTheStandingLiveGave()
    {
        using var fixtures = AtTheBond();

        fixtures.Answers = ArriveAgainThenABounty;

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

    [Fact]
    public async Task APartlyCountedBeatStartsFromNothingWhenItsReplacementIsCounted()
    {
        using var fixtures = AtTheBond();

        fixtures.Answers = ArriveAgainThenABounty;
        fixtures.Book.Observe(AdventureFixtures.Event($$"""{ "timestamp":"{{Stamp(Now.AddMinutes(20))}}", "event":"FactionKillBond", "Reward":5000, "AwardingFaction":"F", "VictimFaction":"V" }"""), "F1");
        Assert.Equal(1, fixtures.Book.Standing("F1", ChapterKey)!.Counted);

        Assert.Null(await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken));
        Assert.Equal(0, fixtures.Book.Standing("F1", ChapterKey)!.Counted);
    }

    [Fact]
    public async Task RefusingADockBeatRemembersNothing()
    {
        using var fixtures = Fixtures(NewWords);

        fixtures.SaveRunning(["chapter-one", ChapterKey]);

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

    [Fact]
    public async Task TheBeaconScanThatEndsActOneCannotBeRefused()
    {
        using var fixtures = StoryFixtures.ScriptedWith(Lines(), NothingObjected);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, TestContext.Current.CancellationToken));

        var key = Assert.Single(fixtures.Stories.Current("F1")!.Chapters);
        var steps = fixtures.Book.Store.Find("F1", key)!.Beats.SelectMany(beat => beat.Steps).Select(step => step.Trigger).ToList();

        fixtures.Fire("F1", key, steps[0], Now.AddMinutes(1));
        fixtures.Fire("F1", key, steps[1], Now.AddMinutes(2));

        Assert.Equal(TriggerKind.Beacon, fixtures.Book.Standing("F1", key)!.CurrentBeat!.Trigger.Kind);
        Assert.Null(fixtures.Director.RefusableBeat("F1"));
        Assert.NotNull(await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheRefusedActivitiesAreKeptOnDisk()
    {
        using var fixtures = AtTheBond(NewWords);

        Assert.Null(await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken));

        var reread = StoryStore.Open(fixtures.StoryPath, fixtures.Files, NullLogger<StoryStore>.Instance);

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

    [Fact]
    public void TheRefusedAdventureRoundTripsThroughTheFile()
    {
        var path = Path.Combine(MemoryInstall.FakeRoot, "d47-refused-beat", Guid.NewGuid().ToString("N"), "adventures.json");
        var files = new MemoryFileSystem();

        var store = new AdventureStore(path, files, NullLogger<AdventureStore>.Instance);
        Assert.Null(store.Save("F1", Chapter() with { AcceptedAt = Now, RewrittenAt = RefusedAt, RewrittenFrom = 3 }));

        var reread = new AdventureStore(path, files, NullLogger<AdventureStore>.Instance);
        reread.Poll();

        var back = Assert.Single(reread.For("F1"));

        Assert.Equal(RefusedAt, back.RewrittenAt);
        Assert.Equal(3, back.RewrittenFrom);
    }
}
