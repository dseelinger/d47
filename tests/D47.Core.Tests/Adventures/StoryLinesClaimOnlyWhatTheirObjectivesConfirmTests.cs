using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Xunit;
using static D47.Core.Tests.Stories.PlannedChapterWorld;

namespace D47.Core.Tests.Adventures;

/// <summary>A line may state only what its objective's trigger confirms; one call reads the draft against the table and refused objectives are written again.</summary>
public sealed class StoryLinesClaimOnlyWhatTheirObjectivesConfirmTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private const string SecondObjected = """{"objections": [{"objective": 2, "quote": "another page", "kind": "history", "reason": "it says the Commander has read the ledger before."}]}""";

    private static async Task<(AdventureOutcome Outcome, RoundScriptedLlmProvider Provider)> Write(params string[] after)
    {
        var ask = Ask();
        var plan = await PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider([RoundScriptedLlmProvider.Saying(Lines(plan.Objectives.Count)), .. after.Select(reply => RoundScriptedLlmProvider.Saying(reply))]);
        var outcome = await Generator(provider).GenerateAsync(ask, Now, TestContext.Current.CancellationToken);

        return (outcome, provider);
    }

    [Trait("Category", "Gate")]
    [Fact]
    public void EveryTriggerKindSaysWhatItConfirms()
    {
        var missing = Enum.GetValues<TriggerKind>().Where(kind => !TriggerConfirms.Kinds.Contains(kind)).ToList();

        Assert.True(missing.Count == 0, $"Add an entry to TriggerConfirms for: {string.Join(", ", missing)}.");
        Assert.All(Enum.GetValues<TriggerKind>(), kind => Assert.False(string.IsNullOrWhiteSpace(TriggerConfirms.Of(kind))));
    }

    [Fact]
    public async Task TheFidelityCallShowsEachObjectiveItsEntryAndTheEnding()
    {
        var (outcome, provider) = await Write(NoObjections);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(3, provider.Requests.Count);

        var prompt = provider.Requests[1].Prompt.History[0].Text;

        Assert.Equal("story_fidelity", provider.Requests[1].ResponseSchema?.Name);
        Assert.Contains(TriggerConfirms.Rule, prompt);
        Assert.Contains($"Confirms: {TriggerConfirms.Of(TriggerKind.Bounty)}", prompt);
        Assert.Contains("\"The ledger has another page, the one.\"", prompt);
        Assert.Contains("(the last objective: its lines are the ending)", prompt);
    }

    [Fact]
    public async Task AnObjectiveTheFidelityCallNamesIsWrittenAgainAndTheOthersStand()
    {
        var (outcome, provider) = await Write(SecondObjected, Repair(2), NoObjections);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("story_line_repairs", provider.Requests[2].ResponseSchema?.Name);

        var repair = provider.Requests[2].Prompt.History[0].Text;

        Assert.Contains("it says the Commander has read the ledger before.", repair);
        Assert.Equal(["Part one", "Part two Again", "Part three"], outcome.Draft!.Beats.Take(3).Select(beat => beat.Title));
    }

    [Fact]
    public async Task AnObjectiveTheCheckPassedIsNotRefusedByALaterRound()
    {
        var (outcome, _) = await Write(
            SecondObjected,
            Repair(2),
            """{"objections": [{"objective": 1, "quote": "another page", "kind": "action", "reason": "it says the Commander was paid."}, {"objective": 3, "quote": "another page", "kind": "event", "reason": "it says no beacon was scanned."}]}""");

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(["Part one", "Part two Again", "Part three"], outcome.Draft!.Beats.Take(3).Select(beat => beat.Title));
    }

    [Theory]
    [InlineData("""{"objections": [{"objective": 2, "quote": "To one name", "kind": "fiction", "reason": "a record in the story."}]}""")]
    [InlineData("""{"objections": [{"objective": 2, "quote": "The hold took in the canisters", "kind": "action", "reason": "words the lines do not hold."}]}""")]
    [InlineData("""{"objections": [{"objective": 2, "kind": "action", "reason": "no quote."}]}""")]
    [InlineData("""{"objections": [{"objective": 2, "quote": "another page", "kind": "fiction", "reason": "a record in the story."}]}""")]
    public async Task AnObjectionThatQuotesNoCheckedStatementInTheLinesIsDropped(string objected)
    {
        var (outcome, provider) = await Write(objected);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(3, provider.Requests.Count);
    }

    [Fact]
    public async Task AnObjectionThatOutlastsTheRepairsFailsAsAFidelityRefusal()
    {
        const string again = """{"objections": [{"objective": 2, "quote": "legible now", "kind": "history", "reason": "it says the Commander has read the ledger before."}]}""";
        var (outcome, _) = await Write(SecondObjected, Repair(2), again, Repair(2), again);

        Assert.False(outcome.Succeeded);
        Assert.StartsWith("The story states what its objectives do not confirm: Fidelity check: Objective 2 (Part two Again)", outcome.Refusal);
    }

    [Fact]
    public async Task AStockStorysBeaconConfirmsItsCoresComeAboard()
    {
        var story = new AdventureStory("s", "S", "Public", "Hidden", Chapter: 1, DaysRunning: 3, DaysSinceBeacon: null, Stage: "Act One")
        {
            Beacon = new AdventureBeacon(13872878396833, "IC 2391 Sector MX-T b3-6"),
        };
        var ask = Ask(story);
        var plan = await PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider([
            RoundScriptedLlmProvider.Saying(Lines(plan.Objectives.Count)),
            RoundScriptedLlmProvider.Saying(NoObjections),
        ]);

        var outcome = await Generator(provider).GenerateAsync(ask, Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(TriggerKind.Beacon, outcome.Draft!.Beats[^1].Steps[^1].Trigger.Kind);
        Assert.Contains("the story's Guardian cores came aboard", TriggerConfirms.Of(TriggerKind.Beacon, story: true));
        Assert.DoesNotContain("cores", TriggerConfirms.Of(TriggerKind.Beacon));
        Assert.Contains($"Confirms: {TriggerConfirms.Of(TriggerKind.Beacon, story: true)}", provider.Requests[1].Prompt.History[0].Text);
    }

    [Fact]
    public async Task AFidelityReplyThatCannotBeReadObjectsToNothing()
    {
        var (outcome, _) = await Write("not json");

        Assert.True(outcome.Succeeded, outcome.Refusal);
    }
}
