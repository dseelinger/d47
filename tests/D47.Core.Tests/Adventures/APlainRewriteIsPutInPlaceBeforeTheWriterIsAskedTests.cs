using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// A figurative phrase the checker rewrites is put in place and the draft checked again with no writer round; an invented feature,
/// or a rewrite that fails a later check, goes back to the writer, as does a part the plain check refuses again after its own
/// rewrite, with every finding raised on it.
/// </summary>
public sealed class APlainRewriteIsPutInPlaceBeforeTheWriterIsAskedTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private const string Plain = """{"objections": []}""";

    private const string LineRewritten = """{"objections": [{"part": "objective 2", "quote": "another page", "kind": "figurative", "reason": "it calls the Commander's goal a page.", "plain": "a further record"}]}""";

    private const string LineInvented = """{"objections": [{"part": "objective 2", "quote": "another page", "kind": "invented", "reason": "ledgers have no pages to turn.", "plain": "a further record"}]}""";

    private const string RewriteObjected = """{"objections": [{"objective": 2, "quote": "a further record", "kind": "history", "reason": "it says the Commander has read the record before."}]}""";

    private const string PremiseRewritten = """{"objections": [{"part": "premise", "quote": "ledger turns up", "kind": "figurative", "reason": "a ledger stands for the missing freight.", "plain": "ledger is found"}]}""";

    private const string PlainerSpine = """
        {"objectives": [], "name": "The Lost Cargo", "premise": "Freight went missing from Ossen's Lantern.", "want": "To find the missing freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "The freight was never loaded.", "ending": "The freight is accounted for."}
        """;

    private static async Task<(AdventureOutcome Outcome, RoundScriptedLlmProvider Provider)> Write(params string[] replies)
    {
        var ask = PlannedChapterWorld.Ask();
        var plan = await PlannedChapterWorld.PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider([RoundScriptedLlmProvider.Saying(PlannedChapterWorld.Lines(plan.Objectives.Count)), .. replies.Select(reply => RoundScriptedLlmProvider.Saying(reply))])
        {
            ScriptsPlainChecks = true,
        };
        var outcome = await PlannedChapterWorld.Generator(provider).GenerateAsync(ask, Now, TestContext.Current.CancellationToken);

        return (outcome, provider);
    }

    private static IEnumerable<string?> Schemas(RoundScriptedLlmProvider provider) => provider.Requests.Select(request => request.ResponseSchema?.Name);

    [Fact]
    public async Task AFigurativeLineIsRewrittenInPlaceAndCheckedAgainWithNoWriterRound()
    {
        var (outcome, provider) = await Write(Plain, LineRewritten, Plain, Plain);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(["story_lines", "story_fidelity", "story_plain", "story_fidelity", "story_plain"], Schemas(provider));
        Assert.Equal("The ledger has a further record, the two.", outcome.Draft!.Beats[1].Lines[0].Text);
    }

    [Fact]
    public async Task AnInventedFeatureGoesBackToTheWriterEvenWithARewrite()
    {
        var (outcome, provider) = await Write(Plain, LineInvented, PlannedChapterWorld.Repair(2), Plain, Plain);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains("story_line_repairs", Schemas(provider));
        Assert.Equal("The ledger's two page is legible now.", outcome.Draft!.Beats[1].Lines[0].Text);
    }

    [Fact]
    public async Task ARewriteTheFidelityCheckRefusesIsRepairedByTheWriter()
    {
        var (outcome, provider) = await Write(Plain, LineRewritten, RewriteObjected, PlannedChapterWorld.Repair(2), Plain, Plain);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("story_line_repairs", provider.Requests[4].ResponseSchema?.Name);
        Assert.Contains("it says the Commander has read the record before.", provider.Requests[4].Prompt.History[0].Text);
        Assert.Equal("The ledger's two page is legible now.", outcome.Draft!.Beats[1].Lines[0].Text);
    }

    [Fact]
    public async Task ARewrittenLineThatIsStillNotPlainFailsAfterItsRounds()
    {
        const string stillNot = """{"objections": [{"part": "objective 2", "quote": "a further record", "kind": "figurative", "reason": "still an image.", "plain": "a further record"}]}""";
        const string stillNotRepaired = """{"objections": [{"part": "objective 2", "quote": "legible now", "kind": "figurative", "reason": "still an image."}]}""";

        var (outcome, _) = await Write(
            Plain, LineRewritten,
            Plain, stillNot,
            PlannedChapterWorld.Repair(2), Plain, stillNotRepaired,
            PlannedChapterWorld.Repair(2), Plain, stillNotRepaired);

        Assert.False(outcome.Succeeded);
        Assert.StartsWith("The story is not written plainly: ", outcome.Refusal);
    }

    [Fact]
    public async Task AFigurativePremiseIsRewrittenInPlaceWithNoWriterRound()
    {
        var (outcome, provider) = await Write(Plain, PremiseRewritten, Plain, Plain);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.DoesNotContain("story_line_repairs", Schemas(provider));
        Assert.Equal("A salvage broker's ledger is found in the Commander's hold.", outcome.Draft!.Spine!.Premise);
        Assert.Contains("[premise] A salvage broker's ledger is found in the Commander's hold.", provider.Requests[4].Prompt.History[0].Text);
    }

    [Fact]
    public async Task ALineThePlainCheckRefusesAgainAfterItsOwnRewriteGoesToTheWriterWithEveryFinding()
    {
        const string rewriteRefused = """{"objections": [{"part": "objective 2", "quote": "a further record", "kind": "figurative", "reason": "it names no record.", "plain": "the broker"}]}""";

        var (outcome, provider) = await Write(Plain, LineRewritten, Plain, rewriteRefused, PlannedChapterWorld.Repair(2), Plain, Plain);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var repair = Assert.Single(provider.Requests, request => request.ResponseSchema?.Name == "story_line_repairs").Prompt.History[0].Text;
        Assert.Contains("\"a further record\" is figurative", repair);
        Assert.Contains("\"another page\" is figurative", repair);
        Assert.Equal("The ledger's two page is legible now.", outcome.Draft!.Beats[1].Lines[0].Text);
    }

    [Fact]
    public async Task APremiseThePlainCheckRefusesAgainAfterItsOwnRewriteGoesToTheWriterWithEveryFinding()
    {
        const string rewriteRefused = """{"objections": [{"part": "premise", "quote": "ledger is found", "kind": "figurative", "reason": "it says not whose ledger.", "plain": "ledger is open"}]}""";

        var (outcome, provider) = await Write(Plain, PremiseRewritten, Plain, rewriteRefused, PlainerSpine, Plain, Plain);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var again = Assert.Single(provider.Requests, request => request.ResponseSchema?.Name == "story_line_repairs").Prompt.History[0].Text;
        Assert.Contains("\"ledger is found\" is figurative", again);
        Assert.Contains("\"ledger turns up\" is figurative", again);
    }
}
