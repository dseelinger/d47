using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// An objective with no line, in the first draft or in a repair's replacement, is refused in the dry run and written again
/// by the next repair round, rather than failing the chapter after the rounds have ended.
/// </summary>
public sealed class ALinelessObjectiveIsWrittenAgainInRepairTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Five objectives; the second is a Colonia hop, past the 80 light year reach.</summary>
    private const string SecondTooFar = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "Where The Freight Went", "function": "catalyst", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Colonia", "line": "Twenty-two thousand light years."},
          {"title": "The Anchorage", "function": "midpoint", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
          {"title": "Back To The Lantern", "function": "all is lost", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Nothing was ever loaded."},
          {"title": "The Freight Delivered", "function": "finale", "kind": "rank", "reason": "Someone there knows about the burst.", "career": "Trade", "rank": 8, "line": "The freight is delivered."}
        ]}
        """;

    /// <summary>Five objectives that all stand, the second written with no line.</summary>
    private const string SecondLineless = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Anchorage", "function": "catalyst", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage"},
          {"title": "The Collectors", "function": "midpoint", "kind": "bounty", "reason": "Someone there knows about the burst.", "count": 3, "line": "Three names on a board."},
          {"title": "Back To The Lantern", "function": "all is lost", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Nothing was ever loaded."},
          {"title": "The Freight Delivered", "function": "finale", "kind": "rank", "reason": "Someone there knows about the burst.", "career": "Trade", "rank": 8, "line": "The freight is delivered."}
        ]}
        """;

    /// <summary>Four objectives; the second's further step is a Colonia hop, past the reach. Its numbered place in the repair list is 3.</summary>
    private const string StepTooFar = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Anchorage", "function": "catalyst", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name.",
           "then": [{"kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Colonia"}]},
          {"title": "Back To The Lantern", "function": "all is lost", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Nothing was ever loaded."},
          {"title": "The Freight Delivered", "function": "finale", "kind": "rank", "reason": "Someone there knows about the burst.", "career": "Trade", "rank": 8, "line": "The freight is delivered."}
        ]}
        """;

    private const string CollectorsWithNoLine = """
        {"replacements": [
          {"objective": 2, "title": "The Collectors", "function": "catalyst", "kind": "bounty", "reason": "Someone there knows about the burst.", "count": 3}
        ]}
        """;

    private const string CollectorsWithALine = """
        {"replacements": [
          {"objective": 2, "title": "The Collectors", "function": "catalyst", "kind": "bounty", "reason": "Someone there knows about the burst.", "count": 3, "line": "Three names on a board."}
        ]}
        """;

    private const string AnchorageWithALine = """
        {"replacements": [
          {"objective": 2, "title": "The Anchorage", "function": "catalyst", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."}
        ]}
        """;

    private const string StepAsBounties = """
        {"replacements": [
          {"objective": 3, "title": "Anything", "kind": "bounty", "reason": "Someone there knows about the burst.", "count": 2}
        ]}
        """;

    [Fact]
    public async Task AReplacementWithNoLineIsRefusedAndTheNextReplacementsLinesAreWritten()
    {
        var provider = Scripted(SecondTooFar, CollectorsWithNoLine, CollectorsWithALine);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var repairs = RepairPrompts(provider);
        Assert.Equal(2, repairs.Count);
        Assert.Contains("Objective 2 (The Collectors) has no line.", repairs[1]);
        Assert.Contains("Write a replacement for objective 2, and for no other.", repairs[1]);

        var written = outcome.Draft!.Beats[1];
        Assert.Equal("The Collectors", written.Title);
        Assert.Equal(["Three names on a board."], [.. written.Lines.Select(line => line.Text)]);
    }

    [Fact]
    public async Task AFirstDraftObjectiveWithNoLineIsAskedForAgainInTheFirstRepairRound()
    {
        var provider = Scripted(SecondLineless, AnchorageWithALine);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var repair = Assert.Single(RepairPrompts(provider));
        Assert.Contains("Objective 2 (The Anchorage) has no line.", repair);
        Assert.Contains("Write a replacement for objective 2, and for no other.", repair);
        Assert.Equal(["To one name."], [.. outcome.Draft!.Beats[1].Lines.Select(line => line.Text)]);
    }

    [Fact]
    public async Task AReplacementWithLinesReplacesTheRefusedObjectivesLines()
    {
        var outcome = await Write(Scripted(SecondTooFar, CollectorsWithALine));

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(["Three names on a board."], [.. outcome.Draft!.Beats[1].Lines.Select(line => line.Text)]);
    }

    [Fact]
    public async Task AReplacementForAFurtherStepKeepsNoLineAndIsNotRefusedForIt()
    {
        var provider = Scripted(StepTooFar, StepAsBounties);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Single(RepairPrompts(provider));

        var anchorage = outcome.Draft!.Beats[1];
        Assert.Equal(["To one name."], [.. anchorage.Lines.Select(line => line.Text)]);
        Assert.Equal(TriggerKind.Bounty, anchorage.Steps[^1].Trigger.Kind);
    }

    [Fact]
    public async Task ADetourOfOneObjectiveIsNotRefusedForHavingNoLine()
    {
        const string lineless = """
            {"opening": "x", "reply": "Here it is.", "beats": [
              {"title": "The Collectors", "function": "finale", "kind": "bounty", "reason": "Someone there knows about the burst.", "count": 3}
            ]}
            """;

        var detour = AdventureFixtures.LanternRoute() with { Detour = true, Beats = [AdventureFixtures.LanternRoute().Beats[0]] };
        var provider = new RoundScriptedLlmProvider(Saying(lineless));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), new RecordingLogger())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short, Rewrite: new AdventureRewrite(detour, 0)), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Empty(RepairPrompts(provider));
        Assert.Equal("The Collectors", Assert.Single(outcome.Draft!.Beats).Title);
    }

    private static List<string> RepairPrompts(RoundScriptedLlmProvider provider) =>
        [.. provider.Requests.Where(request => request.ResponseSchema?.Name == "story_replacements").Select(request => request.Prompt.History[0].Text)];

    private static RoundScriptedLlmProvider Scripted(string beats, params string[] repairs) =>
        new([
            Saying(AdventureGeneratorTests.Spine),
            Saying(beats),
            .. repairs.Select(repair => Saying(repair)),
        ]);

    private static Task<AdventureOutcome> Write(RoundScriptedLlmProvider provider) =>
        AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), new RecordingLogger())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Evening), Now, CancellationToken.None);
}
