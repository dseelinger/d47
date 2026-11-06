using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>A chapter's wording is checked against the plain-words rule, with repair rounds counted apart from the place and fidelity rounds.</summary>
public sealed class AChapterIsCheckedForPlainWordsBeforeTheCommanderSeesItTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private const string Plain = """{"objections": []}""";

    private const string LineObjected = """{"objections": [{"part": "objective 2", "quote": "another page", "kind": "figurative", "reason": "it calls the Commander's goal a page."}]}""";

    private const string NotInTheLine = """{"objections": [{"part": "objective 2", "quote": "a door in the sky", "kind": "figurative", "reason": "it is a metaphor."}]}""";

    private const string AgainObjected = """{"objections": [{"part": "objective 2", "quote": "legible now", "kind": "figurative", "reason": "it calls the Commander's goal a page."}]}""";

    private const string OpeningObjected = """{"objections": [{"part": "opening", "quote": "A ledger sits in the hold", "kind": "figurative", "reason": "it does not say who owns the ledger."}]}""";

    private static async Task<(AdventureOutcome Outcome, RoundScriptedLlmProvider Provider)> Write(params string[] replies)
    {
        var ask = PlannedChapterWorld.Ask();
        var plan = await PlannedChapterWorld.PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider([RoundScriptedLlmProvider.Saying(PlannedChapterWorld.Lines(plan.Objectives.Count)), .. replies.Select(reply => RoundScriptedLlmProvider.Saying(reply))]) { ScriptsPlainChecks = true };
        var outcome = await PlannedChapterWorld.Generator(provider).GenerateAsync(ask, Now, TestContext.Current.CancellationToken);

        return (outcome, provider);
    }

    private static IEnumerable<string?> Schemas(RoundScriptedLlmProvider provider) => provider.Requests.Select(request => request.ResponseSchema?.Name);

    [Fact]
    public async Task AnObjectionQuotingAnObjectivesLineSendsThatObjectiveThroughARepairRound()
    {
        var (outcome, provider) = await Write(Plain, LineObjected, PlannedChapterWorld.Repair(2), Plain, Plain);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(["story_lines", "story_fidelity", "story_plain", "story_line_repairs", "story_fidelity", "story_plain"], Schemas(provider));

        var repair = provider.Requests[3].Prompt.History[0].Text;

        Assert.Contains("it calls the Commander's goal a page.", repair);
        Assert.Equal("The ledger's two page is legible now.", outcome.Draft!.Beats[1].Lines[0].Text);
    }

    [Fact]
    public async Task AnObjectionWhoseQuoteIsNotInThePartItNamesIsDropped()
    {
        var (outcome, provider) = await Write(Plain, NotInTheLine);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(3, provider.Requests.Count);
    }

    [Fact]
    public async Task ADraftStillObjectedToAfterTwoRoundsFailsAsNotWrittenPlainly()
    {
        var (outcome, provider) = await Write(Plain, LineObjected, PlannedChapterWorld.Repair(2), Plain, AgainObjected, PlannedChapterWorld.Repair(2), Plain, AgainObjected);

        Assert.False(outcome.Succeeded);
        Assert.StartsWith("The story is not written plainly: ", outcome.Refusal);
        Assert.Contains("legible now", outcome.Refusal);
        Assert.Equal(2, Schemas(provider).Count(schema => schema == "story_line_repairs"));
    }

    [Fact]
    public async Task AnOpeningObjectionRewritesTheOpeningAndKeepsTheObjectives()
    {
        var plainOpening = """{"objectives": [], "opening": [{"text": "A trader pays for news of the freight.", "speaker": "ship"}]}""";
        var (outcome, provider) = await Write(Plain, OpeningObjected, plainOpening, Plain, Plain);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains("Write again the opening", provider.Requests[3].Prompt.History[0].Text);
        Assert.Equal("A trader pays for news of the freight.", Assert.Single(outcome.Draft!.OpeningLines).Text);
    }

    private const string SpineObjected = """{"objections": [{"part": "premise", "quote": "ledger turns up", "kind": "figurative", "reason": "a ledger stands for the Commander's standing."}]}""";

    private const string PlainerSpine = """
        {"objectives": [], "name": "The Lost Cargo", "premise": "Freight went missing from Ossen's Lantern.", "want": "To find the missing freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "The freight was never loaded.", "ending": "The freight is accounted for."}
        """;

    [Fact]
    public async Task ASpineObjectionAsksForTheSpineAgainWithTheObjectionInThePrompt()
    {
        var (outcome, provider) = await Write(Plain, SpineObjected, PlainerSpine, Plain, Plain);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var again = provider.Requests[3].Prompt.History[0].Text;

        Assert.Equal("story_line_repairs", provider.Requests[3].ResponseSchema?.Name);
        Assert.Contains("Write again the chapter's name and spine", again);
        Assert.Contains("a ledger stands for the Commander's standing.", again);
        Assert.Equal("The Lost Cargo", outcome.Draft!.Name);
    }

    [Fact]
    public async Task ASpineStillObjectedToAfterTwoRoundsFailsAsNotWrittenPlainly()
    {
        const string again = """{"objections": [{"part": "premise", "quote": "Freight went missing", "kind": "figurative", "reason": "a ledger stands for the Commander's standing."}]}""";
        var (outcome, _) = await Write(Plain, SpineObjected, PlainerSpine, Plain, again, PlainerSpine, Plain, again);

        Assert.False(outcome.Succeeded);
        Assert.StartsWith("The story is not written plainly: ", outcome.Refusal);
    }

    [Fact]
    public async Task PlainRoundsDoNotUseUpTheFidelityRounds()
    {
        const string fidelity = """{"objections": [{"objective": 2, "quote": "another page", "kind": "history", "reason": "it says the Commander has read the ledger before."}]}""";
        const string fidelityAgain = """{"objections": [{"objective": 2, "quote": "legible now", "kind": "history", "reason": "it says the Commander has read the ledger before."}]}""";
        var (outcome, provider) = await Write(
            fidelity, PlannedChapterWorld.Repair(2),
            fidelityAgain, PlannedChapterWorld.Repair(2),
            Plain, AgainObjected, PlannedChapterWorld.Repair(2),
            Plain, Plain);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(3, Schemas(provider).Count(schema => schema == "story_line_repairs"));
    }
}
