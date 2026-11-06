using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// The rewrites the last plain check gives are put in place once the writer's rounds are used up, unchecked; an objection with no
/// rewrite, or a rewrite that repeats the quote, still refuses the story. Between writer rounds a part takes one rewrite of the check's.
/// </summary>
public sealed class TheLastPlainCheckRewriteIsPutInPlaceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private const string Plain = """{"objections": []}""";

    private const string FigurativeSpine = """
        {"name": "The Unrecoverable Column", "premise": "A ledger will not balance.", "want": "To find the missing freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "The freight was never loaded.", "ending": "The column balances."}
        """;

    private const string PremiseObjected = """{"objections": [{"part": "premise", "quote": "A ledger will not balance", "kind": "figurative", "reason": "a ledger stands for the missing freight.", "plain": "Freight went missing from Ossen's Lantern"}]}""";

    private const string StakeObjected = """{"objections": [{"part": "stake", "quote": "a debt can be owed to nobody", "kind": "figurative", "reason": "it names no one owed.", "plain": "the freight's owner can be found"}]}""";

    private const string EndingObjected = """{"objections": [{"part": "ending", "quote": "The column balances", "kind": "figurative", "reason": "a column stands for the freight.", "plain": "The freight is accounted for"}]}""";

    private const string EndingObjectedWithNoRewrite = """{"objections": [{"part": "ending", "quote": "The column balances", "kind": "figurative", "reason": "a column stands for the freight.", "plain": ""}]}""";

    private const string LineObjected = """{"objections": [{"part": "objective 2", "quote": "To one name", "kind": "figurative", "reason": "it calls the Commander's goal a name.", "plain": "To the trader at Maren Anchorage"}]}""";

    private const string TraderObjected = """{"objections": [{"part": "objective 2", "quote": "the trader", "kind": "figurative", "reason": "it names no trade.", "plain": "the commodities trader"}]}""";

    private const string CommoditiesObjected = """{"objections": [{"part": "objective 2", "quote": "commodities trader", "kind": "figurative", "reason": "a trader is not a station role.", "plain": "commodities broker"}]}""";

    private const string CommoditiesObjectedWithNoRewrite = """{"objections": [{"part": "objective 2", "quote": "commodities trader", "kind": "figurative", "reason": "a trader is not a station role."}]}""";

    private const string PremiseWritten = """
        {"name": "The Unrecoverable Column", "premise": "Freight went missing from Ossen's Lantern.", "want": "To find the missing freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "The freight was never loaded.", "ending": "The column balances."}
        """;

    private const string StakeWritten = """
        {"name": "The Unrecoverable Column", "premise": "Freight went missing from Ossen's Lantern.", "want": "To find the missing freight.",
         "stake": "Whether the freight's owner can be found.", "turn": "The freight was never loaded.", "ending": "The column balances."}
        """;

    private static string Dock(string line) => $$"""
        {"replacements": [
          {"objective": 2, "title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "{{line}}"}
        ]}
        """;

    private static RoundScriptedLlmProvider Scripted(params string[] replies) =>
        new([.. replies.Select(reply => RoundScriptedLlmProvider.Saying(reply))]) { ScriptsPlainChecks = true };

    private static Task<AdventureOutcome> Write(RoundScriptedLlmProvider provider) =>
        AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

    [Fact]
    public async Task ASpineWithAThirdFigurativePhraseTakesTheLastRewrite()
    {
        var provider = Scripted(
            FigurativeSpine, PremiseObjected, StakeObjected, PremiseWritten, StakeObjected, EndingObjected, StakeWritten, EndingObjected,
            AdventureGeneratorTests.GoodBeats, Plain, Plain);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(11, provider.CallCount);
        Assert.Equal("Freight went missing from Ossen's Lantern.", outcome.Draft!.Spine!.Premise);
        Assert.Equal("Whether the freight's owner can be found.", outcome.Draft.Spine.Stake);
        Assert.Equal("The freight is accounted for.", outcome.Draft.Spine.Ending);
    }

    [Fact]
    public async Task ASpineWhoseLastObjectionHasNoRewriteIsStillRefused()
    {
        var provider = Scripted(FigurativeSpine, PremiseObjected, StakeObjected, PremiseWritten, StakeObjected, EndingObjected, StakeWritten, EndingObjectedWithNoRewrite);

        var outcome = await Write(provider);

        Assert.False(outcome.Succeeded);
        Assert.Equal(8, provider.CallCount);
        Assert.StartsWith("The story is not written plainly: ", outcome.Refusal);
    }

    [Fact]
    public async Task AnObjectiveWithAThirdFigurativePhraseTakesTheLastRewrite()
    {
        var provider = Scripted(
            AdventureGeneratorTests.Spine, Plain, AdventureGeneratorTests.GoodBeats, Plain, LineObjected, Plain, TraderObjected,
            Dock("To the trader at Maren Anchorage."), Plain, TraderObjected, Plain, CommoditiesObjected,
            Dock("To the commodities trader at Maren Anchorage."), Plain, CommoditiesObjected);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(15, provider.CallCount);
        Assert.Equal("To the commodities broker at Maren Anchorage.", outcome.Draft!.Beats[1].Lines[0].Text);
    }

    [Fact]
    public async Task AnObjectiveWhoseLastObjectionHasNoRewriteIsStillRefused()
    {
        var provider = Scripted(
            AdventureGeneratorTests.Spine, Plain, AdventureGeneratorTests.GoodBeats, Plain, LineObjected, Plain, TraderObjected,
            Dock("To the trader at Maren Anchorage."), Plain, TraderObjected, Plain, CommoditiesObjected,
            Dock("To the commodities trader at Maren Anchorage."), Plain, CommoditiesObjectedWithNoRewrite);

        var outcome = await Write(provider);

        Assert.False(outcome.Succeeded);
        Assert.StartsWith("The story is not written plainly: ", outcome.Refusal);
    }
}
