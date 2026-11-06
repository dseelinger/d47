using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>Fidelity objections are repaired in a round counted apart from the place rounds, and a sightseeing hand-in confirms the visit.</summary>
public sealed class AFidelityObjectionGetsARepairRoundOfItsOwnTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private const string SightsConfirmed = "a sightseeing mission was handed in, so its passengers were taken to the sights it listed — not what they made of them";

    private static string Objected(string quote, string reason, string kind = "history") =>
        $$"""{"objections": [{"objective": 2, "quote": "{{quote}}", "kind": "{{kind}}", "reason": "{{reason}}"}]}""";

    private static readonly string FirstObjected = Objected("another page", "it says the Commander has read the ledger before.");

    private static readonly string RepairedObjected = Objected("legible now", "it says the Commander moored.", "action");

    private static async Task<(AdventureOutcome Outcome, RoundScriptedLlmProvider Provider)> Write(params string[] after)
    {
        var ask = PlannedChapterWorld.Ask();
        var plan = await PlannedChapterWorld.PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider([RoundScriptedLlmProvider.Saying(PlannedChapterWorld.Lines(plan.Objectives.Count)), .. after.Select(reply => RoundScriptedLlmProvider.Saying(reply))]);
        var outcome = await PlannedChapterWorld.Generator(provider).GenerateAsync(ask, Now, TestContext.Current.CancellationToken);

        return (outcome, provider);
    }

    [Fact]
    public async Task AnObjectionToTheRepairGetsASecondRepairRoundAndIsWrittenWhenItPasses()
    {
        var (outcome, provider) = await Write(FirstObjected, PlannedChapterWorld.Repair(2), RepairedObjected, PlannedChapterWorld.Repair(2), PlannedChapterWorld.NoObjections);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(["story_lines", "story_fidelity", "story_line_repairs", "story_fidelity", "story_line_repairs", "story_fidelity", "story_plain"], provider.Requests.Select(request => request.ResponseSchema?.Name));
    }

    [Fact]
    public async Task TwoObjectionsToOneObjectiveBothReachTheRepairPrompt()
    {
        const string two = """
            {"objections": [
              {"objective": 2, "quote": "another page", "kind": "history", "reason": "it says the Commander has read the ledger before."},
              {"objective": 2, "quote": "the two", "kind": "event", "reason": "it says how the hop went."}
            ]}
            """;
        var (outcome, provider) = await Write(two, PlannedChapterWorld.Repair(2), PlannedChapterWorld.NoObjections);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var repair = provider.Requests[2].Prompt.History[0].Text;

        Assert.Contains("it says the Commander has read the ledger before.", repair);
        Assert.Contains("it says how the hop went.", repair);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(repair, @"- .*Objective 2 [(]"));
    }

    [Fact]
    public async Task AnObjectionFromAnEarlierRoundIsListedAgainInTheNextRepairPrompt()
    {
        var (_, provider) = await Write(FirstObjected, PlannedChapterWorld.Repair(2), RepairedObjected, PlannedChapterWorld.Repair(2), PlannedChapterWorld.NoObjections);

        var second = provider.Requests[4].Prompt.History[0].Text;

        Assert.Contains("it says the Commander moored.", second);
        Assert.Contains("Earlier rounds also objected: it says the Commander has read the ledger before.", second);
    }

    [Fact]
    public void ASightseeingMissionConfirmsTheVisitAndOtherFamiliesKeepTheirText()
    {
        Assert.Equal(SightsConfirmed, TriggerConfirms.Of(TriggerKind.Mission, null, false, null, "Mission_Sightseeing_Fed"));
        Assert.Equal(TriggerConfirms.Of(TriggerKind.Mission), TriggerConfirms.Of(TriggerKind.Mission, null, false, null, "Mission_Courier"));
        Assert.Equal(TriggerConfirms.Of(TriggerKind.Mission), TriggerConfirms.Of(TriggerKind.Mission, null, false, null, null));
    }

    [Theory]
    [InlineData("Mission_Sightseeing", true)]
    [InlineData("Mission_Courier", false)]
    public async Task TheFidelityPromptShowsTheTextForTheObjectivesFamily(string family, bool sightseeing)
    {
        var story = PlannedChapterWorld.Story with { Slots = [new DrawnActivity(TriggerKind.Mission, family, 1)] };
        var ask = PlannedChapterWorld.Ask(story);
        var plan = await PlannedChapterWorld.PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider([
            RoundScriptedLlmProvider.Saying(PlannedChapterWorld.Lines(plan.Objectives.Count)),
            RoundScriptedLlmProvider.Saying(PlannedChapterWorld.NoObjections),
        ]);

        var outcome = await PlannedChapterWorld.Generator(provider).GenerateAsync(ask, Now, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var fidelity = provider.Requests.First(request => request.ResponseSchema?.Name == "story_fidelity").Prompt.History[0].Text;

        Assert.Equal(sightseeing, fidelity.Contains($"Confirms: {SightsConfirmed}", StringComparison.Ordinal));
        Assert.Equal(!sightseeing, fidelity.Contains($"Confirms: {TriggerConfirms.Of(TriggerKind.Mission)}", StringComparison.Ordinal));
    }
}
