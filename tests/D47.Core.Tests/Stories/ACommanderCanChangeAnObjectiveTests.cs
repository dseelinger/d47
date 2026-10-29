using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>The Commander says what they would rather do; the objective is rewritten as a proposal that is held until applied or discarded.</summary>
public sealed class ACommanderCanChangeAnObjectiveTests
{
    private const string ChapterKey = "the-hard-way";

    private const string NoPlace = """{"home": false, "system": null}""";

    private static readonly DateTimeOffset ChangedAt = Now.AddHours(1);

    private const string ABountyAndADock = """
        {"opening": "x", "reply": "The bonds were there to put you in a wing. A bounty does the same.", "beats": [
          {"title": "Another Way", "function": "midpoint", "kind": "bounty", "count": 2, "line": "Two more."},
          {"title": "Home Again", "function": "finale", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Home."}
        ]}
        """;

    private const string ABountyAgain = """
        {"opening": "x", "reply": "A second thought.", "beats": [
          {"title": "Third Way", "function": "midpoint", "kind": "bounty", "count": 3, "line": "Three more."},
          {"title": "Home Again", "function": "finale", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Home."}
        ]}
        """;

    private static AdventureTrigger Counted(TriggerKind kind, int count) => new() { Kind = kind, Count = count };

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
            Beat("The Wing", "midpoint", Counted(TriggerKind.Bond, 3), "Three bonds."),
            Beat("Home", "finale", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Home, System = "Tavell's Reach" }, "End."),
        ],
    };

    private static void Bond(StoryFixtures fixtures, int minutes) =>
        fixtures.Book.Observe(
            AdventureFixtures.Event($$"""{ "timestamp":"{{Stamp(Now.AddMinutes(minutes))}}", "event":"FactionKillBond", "Reward":5000, "AwardingFaction":"F", "VictimFaction":"V" }"""),
            "F1");

    /// <summary>A story on chapter two, waiting on the three kill bonds.</summary>
    private static StoryFixtures AtTheBond(params string[] answers) => AtTheBond(null, answers);

    private static StoryFixtures AtTheBond(Microsoft.Extensions.Logging.ILogger? logger, params string[] answers)
    {
        var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(answers.Select(answer => RoundScriptedLlmProvider.Saying(answer)).ToArray()), logger: logger);

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
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(1)), "F1");
        fixtures.Book.Observe(AdventureFixtures.Event($$"""{ "timestamp":"{{Stamp(Now.AddMinutes(2))}}", "event":"Bounty", "TotalReward":100 }"""), "F1");
        fixtures.Director.Clock = () => ChangedAt;
        return fixtures;
    }

    private static IReadOnlyList<string> Titles(StoryFixtures fixtures) =>
        [.. fixtures.Book.Store.Find("F1", ChapterKey)!.Beats.Select(beat => beat.Title)];

    [Fact]
    public async Task ARemarkIsWrittenToTheLogOnce()
    {
        var lines = new List<string>();
        using var fixtures = AtTheBond(new LogCapture(lines), NoPlace, ABountyAndADock);

        await fixtures.Director.ReviseObjectiveAsync("F1", " I would rather hunt a bounty. ", [], TestContext.Current.CancellationToken);

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
    public async Task ARemarkProducesAProposalAndAReply()
    {
        using var fixtures = AtTheBond(NoPlace, ABountyAndADock);

        var outcome = await fixtures.Director.ReviseObjectiveAsync("F1", "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);

        Assert.Null(outcome.Refusal);

        var proposal = Assert.IsType<ObjectiveProposal>(outcome.Proposal);

        Assert.Equal("The bonds were there to put you in a wing. A bounty does the same.", proposal.Reply);
        Assert.Equal(["Another Way", "Home Again"], proposal.Beats.Select(beat => beat.Title));
        Assert.Same(proposal, fixtures.Director.Proposal("F1"));
        Assert.Equal(["The Lantern", "First Blood", "The Wing", "Home"], Titles(fixtures));
        Assert.False(fixtures.Director.IsRewriting("F1"));
    }

    [Fact]
    public async Task TheWriterIsToldTheCommanderWantsToChangeTheObjectiveAndNotThatTheyRefusedIt()
    {
        using var fixtures = AtTheBond(NoPlace, ABountyAndADock);

        await fixtures.Director.ReviseObjectiveAsync("F1", "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);

        var prompt = fixtures.Provider.Requests[1].Prompt.History[0].Text;

        Assert.Contains("They want to change the objective they were on", prompt);
        Assert.Contains("The objective the Commander wants to change: The Wing", prompt);
        Assert.Contains("The Commander now says: \"I would rather hunt a bounty.\"", prompt);
        Assert.Contains("Your reply never states the hidden story.", prompt);
        Assert.Contains("\"reply\"", prompt);
        Assert.DoesNotContain("They have refused the objective", prompt);
        Assert.DoesNotContain("The objective the Commander refused", prompt);
    }

    [Fact]
    public async Task AFurtherRemarkRevisesWithTheEarlierRoundsInTheExchange()
    {
        using var fixtures = AtTheBond(NoPlace, ABountyAndADock, NothingObjected, NoPlace, ABountyAgain);

        var first = await fixtures.Director.ReviseObjectiveAsync("F1", "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);
        var second = await fixtures.Director.ReviseObjectiveAsync("F1", "Make it three.", first.Proposal!.Exchange, TestContext.Current.CancellationToken);

        var prompt = fixtures.Provider.Requests[4].Prompt.History[0].Text;

        Assert.Contains("Commander: I would rather hunt a bounty.", prompt);
        Assert.Contains("You: The bonds were there to put you in a wing.", prompt);
        Assert.Contains("The Commander now says: \"Make it three.\"", prompt);
        Assert.Equal(["Third Way", "Home Again"], second.Proposal!.Beats.Select(beat => beat.Title));
        Assert.Equal(2, second.Proposal.Exchange.Count);
        Assert.Same(second.Proposal, fixtures.Director.Proposal("F1"));
    }

    [Fact]
    public async Task ApplyingAProposalReplacesTheCurrentObjectiveAndRemembersNothingAsRefused()
    {
        using var fixtures = AtTheBond(NoPlace, ABountyAndADock);

        await fixtures.Director.ReviseObjectiveAsync("F1", "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);

        Assert.Null(fixtures.Director.ApplyObjective("F1"));
        Assert.Equal(["The Lantern", "First Blood", "Another Way", "Home Again"], Titles(fixtures));

        var chapter = fixtures.Book.Store.Find("F1", ChapterKey)!;

        Assert.Equal(2, chapter.RewrittenFrom);
        Assert.Equal(ChangedAt, chapter.RewrittenAt);
        Assert.Equal("Another Way", fixtures.Book.Standing("F1", ChapterKey)!.CurrentBeat!.Title);
        Assert.Empty(fixtures.Stories.Current("F1")!.Refused);
        Assert.Null(fixtures.Director.Proposal("F1"));
    }

    [Fact]
    public async Task ApplyingAProposalRecordsTheChapterAsChanged()
    {
        using var fixtures = AtTheBond(NoPlace, ABountyAndADock);

        await fixtures.Director.ReviseObjectiveAsync("F1", "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);
        Assert.Empty(fixtures.Stories.Current("F1")!.ChangedChapters);
        Assert.Null(fixtures.Director.ApplyObjective("F1"));

        Assert.Equal([ChapterKey], fixtures.Stories.Current("F1")!.ChangedChapters);
    }

    [Fact]
    public async Task DiscardingAProposalRecordsNoChangedChapter()
    {
        using var fixtures = AtTheBond(NoPlace, ABountyAndADock);

        await fixtures.Director.ReviseObjectiveAsync("F1", "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);
        fixtures.Director.DiscardObjective("F1");

        Assert.Empty(fixtures.Stories.Current("F1")!.ChangedChapters);
    }

    [Fact]
    public async Task DiscardingAProposalChangesNothing()
    {
        using var fixtures = AtTheBond(NoPlace, ABountyAndADock);

        await fixtures.Director.ReviseObjectiveAsync("F1", "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);
        fixtures.Director.DiscardObjective("F1");

        Assert.Null(fixtures.Director.Proposal("F1"));
        Assert.Equal(["The Lantern", "First Blood", "The Wing", "Home"], Titles(fixtures));
        Assert.Null(fixtures.Book.Store.Find("F1", ChapterKey)!.RewrittenAt);
        Assert.Empty(fixtures.Stories.Current("F1")!.Refused);
        Assert.NotNull(fixtures.Director.ApplyObjective("F1"));
    }

    [Fact]
    public async Task AProposalMadeStaleByAFiredBeatIsRefusedAtApply()
    {
        using var fixtures = AtTheBond(NoPlace, ABountyAndADock);

        await fixtures.Director.ReviseObjectiveAsync("F1", "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);

        Bond(fixtures, 20);
        Bond(fixtures, 21);
        Bond(fixtures, 22);

        Assert.Equal("Home", fixtures.Book.Standing("F1", ChapterKey)!.CurrentBeat!.Title);
        Assert.NotNull(fixtures.Director.ApplyObjective("F1"));
        Assert.Equal(["The Lantern", "First Blood", "The Wing", "Home"], Titles(fixtures));
        Assert.Null(fixtures.Director.Proposal("F1"));
    }

    [Fact]
    public async Task AProposalIsRefusedAtApplyWhenTheStoryMovedOn()
    {
        using var fixtures = AtTheBond(NoPlace, ABountyAndADock);

        await fixtures.Director.ReviseObjectiveAsync("F1", "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);
        fixtures.Stories.Update("F1", Id, story => story with { Chapters = [.. story.Chapters, "chapter-three"] });

        Assert.NotNull(fixtures.Director.ApplyObjective("F1"));
        Assert.Equal(["The Lantern", "First Blood", "The Wing", "Home"], Titles(fixtures));
    }

    [Fact]
    public async Task AFailedWriteHoldsNoProposal()
    {
        using var fixtures = AtTheBond("this is not a story");

        var outcome = await fixtures.Director.ReviseObjectiveAsync("F1", "I would rather hunt a bounty.", [], TestContext.Current.CancellationToken);

        Assert.Null(outcome.Proposal);
        Assert.NotNull(outcome.Refusal);
        Assert.Null(fixtures.Director.Proposal("F1"));
        Assert.False(fixtures.Director.IsRewriting("F1"));
    }
}
