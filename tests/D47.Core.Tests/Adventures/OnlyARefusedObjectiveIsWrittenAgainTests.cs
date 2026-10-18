using System.Text.Json;
using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// After the dry run, only the refused objectives go back to the model, by number; the objectives that stood are kept as
/// written, each replacement is checked in place, and a draft gets <see cref="AdventureGenerator.RepairRounds"/> rounds.
/// </summary>
public sealed class OnlyARefusedObjectiveIsWrittenAgainTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Five objectives; the third is a Colonia hop on a "near here" ask, past the 80 light year reach.</summary>
    private const string ThirdTooFar = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Anchorage", "function": "catalyst", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
          {"title": "Where The Freight Went", "function": "midpoint", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Colonia", "line": "Twenty-two thousand light years."},
          {"title": "Back To The Lantern", "function": "all is lost", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Nothing was ever loaded."},
          {"title": "The Column Will Not Balance", "function": "finale", "kind": "rank", "career": "Trade", "rank": 8, "line": "It balances."}
        ]}
        """;

    private const string Bounties = """
        {"replacements": [
          {"objective": 3, "title": "The Collectors", "function": "midpoint", "kind": "bounty", "count": 3, "line": "Three names on a board."}
        ]}
        """;

    private const string ColoniaAgain = """
        {"replacements": [
          {"objective": 3, "title": "The Far Column", "function": "midpoint", "kind": "arrive", "reason": "The freight was signed for there.", "system": "Colonia", "line": "A long way."}
        ]}
        """;

    /// <summary>A replacement landing in Ossen's Lantern, so the kept objective 4, which arrives there, arrives where the Commander already is.</summary>
    private const string LandingInTheLantern = """
        {"replacements": [
          {"objective": 3, "title": "The Consignee", "function": "midpoint", "kind": "land", "reason": "The consignee's pad is there.", "system": "Ossen's Lantern", "body": "Ossen's Lantern 2 a", "line": "Dust."}
        ]}
        """;

    [Fact]
    public async Task TheObjectivesThatStoodComeBackAsTheyWereAroundANewThird()
    {
        var provider = Scripted(ThirdTooFar, Bounties);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(3, provider.CallCount);

        // The same objectives written in one answer, with nothing to repair, are what the kept ones must equal.
        var control = await Write(Scripted(Spliced(ThirdTooFar, Bounties)));
        Assert.True(control.Succeeded, control.Refusal);

        var repaired = outcome.Draft!.Beats;
        Assert.Equal(5, repaired.Count);
        Assert.Equal("The Collectors", repaired[2].Title);
        Assert.Equal(TriggerKind.Bounty, repaired[2].Trigger.Kind);

        foreach (var kept in new[] { 0, 1, 3, 4 })
        {
            Assert.Equal(JsonSerializer.Serialize(control.Draft!.Beats[kept]), JsonSerializer.Serialize(repaired[kept]));
        }

        Assert.Equal(["The Lantern", "The Anchorage", "Back To The Lantern", "The Column Will Not Balance"], [.. new[] { 0, 1, 3, 4 }.Select(kept => repaired[kept].Title)]);
    }

    [Fact]
    public async Task TheRepairCallNamesTheRefusedObjectiveAndAsksOnlyForReplacements()
    {
        var provider = Scripted(ThirdTooFar, Bounties);

        await Write(provider);

        var repair = provider.Requests[2];
        var prompt = repair.Prompt.History[0].Text;

        Assert.Contains("3. Where The Freight Went (midpoint) — arrive: Colonia", prompt);
        Assert.Contains("4. Back To The Lantern (all is lost) — arrive: Ossen's Lantern", prompt);
        Assert.Contains("Objective 3 (Where The Freight Went) is 21886 light years from the previous stop; the reach is 80.", prompt);
        Assert.Contains("Write a replacement for objective 3, and for no other.", prompt);

        var schema = Assert.IsType<D47.Core.Conversation.LlmResponseSchema>(repair.ResponseSchema);
        var root = JsonDocument.Parse(schema.SchemaJson).RootElement;

        Assert.Equal("story_replacements", schema.Name);
        Assert.Equal(["replacements"], root.GetProperty("required").EnumerateArray().Select(name => name.GetString()));
        Assert.Equal(
            "integer",
            root.GetProperty("properties").GetProperty("replacements").GetProperty("items").GetProperty("properties").GetProperty("objective").GetProperty("type").GetString());
    }

    [Fact]
    public async Task AReplacementOutOfReachOfTheKeptObjectiveBeforeItIsRefusedInTheSecondRound()
    {
        var provider = Scripted(ThirdTooFar, ColoniaAgain, Bounties);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(4, provider.CallCount);
        Assert.Contains(
            "Objective 3 (The Far Column) is 21886 light years from the previous stop; the reach is 80.",
            provider.Requests[3].Prompt.History[0].Text);
        Assert.Equal("The Collectors", outcome.Draft!.Beats[2].Title);
    }

    [Fact]
    public async Task AReplacementThatLeavesTheKeptObjectiveAfterItUnableToStandIsTheOneRefused()
    {
        var provider = Scripted(ThirdTooFar, LandingInTheLantern, Bounties);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var second = provider.Requests[3].Prompt.History[0].Text;
        Assert.Contains(
            "The replacement objective 3 leaves objective 4, which is kept, unable to stand: Objective 4 (Back To The Lantern) arrives at Ossen's Lantern, but the Commander is already there",
            second);
        Assert.Contains("Write a replacement for objective 3, and for no other.", second);
        Assert.Equal("Back To The Lantern", outcome.Draft!.Beats[3].Title);
    }

    [Fact]
    public async Task AReplacementForAnObjectiveThatStoodIsIgnored()
    {
        const string alsoTheFirst = """
            {"replacements": [
              {"objective": 1, "title": "Somewhere Else", "function": "setup", "kind": "bounty", "count": 1, "line": "Not asked for."},
              {"objective": 3, "title": "The Collectors", "function": "midpoint", "kind": "bounty", "count": 3, "line": "Three names on a board."}
            ]}
            """;

        var outcome = await Write(Scripted(ThirdTooFar, alsoTheFirst));

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("The Lantern", outcome.Draft!.Beats[0].Title);
        Assert.Equal("The Collectors", outcome.Draft.Beats[2].Title);
    }

    [Fact]
    public async Task TwoFailedRoundsFailTheDraftAndEveryRefusalIsLoggedWithItsRound()
    {
        var provider = Scripted(ThirdTooFar, ColoniaAgain, ColoniaAgain);
        var log = new RecordingLogger();

        var outcome = await Write(provider, log);

        Assert.False(outcome.Succeeded);
        Assert.Equal(2 + AdventureGenerator.RepairRounds, provider.CallCount);
        Assert.Contains("Objective 3 (The Far Column) is 21886 light years from the previous stop", outcome.Refusal);

        var refusals = log.Entries.Where(entry => entry.Level == LogLevel.Information && entry.Message.Contains(" refused: ", StringComparison.Ordinal)).Select(entry => entry.Message).ToList();

        Assert.Equal(3, refusals.Count);
        Assert.StartsWith("Draft 1, first round, beats step refused: Objective 3 (Where The Freight Went)", refusals[0]);
        Assert.StartsWith("Draft 1, repair 1 round, replacements step refused: Objective 3 (The Far Column)", refusals[1]);
        Assert.StartsWith("Draft 1, repair 2 round, replacements step refused: Objective 3 (The Far Column)", refusals[2]);
    }

    private static RoundScriptedLlmProvider Scripted(string beats, params string[] repairs) =>
        new([
            RoundScriptedLlmProvider.Saying(AdventureGeneratorTests.Spine),
            RoundScriptedLlmProvider.Saying(beats),
            .. repairs.Select(repair => RoundScriptedLlmProvider.Saying(repair)),
        ]);

    private static Task<AdventureOutcome> Write(RoundScriptedLlmProvider provider, ILogger? log = null) =>
        AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), log ?? new RecordingLogger())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Evening), Now, CancellationToken.None);

    /// <summary>The beats answer with each replacement put in place of the objective it numbers.</summary>
    private static string Spliced(string beats, string replacements)
    {
        var answer = System.Text.Json.Nodes.JsonNode.Parse(beats)!;
        var list = answer["beats"]!.AsArray();

        foreach (var replacement in System.Text.Json.Nodes.JsonNode.Parse(replacements)!["replacements"]!.AsArray())
        {
            var beat = replacement!.DeepClone().AsObject();
            var at = beat["objective"]!.GetValue<int>() - 1;
            beat.Remove("objective");
            list[at] = beat;
        }

        return answer.ToJsonString();
    }
}
