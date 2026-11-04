using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>A chapter's wording is checked against the plain-words rule, with repair rounds counted apart from the place and fidelity rounds.</summary>
public sealed class AChapterIsCheckedForPlainWordsBeforeTheCommanderSeesItTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private const string Plain = """{"objections": []}""";

    private const string LineObjected = """{"objections": [{"part": "objective 2", "quote": "To one name", "kind": "figurative", "reason": "it calls the Commander's goal a name."}]}""";

    private const string DockObjected = """{"objections": [{"part": "objective 2", "quote": "at a dock", "kind": "figurative", "reason": "it names no station."}]}""";

    private const string NotInTheLine = """{"objections": [{"part": "objective 2", "quote": "a door in the sky", "kind": "figurative", "reason": "it is a metaphor."}]}""";

    private const string SpineObjected = """{"objections": [{"part": "premise", "quote": "A ledger will not balance", "kind": "figurative", "reason": "a ledger stands for the Commander's standing."}]}""";

    private const string FigurativeSpine = """
        {"name": "The Unrecoverable Column", "premise": "A ledger will not balance.", "want": "To find the missing freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "The freight was never loaded.", "ending": "The column balances."}
        """;

    private const string PlainerSpine = """
        {"name": "The Lost Cargo", "premise": "Freight went missing from Ossen's Lantern.", "want": "To find the missing freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "The freight was never loaded.", "ending": "The freight is accounted for."}
        """;

    private const string NothingObjected = """{"objections": []}""";

    private const string SecondObjected = """{"objections": [{"objective": 2, "quote": "one name", "kind": "history", "reason": "it says the Commander has docked there before."}]}""";

    private const string PlainDock = """
        {"replacements": [
          {"objective": 2, "title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Dock at Maren Anchorage."}
        ]}
        """;

    private const string LostHop = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Lost Hop", "function": "turn", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Nowhere Reached", "line": "To one name."},
          {"title": "The Freight Delivered", "function": "resolution", "kind": "rank", "reason": "Someone there knows about the burst.", "career": "Trader", "rank": 8, "line": "The freight is delivered."}
        ]}
        """;

    private const string StillLost = """
        {"replacements": [
          {"objective": 2, "title": "The Lost Hop", "function": "turn", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Nowhere Reached", "line": "To one name."}
        ]}
        """;

    private const string QuietDock = """
        {"replacements": [
          {"objective": 2, "title": "The Quiet Dock", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "One name, at a dock."}
        ]}
        """;

    private static RoundScriptedLlmProvider Scripted(params string[] replies) =>
        new(replies.Select(reply => RoundScriptedLlmProvider.Saying(reply)).ToArray()) { ScriptsPlainChecks = true };

    private static Task<AdventureOutcome> Write(RoundScriptedLlmProvider provider) =>
        AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

    [Fact]
    public async Task AnObjectionQuotingAnObjectivesLineSendsThatObjectiveThroughARepairRound()
    {
        var provider = Scripted(AdventureGeneratorTests.Spine, Plain, AdventureGeneratorTests.GoodBeats, NothingObjected, LineObjected, PlainDock, NothingObjected, Plain);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var repair = provider.Requests[5].Prompt.History[0].Text;

        Assert.Contains("Write a replacement for objective 2, and for no other.", repair);
        Assert.Contains("it calls the Commander's goal a name.", repair);
        Assert.Equal("Dock at Maren Anchorage.", outcome.Draft!.Beats[1].Lines[0].Text);
    }

    [Fact]
    public async Task AnObjectionWhoseQuoteIsNotInThePartItNamesIsDropped()
    {
        var provider = Scripted(AdventureGeneratorTests.Spine, Plain, AdventureGeneratorTests.GoodBeats, NothingObjected, NotInTheLine);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(5, provider.CallCount);
    }

    [Fact]
    public async Task ASpineObjectionAsksForTheSpineAgainWithTheObjectionInThePrompt()
    {
        var provider = Scripted(FigurativeSpine, SpineObjected, PlainerSpine, Plain, AdventureGeneratorTests.GoodBeats, NothingObjected, Plain);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.DoesNotContain("Its wording is refused", provider.Requests[0].Prompt.History[0].Text);

        var again = provider.Requests[2].Prompt.History[0].Text;

        Assert.Contains("Its wording is refused", again);
        Assert.Contains("a ledger stands for the Commander's standing.", again);
        Assert.Equal("The Lost Cargo", outcome.Draft!.Name);
    }

    [Fact]
    public async Task ADraftStillObjectedToAfterTwoRoundsFailsAsNotWrittenPlainly()
    {
        var provider = Scripted(
            AdventureGeneratorTests.Spine, Plain, AdventureGeneratorTests.GoodBeats,
            NothingObjected, LineObjected, PlainDock.Replace("Dock at Maren Anchorage.", "To one name.", StringComparison.Ordinal),
            NothingObjected, LineObjected, PlainDock.Replace("Dock at Maren Anchorage.", "To one name.", StringComparison.Ordinal),
            NothingObjected, LineObjected);

        var outcome = await Write(provider);

        Assert.False(outcome.Succeeded);
        Assert.Equal(11, provider.CallCount);
        Assert.StartsWith("The story is not written plainly: ", outcome.Refusal);
        Assert.Contains("To one name", outcome.Refusal);
    }

    [Fact]
    public async Task ASpineStillObjectedToAfterTwoRoundsFailsAsNotWrittenPlainly()
    {
        var provider = Scripted(FigurativeSpine, SpineObjected, FigurativeSpine, SpineObjected, FigurativeSpine, SpineObjected);

        var outcome = await Write(provider);

        Assert.False(outcome.Succeeded);
        Assert.Equal(6, provider.CallCount);
        Assert.StartsWith("The story is not written plainly: ", outcome.Refusal);
    }

    [Fact]
    public async Task PlainRoundsDoNotUseUpThePlaceOrFidelityRounds()
    {
        var provider = Scripted(
            AdventureGeneratorTests.Spine, Plain, LostHop,
            StillLost, QuietDock,
            SecondObjected, QuietDock,
            NothingObjected, DockObjected, QuietDock,
            NothingObjected, DockObjected, PlainDock,
            NothingObjected, Plain);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(15, provider.CallCount);
        Assert.Equal("Dock at Maren Anchorage.", outcome.Draft!.Beats[1].Lines[0].Text);
    }

    private const string OpeningObjected = """{"objections": [{"part": "opening", "quote": "Somebody is paying", "kind": "figurative", "reason": "it does not say who pays or for what."}]}""";

    private const string PlainOpening = """{"replacements": [], "opening": "A trader at Ossen's Lantern pays for news of the freight."}""";

    [Fact]
    public async Task AnOpeningObjectionRewritesTheOpeningAndKeepsTheObjectives()
    {
        var provider = Scripted(AdventureGeneratorTests.Spine, Plain, AdventureGeneratorTests.GoodBeats, NothingObjected, OpeningObjected, PlainOpening, NothingObjected, Plain);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(8, provider.CallCount);
        Assert.Contains("The opening is refused too", provider.Requests[5].Prompt.History[0].Text);
        Assert.Equal("A trader at Ossen's Lantern pays for news of the freight.", Assert.Single(outcome.Draft!.OpeningLines).Text);
        Assert.Equal(3, outcome.Draft.Beats.Count);
    }
}
