using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
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

    private const string LineRewritten = """{"objections": [{"part": "objective 2", "quote": "To one name", "kind": "figurative", "reason": "it calls the Commander's goal a name.", "plain": "To the trader at Maren Anchorage"}]}""";

    private const string LineInvented = """{"objections": [{"part": "objective 2", "quote": "To one name", "kind": "invented", "reason": "docks have no name register.", "plain": "To the trader at Maren Anchorage"}]}""";

    private const string RewriteObjected = """{"objections": [{"objective": 2, "quote": "the trader at Maren Anchorage", "kind": "history", "reason": "it says the Commander has met the trader before."}]}""";

    private const string PlainDock = """
        {"replacements": [
          {"objective": 2, "title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Dock at Maren Anchorage."}
        ]}
        """;

    private const string FigurativeSpine = """
        {"name": "The Unrecoverable Column", "premise": "A ledger will not balance.", "want": "To find the missing freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "The freight was never loaded.", "ending": "The column balances."}
        """;

    private const string PremiseRewritten = """{"objections": [{"part": "premise", "quote": "A ledger will not balance", "kind": "figurative", "reason": "a ledger stands for the missing freight.", "plain": "Freight went missing from Ossen's Lantern"}]}""";

    private static RoundScriptedLlmProvider Scripted(params string[] replies) =>
        new([.. replies.Select(reply => RoundScriptedLlmProvider.Saying(reply))]) { ScriptsPlainChecks = true };

    private static Task<AdventureOutcome> Write(RoundScriptedLlmProvider provider) =>
        AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

    private static bool AskedTheWriterForReplacements(RoundScriptedLlmProvider provider) =>
        provider.Requests.Any(request => request.Prompt.History[0].Text.Contains("Some of it cannot stand", StringComparison.Ordinal));

    [Fact]
    public async Task AFigurativeLineIsRewrittenInPlaceAndCheckedAgainWithNoWriterRound()
    {
        var provider = Scripted(AdventureGeneratorTests.Spine, Plain, AdventureGeneratorTests.GoodBeats, Plain, LineRewritten, Plain, Plain);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(7, provider.CallCount);
        Assert.False(AskedTheWriterForReplacements(provider));
        Assert.Equal("To the trader at Maren Anchorage.", outcome.Draft!.Beats[1].Lines[0].Text);
    }

    [Fact]
    public async Task AnInventedFeatureGoesBackToTheWriterEvenWithARewrite()
    {
        var provider = Scripted(AdventureGeneratorTests.Spine, Plain, AdventureGeneratorTests.GoodBeats, Plain, LineInvented, PlainDock, Plain, Plain);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.True(AskedTheWriterForReplacements(provider));
        Assert.Equal("Dock at Maren Anchorage.", outcome.Draft!.Beats[1].Lines[0].Text);
    }

    [Fact]
    public async Task ARewriteTheFidelityCheckRefusesIsRepairedByTheWriter()
    {
        var provider = Scripted(AdventureGeneratorTests.Spine, Plain, AdventureGeneratorTests.GoodBeats, Plain, LineRewritten, RewriteObjected, PlainDock, Plain, Plain);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.True(AskedTheWriterForReplacements(provider));
        Assert.Contains("it says the Commander has met the trader before.", provider.Requests[6].Prompt.History[0].Text);
        Assert.Equal("Dock at Maren Anchorage.", outcome.Draft!.Beats[1].Lines[0].Text);
    }

    [Fact]
    public async Task ARewrittenLineThatIsStillNotPlainFailsAfterItsRounds()
    {
        const string StillNot = """{"objections": [{"part": "objective 2", "quote": "To the trader at Maren Anchorage", "kind": "figurative", "reason": "still an image.", "plain": "To the trader at Maren Anchorage"}]}""";

        var provider = Scripted(AdventureGeneratorTests.Spine, Plain, AdventureGeneratorTests.GoodBeats, Plain, LineRewritten, Plain, StillNot, Plain, StillNot);

        var outcome = await Write(provider);

        Assert.False(outcome.Succeeded);
        Assert.Equal(9, provider.CallCount);
        Assert.StartsWith("The story is not written plainly: ", outcome.Refusal);
    }

    [Fact]
    public async Task AFigurativePremiseIsRewrittenInPlaceWithNoSecondSpineCall()
    {
        var provider = Scripted(FigurativeSpine, PremiseRewritten, Plain, AdventureGeneratorTests.GoodBeats, Plain, Plain);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(6, provider.CallCount);
        Assert.Equal("Freight went missing from Ossen's Lantern.", outcome.Draft!.Spine!.Premise);
        Assert.Contains("[premise] Freight went missing from Ossen's Lantern.", provider.Requests[2].Prompt.History[0].Text);
    }

    [Fact]
    public async Task ALineThePlainCheckRefusesAgainAfterItsOwnRewriteGoesToTheWriterWithEveryFinding()
    {
        const string RewriteRefused = """{"objections": [{"part": "objective 2", "quote": "the trader at Maren Anchorage", "kind": "figurative", "reason": "it names no trader.", "plain": "the broker"}]}""";

        var provider = Scripted(AdventureGeneratorTests.Spine, Plain, AdventureGeneratorTests.GoodBeats, Plain, LineRewritten, Plain, RewriteRefused, PlainDock, Plain, Plain);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var repair = Assert.Single(provider.Requests, request => request.Prompt.History[0].Text.Contains("Some of it cannot stand", StringComparison.Ordinal)).Prompt.History[0].Text;
        Assert.Contains("\"the trader at Maren Anchorage\" is figurative", repair);
        Assert.Contains("\"To one name\" is figurative", repair);
        Assert.Equal("Dock at Maren Anchorage.", outcome.Draft!.Beats[1].Lines[0].Text);
    }

    [Fact]
    public async Task APremiseThePlainCheckRefusesAgainAfterItsOwnRewriteGoesToTheWriterWithEveryFinding()
    {
        const string RewriteRefused = """{"objections": [{"part": "premise", "quote": "Freight went missing", "kind": "figurative", "reason": "it says not whose freight.", "plain": "Cargo was lost"}]}""";

        var provider = Scripted(FigurativeSpine, PremiseRewritten, RewriteRefused, AdventureGeneratorTests.Spine, Plain, AdventureGeneratorTests.GoodBeats, Plain, Plain);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var again = Assert.Single(provider.Requests, request => request.Prompt.History[0].Text.Contains("Its wording is refused", StringComparison.Ordinal)).Prompt.History[0].Text;
        Assert.Contains("\"Freight went missing\" is figurative", again);
        Assert.Contains("\"A ledger will not balance\" is figurative", again);
    }
}
