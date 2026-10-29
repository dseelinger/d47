using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Storage;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>The Director rewrites the current objective of one named active short story, by remark or by refusal.</summary>
public sealed class AShortStorysObjectiveCanBeRewrittenTests
{
    private const string Key = "the-anchorage-run";

    private const string NoPlace = """{"home": false, "system": null}""";

    private static readonly DateTimeOffset ChangedAt = Now.AddHours(1);

    private const string ABountyAndADock = """
        {"opening": "x", "reply": "A bounty does the same.", "beats": [
          {"title": "Another Way", "function": "midpoint", "kind": "bounty", "count": 2, "line": "Two more."},
          {"title": "Home Again", "function": "finale", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Home."}
        ]}
        """;

    private static Adventure ShortStory(string key, string? storyId = null) => new()
    {
        Key = key,
        Name = "The Anchorage Run",
        Source = AdventureSource.Generated,
        Written = Now,
        Spine = new AdventureSpine { Premise = "A debt.", Want = "To pay it.", Stake = "Whether it can be.", Turn = "It is owed to you.", Ending = "It is forgiven." },
        Opening = "Go.",
        StoryId = storyId,
        Beats =
        [
            Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
            Beat("First Blood", "catalyst", new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 1 }, "One."),
            Beat("The Wing", "midpoint", new AdventureTrigger { Kind = TriggerKind.Bond, Count = 3 }, "Three bonds."),
            Beat("Home", "finale", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Home, System = "Tavell's Reach" }, "End."),
        ],
    };

    private static void Bond(StoryFixtures fixtures, int minutes) =>
        fixtures.Book.Observe(
            AdventureFixtures.Event($$"""{ "timestamp":"{{Stamp(Now.AddMinutes(minutes))}}", "event":"FactionKillBond", "Reward":5000, "AwardingFaction":"F", "VictimFaction":"V" }"""),
            "F1");

    /// <summary>A short story begun, the Lantern reached and a bounty taken, waiting on three kill bonds.</summary>
    private static StoryFixtures AtTheBond(params string[] answers) => AtTheBond(null, answers);

    private static StoryFixtures AtTheBond(Microsoft.Extensions.Logging.ILogger? logger, params string[] answers)
    {
        var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(answers.Select(answer => RoundScriptedLlmProvider.Saying(answer)).ToArray()), logger: logger);

        Assert.Null(fixtures.Book.Write("F1", ShortStory(Key)));
        Assert.Null(fixtures.Book.Begin("F1", Key, Now));
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(1)), "F1");
        fixtures.Book.Observe(AdventureFixtures.Event($$"""{ "timestamp":"{{Stamp(Now.AddMinutes(2))}}", "event":"Bounty", "TotalReward":100 }"""), "F1");
        fixtures.Director.Clock = () => ChangedAt;
        return fixtures;
    }

    private static IReadOnlyList<string> Titles(StoryFixtures fixtures) =>
        [.. fixtures.Book.Store.Find("F1", Key)!.Beats.Select(beat => beat.Title)];

    private static ActivityBars Bars() => new(null, new MemoryFileSystem(), NullLogger.Instance);

    [Fact]
    public async Task AShortStorysRemarkIsWrittenToTheLogOnce()
    {
        var lines = new List<string>();
        using var fixtures = AtTheBond(new LogCapture(lines), NoPlace, ABountyAndADock);

        await fixtures.Director.ReviseAdventureObjectiveAsync("F1", Key, " I would rather hunt a bounty. ", [], TestContext.Current.CancellationToken);

        Assert.Single(lines, line => line.StartsWith("Objective change asked: ", StringComparison.Ordinal));
        Assert.Contains("Objective change asked: I would rather hunt a bounty.", lines);
    }

    private sealed class LogCapture(List<string> lines) : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            lines.Add(formatter(state, exception));
    }

    [Fact]
    public async Task ARemarkProducesAProposalAndChangesNothing()
    {
        using var fixtures = AtTheBond(NoPlace, ABountyAndADock);

        var outcome = await fixtures.Director.ReviseAdventureObjectiveAsync("F1", Key, "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);

        Assert.Null(outcome.Refusal);

        var proposal = Assert.IsType<ObjectiveProposal>(outcome.Proposal);
        var ask = Assert.Single(fixtures.Asks);

        Assert.Equal("A bounty does the same.", proposal.Reply);
        Assert.Equal(["Another Way", "Home Again"], proposal.Beats.Select(beat => beat.Title));
        Assert.Same(proposal, fixtures.Director.AdventureProposal("F1", Key));
        Assert.Null(fixtures.Director.Proposal("F1"));
        Assert.Equal(["The Lantern", "First Blood", "The Wing", "Home"], Titles(fixtures));
        Assert.Null(ask.Story);
        Assert.Equal("I would rather hunt a bounty.", ask.Rewrite!.Remark);
        Assert.Equal(2, ask.Rewrite.From);
        Assert.False(fixtures.Director.IsRewriting("F1"));
    }

    [Fact]
    public async Task ApplyingReplacesTheCurrentObjectiveAndThoseAfterIt()
    {
        using var fixtures = AtTheBond(NoPlace, ABountyAndADock);

        await fixtures.Director.ReviseAdventureObjectiveAsync("F1", Key, "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);

        Assert.Null(fixtures.Director.ApplyAdventureObjective("F1", Key));
        Assert.Equal(["The Lantern", "First Blood", "Another Way", "Home Again"], Titles(fixtures));
        Assert.Equal("Another Way", fixtures.Book.Standing("F1", Key)!.CurrentBeat!.Title);
        Assert.Null(fixtures.Director.AdventureProposal("F1", Key));
    }

    [Fact]
    public async Task ApplyingAfterTheBeatChangedIsRefusedAndDropsTheProposal()
    {
        using var fixtures = AtTheBond(NoPlace, ABountyAndADock);

        await fixtures.Director.ReviseAdventureObjectiveAsync("F1", Key, "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);

        for (var minute = 3; minute < 6; minute++)
        {
            Bond(fixtures, minute);
        }

        Assert.Equal("The story has moved on from that objective.", fixtures.Director.ApplyAdventureObjective("F1", Key));
        Assert.Null(fixtures.Director.AdventureProposal("F1", Key));
        Assert.Equal(["The Lantern", "First Blood", "The Wing", "Home"], Titles(fixtures));
    }

    [Fact]
    public async Task DiscardingDropsTheProposalAndChangesNothing()
    {
        using var fixtures = AtTheBond(NoPlace, ABountyAndADock);

        await fixtures.Director.ReviseAdventureObjectiveAsync("F1", Key, "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);
        fixtures.Director.DiscardAdventureObjective("F1", Key);

        Assert.Null(fixtures.Director.AdventureProposal("F1", Key));
        Assert.Equal(["The Lantern", "First Blood", "The Wing", "Home"], Titles(fixtures));
        Assert.Equal("There is no change to apply.", fixtures.Director.ApplyAdventureObjective("F1", Key));
    }

    [Fact]
    public async Task AnAppliedChangeCountsTowardTheBarOfTheReplacedActivity()
    {
        using var fixtures = AtTheBond(NoPlace, ABountyAndADock);
        var bars = Bars();

        fixtures.Director.Bars = bars;
        await fixtures.Director.ReviseAdventureObjectiveAsync("F1", Key, "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);
        Assert.Equal(0, bars.Changed("F1", "bond"));

        Assert.Null(fixtures.Director.ApplyAdventureObjective("F1", Key));
        Assert.Equal(1, bars.Changed("F1", "bond"));
    }

    [Fact]
    public async Task AReplacementAsksForNoneOfTheReplacedActivityAndCountsNothing()
    {
        using var fixtures = AtTheBond(ABountyAndADock);
        var bars = Bars();

        for (var i = 0; i < ActivityBars.Threshold; i++)
        {
            bars.Count("F1", "scan");
        }

        fixtures.Director.Bars = bars;

        Assert.Null(await fixtures.Director.RefuseAdventureBeatAsync("F1", Key, TestContext.Current.CancellationToken));

        var ask = Assert.Single(fixtures.Asks);

        Assert.Null(ask.Story);
        Assert.Null(ask.Rewrite!.Remark);
        Assert.Equal(["bond", "scan"], ask.Barred!.Order());
        Assert.Equal(["The Lantern", "First Blood", "Another Way", "Home Again"], Titles(fixtures));
        Assert.Equal(0, bars.Changed("F1", "bond"));
    }

    [Fact]
    public async Task AReplacementWhoseDraftAsksForTheReplacedActivityIsNotUsed()
    {
        const string AgainBonds = """
            {"opening": "x", "reply": "ok", "beats": [
              {"title": "More Bonds", "function": "midpoint", "kind": "bond", "count": 2, "line": "Two more."},
              {"title": "Home Again", "function": "finale", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Home."}
            ]}
            """;

        using var fixtures = AtTheBond(AgainBonds, AgainBonds, AgainBonds);

        Assert.NotNull(await fixtures.Director.RefuseAdventureBeatAsync("F1", Key, TestContext.Current.CancellationToken));
        Assert.Equal(["The Lantern", "First Blood", "The Wing", "Home"], Titles(fixtures));
        Assert.False(fixtures.Director.IsRewriting("F1"));
    }

    [Fact]
    public async Task AChapterOfAStoryIsRefused()
    {
        using var fixtures = AtTheBond();
        Assert.Null(fixtures.Book.Write("F1", ShortStory("a-chapter", Id)));
        Assert.Null(fixtures.Book.Begin("F1", "a-chapter", Now));

        var outcome = await fixtures.Director.ReviseAdventureObjectiveAsync("F1", "a-chapter", "Something else.", [], TestContext.Current.CancellationToken);

        Assert.Equal("That is a chapter of a story. Change its objective from the story.", outcome.Refusal);
        Assert.Equal("That is a chapter of a story. Change its objective from the story.", await fixtures.Director.RefuseAdventureBeatAsync("F1", "a-chapter", TestContext.Current.CancellationToken));
        Assert.Empty(fixtures.Asks);
    }

    [Fact]
    public async Task AStoryNotBegunIsRefused()
    {
        using var fixtures = AtTheBond();
        Assert.Null(fixtures.Book.Write("F1", ShortStory("not-begun")));

        Assert.Equal("That story has not begun.", await fixtures.Director.RefuseAdventureBeatAsync("F1", "not-begun", TestContext.Current.CancellationToken));
        Assert.Empty(fixtures.Asks);
    }

    [Fact]
    public async Task AnAbandonedStoryIsRefused()
    {
        using var fixtures = AtTheBond();
        Assert.Null(fixtures.Book.Abandon("F1", Key, Now.AddMinutes(3)));

        var outcome = await fixtures.Director.ReviseAdventureObjectiveAsync("F1", Key, "Something else.", [], TestContext.Current.CancellationToken);

        Assert.Equal("That story was abandoned.", outcome.Refusal);
        Assert.Empty(fixtures.Asks);
    }

    [Fact]
    public async Task AFinishedStoryIsRefused()
    {
        using var fixtures = AtTheBond();

        for (var minute = 3; minute < 6; minute++)
        {
            Bond(fixtures, minute);
        }

        fixtures.Book.Observe(Jump(Home, Now.AddMinutes(7)), "F1");

        Assert.Equal("That story is done.", await fixtures.Director.RefuseAdventureBeatAsync("F1", Key, TestContext.Current.CancellationToken));
        Assert.Empty(fixtures.Asks);
    }

    [Fact]
    public async Task AStoryThatIsNotOnFileIsRefused()
    {
        using var fixtures = AtTheBond();

        var outcome = await fixtures.Director.ReviseAdventureObjectiveAsync("F1", "nothing-here", "Something else.", [], TestContext.Current.CancellationToken);

        Assert.Equal("There is no such story.", outcome.Refusal);
    }

    [Fact]
    public async Task OneRewriteRunsPerCommanderAtATime()
    {
        using var fixtures = AtTheBond();
        var gate = new TaskCompletionSource<AdventureOutcome>();
        var catalog = StoryFixtures.Catalog;
        var director = new StoryDirector(
            fixtures.Stories,
            fixtures.Book,
            () => catalog,
            (_, _, _) => gate.Task,
            () => StarPosition.Origin,
            _ => { },
            NullLogger.Instance);

        var first = director.RefuseAdventureBeatAsync("F1", Key, TestContext.Current.CancellationToken);

        Assert.True(director.IsRewriting("F1"));
        Assert.Equal("A different objective is already being written.", await director.RefuseAdventureBeatAsync("F1", Key, TestContext.Current.CancellationToken));

        var second = await director.ReviseAdventureObjectiveAsync("F1", Key, "Something else.", [], TestContext.Current.CancellationToken);

        Assert.Equal("A different objective is already being written.", second.Refusal);

        gate.SetResult(new AdventureOutcome(null, null, "Nothing came back.", []));

        Assert.Equal("Nothing came back.", await first);
        Assert.False(director.IsRewriting("F1"));
    }
}
