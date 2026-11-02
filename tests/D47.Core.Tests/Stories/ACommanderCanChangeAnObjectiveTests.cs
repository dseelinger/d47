using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.DetourFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>The Commander says what they would rather do; one small call turns it into a detour or a question, held until applied or discarded.</summary>
public sealed class ACommanderCanChangeAnObjectiveTests
{
    private const string ChapterKey = "the-hard-way";

    private static readonly DateTimeOffset ChangedAt = Now.AddHours(1);

    private const string ABountyAndADock = """
        {"opening": "x", "reply": "The bonds were there to put you in a wing. A bounty does the same.", "beats": [
          {"title": "Another Way", "function": "midpoint", "kind": "bounty", "reason": "Someone there knows about the burst.", "count": 2, "line": "Two more."},
          {"title": "Home Again", "function": "finale", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Home."}
        ]}
        """;

    private static AdventureTrigger Counted(TriggerKind kind, int count) => new() { Kind = kind, Count = count };

    private static Adventure Chapter() => StoryFixtures.GeneratedChapter(ChapterKey, "The Hard Way", Id, DebtSpine,
        Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
        Beat("First Blood", "catalyst", Counted(TriggerKind.Bounty, 1), "One."),
        Beat("The Wing", "midpoint", Counted(TriggerKind.Bond, 3), "Three bonds."),
        Beat("Home", "finale", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Home, System = "Tavell's Reach" }, "End."));

    private static void Bond(StoryFixtures fixtures, int minutes) =>
        fixtures.Book.Observe(
            AdventureFixtures.Event($$"""{ "timestamp":"{{Stamp(Now.AddMinutes(minutes))}}", "event":"FactionKillBond", "Reward":5000, "AwardingFaction":"F", "VictimFaction":"V" }"""),
            "F1");

    /// <summary>A story on chapter two, waiting on the three kill bonds.</summary>
    private static StoryFixtures AtTheBond(params string[] answers) => AtTheBond(null, answers);

    private static StoryFixtures AtTheBond(Microsoft.Extensions.Logging.ILogger? logger, params string[] answers)
    {
        var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(answers.Select(answer => RoundScriptedLlmProvider.Saying(answer)).ToArray()), logger: logger);

        fixtures.SaveRunning(["chapter-one", ChapterKey], 2);

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
    public async Task TheWordsAreWrittenToTheLogOnce()
    {
        var lines = new List<string>();
        using var fixtures = AtTheBond(new LogCapture(lines));
        DetourFixtures.Give(fixtures, null, null, GoToAnlave);

        await fixtures.Director.ProposeDetourAsync("F1", " Go to Anlave. ", null, TestContext.Current.CancellationToken);

        Assert.Single(lines, line => line.StartsWith("Objective change asked: ", StringComparison.Ordinal));
        Assert.Contains("Objective change asked: Go to Anlave.", lines);
    }

    private sealed class LogCapture(List<string> lines) : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            lines.Add(formatter(state, exception));
    }

    [Fact]
    public async Task GoToAnlaveProposesAnArriveStepInOneCallWithNoChapterWrite()
    {
        var lines = new List<string>();
        using var fixtures = AtTheBond(new LogCapture(lines));
        var model = DetourFixtures.Give(fixtures, new LogCapture(lines), null, GoToAnlave);

        var outcome = await fixtures.Director.ProposeDetourAsync("F1", "Go to Anlave.", null, TestContext.Current.CancellationToken);

        Assert.Null(outcome.Refusal);

        var proposal = Assert.IsType<DetourProposal>(outcome.Proposal);
        var step = Assert.IsType<DetourStep>(proposal.Answer.Step);

        Assert.Equal(TriggerKind.Arrive, step.Trigger.Kind);
        Assert.Equal("Anlave", step.Trigger.System);
        Assert.Equal(DetourFixtures.Anlave, step.Trigger.SystemAddress);
        Assert.Equal("Anlave first, then the story.", proposal.Reply);
        Assert.Equal(1, model.CallCount);
        Assert.Empty(fixtures.Asks);
        Assert.Equal(0, fixtures.Provider.CallCount);
        Assert.DoesNotContain(lines, line => line.Contains("fidelity", StringComparison.OrdinalIgnoreCase));
        Assert.Same(proposal, fixtures.Director.Detour("F1"));
        Assert.Equal(["The Lantern", "First Blood", "The Wing", "Home"], Titles(fixtures));
        Assert.Null(fixtures.Stories.Current("F1")!.Detour);
        Assert.False(fixtures.Director.IsRewriting("F1"));
    }

    [Fact]
    public async Task TheModelIsToldTheObjectiveBeingLeftAndTheWords()
    {
        using var fixtures = AtTheBond();
        var model = DetourFixtures.Give(fixtures, null, null, GoToAnlave);

        await fixtures.Director.ProposeDetourAsync("F1", "Go to Anlave.", null, TestContext.Current.CancellationToken);

        var prompt = model.Requests[0].Prompt.History[0].Text;

        Assert.Contains("The objective they are leaving: earn 3 kill bonds.", prompt);
        Assert.Contains("What the Commander says now: \"Go to Anlave.\"", prompt);
        Assert.Contains("Where they are now: Oppi.", prompt);
    }

    [Fact]
    public async Task SomethingCloserToHomeAsksAQuestionWithAtMostThreeOptions()
    {
        using var fixtures = AtTheBond();
        DetourFixtures.Give(fixtures, null, null, WhichPlace);

        var outcome = await fixtures.Director.ProposeDetourAsync("F1", "Something closer to home.", null, TestContext.Current.CancellationToken);
        var proposal = outcome.Proposal!;

        Assert.True(proposal.Answer.IsQuestion);
        Assert.Equal("Which place counts as closer to home?", proposal.Reply);
        Assert.Equal(["Go to Oppi", "Go to Colonia", "Go to Anlave"], proposal.Answer.Options.Select(option => option.Title));
        Assert.All(proposal.Answer.Options, option => Assert.Equal(TriggerKind.Arrive, option.Trigger.Kind));
        Assert.Equal(1, proposal.Questions);
    }

    [Fact]
    public async Task AFactionNamedInAQuestionOffersItsThreeNearestSystems()
    {
        using var fixtures = AtTheBond();
        DetourFixtures.Give(fixtures, null, null, WhichPlaceAgain.Replace("\"faction\": null", "\"faction\": \"Anlave Free\"", StringComparison.Ordinal));

        var outcome = await fixtures.Director.ProposeDetourAsync("F1", "Somewhere the Anlave Free hold.", null, TestContext.Current.CancellationToken);

        Assert.Equal(["Go to Near Holding", "Go to Middle Holding", "Go to Far Holding"], outcome.Proposal!.Answer.Options.Select(option => option.Title));
    }

    [Fact]
    public async Task TwoVagueAnswersAskTwiceAndAThirdIsToldToNameAPlaceOrAnActivity()
    {
        using var fixtures = AtTheBond();
        DetourFixtures.Give(fixtures, null, null, WhichPlace, WhichPlaceAgain, WhichPlaceAgain);

        var first = (await fixtures.Director.ProposeDetourAsync("F1", "Something closer to home.", null, TestContext.Current.CancellationToken)).Proposal!;
        var second = (await fixtures.Director.ProposeDetourAsync("F1", "You know, closer.", first, TestContext.Current.CancellationToken)).Proposal!;
        var third = (await fixtures.Director.ProposeDetourAsync("F1", "Just closer.", second, TestContext.Current.CancellationToken)).Proposal!;

        Assert.True(second.Answer.IsQuestion);
        Assert.Equal("Closer to which place?", second.Reply);
        Assert.Equal(2, second.Questions);
        Assert.True(third.Answer.IsQuestion);
        Assert.Equal(DetourProposer.NamePlaceOrActivity, third.Reply);
        Assert.Empty(third.Answer.Options);
        Assert.Equal(3, third.Exchange.Count);
    }

    [Fact]
    public async Task AFurtherAnswerIsWrittenWithTheEarlierRounds()
    {
        using var fixtures = AtTheBond();
        var model = DetourFixtures.Give(fixtures, null, null, WhichPlace, GoToAnlave);

        var first = (await fixtures.Director.ProposeDetourAsync("F1", "Something closer to home.", null, TestContext.Current.CancellationToken)).Proposal!;
        var second = (await fixtures.Director.ProposeDetourAsync("F1", "Anlave.", first, TestContext.Current.CancellationToken)).Proposal!;

        var prompt = model.Requests[1].Prompt.History[0].Text;

        Assert.Contains("- The Commander: \"Something closer to home.\"", prompt);
        Assert.Contains("- You: \"Which place counts as closer to home?\"", prompt);
        Assert.Equal("Anlave", second.Answer.Step!.Trigger.System);
        Assert.Equal("Something closer to home. Anlave.", second.Words);
    }

    [Fact]
    public async Task APlaceTheGalaxySearchDoesNotKnowIsAQuestionNotAFailure()
    {
        using var fixtures = AtTheBond();
        DetourFixtures.Give(fixtures, null, null, GoToAnlave.Replace("\"system\": \"Anlave\"", "\"system\": \"Atlantis\"", StringComparison.Ordinal));

        var outcome = await fixtures.Director.ProposeDetourAsync("F1", "Go to Atlantis.", null, TestContext.Current.CancellationToken);

        Assert.Null(outcome.Refusal);
        Assert.True(outcome.Proposal!.Answer.IsQuestion);
        Assert.Equal("I could not find Atlantis. Which place do you mean?", outcome.Proposal.Reply);
    }

    [Fact]
    public async Task AnActivityTheStoryRefusedIsNotProposed()
    {
        using var fixtures = AtTheBond();
        fixtures.Stories.Update("F1", Id, story => story with { Refused = ["bounty"] });
        DetourFixtures.Give(fixtures, null, null, HuntBounties);

        var outcome = await fixtures.Director.ProposeDetourAsync("F1", "Hunt bounties.", null, TestContext.Current.CancellationToken);

        Assert.Null(outcome.Proposal!.Answer.Step);
        Assert.StartsWith("This story no longer asks for", outcome.Proposal.Reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnActivityTheCommanderBarredIsNotProposed()
    {
        using var fixtures = AtTheBond();
        var bars = new ActivityBars(null, new D47.Core.Storage.MemoryFileSystem(), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        for (var i = 0; i < 5; i++)
        {
            bars.Count("F1", "bounty");
        }

        fixtures.Director.Bars = bars;
        DetourFixtures.Give(fixtures, null, null, HuntBounties);

        var outcome = await fixtures.Director.ProposeDetourAsync("F1", "Hunt bounties.", null, TestContext.Current.CancellationToken);

        Assert.Null(outcome.Proposal!.Answer.Step);
        Assert.Empty(fixtures.Stories.Current("F1")!.Refused);
    }

    [Fact]
    public async Task UseThisSetsTheDetourAndSetsTheChapterAside()
    {
        using var fixtures = AtTheBond();
        DetourFixtures.Give(fixtures, null, null, GoToAnlave);

        await fixtures.Director.ProposeDetourAsync("F1", "Go to Anlave.", null, TestContext.Current.CancellationToken);

        Assert.Null(fixtures.Director.ApplyDetour("F1"));

        var story = fixtures.Stories.Current("F1")!;
        var detour = fixtures.Book.Standing("F1", story.Detour!.Key)!;

        Assert.Equal(ChapterKey, story.Detour.Chapter);
        Assert.Equal("Go to Anlave.", story.Detour.Words);
        Assert.Equal("Anlave", detour.CurrentTrigger!.System);
        Assert.True(fixtures.Book.Store.Find("F1", ChapterKey)!.IsSetAside);
        Assert.Equal([ChapterKey], story.ChangedChapters);
        Assert.Empty(story.Refused);
        Assert.Null(fixtures.Director.Detour("F1"));
    }

    [Fact]
    public async Task PickingAnOptionSetsThatDetour()
    {
        using var fixtures = AtTheBond();
        DetourFixtures.Give(fixtures, null, null, WhichPlace);

        await fixtures.Director.ProposeDetourAsync("F1", "Something closer to home.", null, TestContext.Current.CancellationToken);

        Assert.Null(fixtures.Director.ApplyDetour("F1", 1));

        var story = fixtures.Stories.Current("F1")!;

        Assert.Equal("Colonia", fixtures.Book.Standing("F1", story.Detour!.Key)!.CurrentTrigger!.System);
        Assert.Equal("Something closer to home. Go to Colonia.", story.Detour.Words);
    }

    [Fact]
    public async Task AQuestionWithoutAPickedOptionAppliesNothing()
    {
        using var fixtures = AtTheBond();
        DetourFixtures.Give(fixtures, null, null, WhichPlace);

        await fixtures.Director.ProposeDetourAsync("F1", "Something closer to home.", null, TestContext.Current.CancellationToken);

        Assert.Equal("There is no change to apply.", fixtures.Director.ApplyDetour("F1"));
        Assert.Null(fixtures.Stories.Current("F1")!.Detour);
    }

    [Fact]
    public async Task KeepingTheCurrentOneChangesNothing()
    {
        using var fixtures = AtTheBond();
        DetourFixtures.Give(fixtures, null, null, GoToAnlave);

        await fixtures.Director.ProposeDetourAsync("F1", "Go to Anlave.", null, TestContext.Current.CancellationToken);
        fixtures.Director.DiscardDetour("F1");

        Assert.Null(fixtures.Director.Detour("F1"));
        Assert.Equal(["The Lantern", "First Blood", "The Wing", "Home"], Titles(fixtures));
        Assert.Empty(fixtures.Stories.Current("F1")!.ChangedChapters);
        Assert.Null(fixtures.Stories.Current("F1")!.Detour);
        Assert.Equal("There is no change to apply.", fixtures.Director.ApplyDetour("F1"));
    }

    [Fact]
    public async Task ChangingAnOpenDetourReplacesItAndKeepsTheChapterSetAside()
    {
        using var fixtures = AtTheBond();
        DetourFixtures.Give(fixtures, null, null, GoToAnlave, HuntBounties);

        await fixtures.Director.ProposeDetourAsync("F1", "Go to Anlave.", null, TestContext.Current.CancellationToken);
        Assert.Null(fixtures.Director.ApplyDetour("F1"));
        var first = fixtures.Stories.Current("F1")!.Detour!;

        Assert.NotNull(fixtures.Director.ChangeableBeat("F1"));

        var outcome = await fixtures.Director.ProposeDetourAsync("F1", "Hunt bounties instead.", null, TestContext.Current.CancellationToken);
        Assert.Equal(first.Key, outcome.Proposal!.WaitedOn);
        Assert.Null(fixtures.Director.ApplyDetour("F1"));

        var story = fixtures.Stories.Current("F1")!;

        Assert.NotEqual(first.Key, story.Detour!.Key);
        Assert.Equal(ChapterKey, story.Detour.Chapter);
        Assert.Null(fixtures.Book.Store.Find("F1", first.Key));
        Assert.Equal(TriggerKind.Bounty, fixtures.Book.Standing("F1", story.Detour.Key)!.CurrentTrigger!.Kind);
        Assert.Equal([ChapterKey], story.ChangedChapters);
        Assert.True(fixtures.Book.Store.Find("F1", ChapterKey)!.IsSetAside);
    }

    [Fact]
    public async Task AProposalMadeStaleByAFiredBeatIsRefusedAtApply()
    {
        using var fixtures = AtTheBond();
        DetourFixtures.Give(fixtures, null, null, GoToAnlave);

        await fixtures.Director.ProposeDetourAsync("F1", "Go to Anlave.", null, TestContext.Current.CancellationToken);

        Bond(fixtures, 20);
        Bond(fixtures, 21);
        Bond(fixtures, 22);

        Assert.Equal("The story has moved on from that objective.", fixtures.Director.ApplyDetour("F1"));
        Assert.Null(fixtures.Stories.Current("F1")!.Detour);
        Assert.Null(fixtures.Director.Detour("F1"));
    }

    [Fact]
    public async Task ApplyingAfterTheStoryMovedOnSaysTheStoryChangedAndDropsTheProposal()
    {
        using var fixtures = AtTheBond();
        DetourFixtures.Give(fixtures, null, null, GoToAnlave);

        await fixtures.Director.ProposeDetourAsync("F1", "Go to Anlave.", null, TestContext.Current.CancellationToken);
        fixtures.Stories.Update("F1", Id, story => story with { PickedAt = story.PickedAt.AddSeconds(1) });

        Assert.Equal("The story changed while the objective was being written.", fixtures.Director.ApplyDetour("F1"));
        Assert.Null(fixtures.Director.Detour("F1"));
        Assert.Equal(["The Lantern", "First Blood", "The Wing", "Home"], Titles(fixtures));
    }

    [Fact]
    public async Task AStoryThatMovedOnWhileTheProposalWasWrittenHoldsNone()
    {
        using var fixtures = AtTheBond();
        DetourFixtures.Give(fixtures, null, () => fixtures.Stories.Update("F1", Id, story => story with { PickedAt = story.PickedAt.AddSeconds(1) }), GoToAnlave);

        var outcome = await fixtures.Director.ProposeDetourAsync("F1", "Go to Anlave.", null, TestContext.Current.CancellationToken);

        Assert.Equal("The story changed while the objective was being written.", outcome.Refusal);
        Assert.Null(fixtures.Director.Detour("F1"));
        Assert.False(fixtures.Director.IsRewriting("F1"));
    }

    [Fact]
    public async Task AnAnswerThatCannotBeReadAsksAgain()
    {
        using var fixtures = AtTheBond();
        DetourFixtures.Give(fixtures, null, null, "this is not a detour");

        var outcome = await fixtures.Director.ProposeDetourAsync("F1", "Go to Anlave.", null, TestContext.Current.CancellationToken);

        Assert.Null(outcome.Refusal);
        Assert.Equal(DetourProposer.NotUnderstood, outcome.Proposal!.Reply);
        Assert.False(fixtures.Director.IsRewriting("F1"));
    }

    [Fact]
    public async Task AStoryThatMovedOnWhileTheRefusalWasWrittenKeepsItsObjective()
    {
        using var fixtures = AtTheBond(ABountyAndADock);
        fixtures.WhileWriting = () => fixtures.Stories.Update("F1", Id, story => story with { Chapters = [.. story.Chapters, "chapter-three"] });

        Assert.Equal(
            "The story changed while the objective was being written.",
            await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken));
        Assert.Equal(["The Lantern", "First Blood", "The Wing", "Home"], Titles(fixtures));
        Assert.Empty(fixtures.Stories.Current("F1")!.Refused);
        Assert.False(fixtures.Director.IsRewriting("F1"));
    }
}
