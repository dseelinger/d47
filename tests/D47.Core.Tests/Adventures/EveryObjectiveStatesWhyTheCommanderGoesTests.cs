using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>Every objective except a choice carries a reason, and the core never presents the next objective as an order.</summary>
public sealed class EveryObjectiveStatesWhyTheCommanderGoesTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private const string Reason = "The third sheet's receipt was stamped in Dyson's Hollow, where the freight was last logged.";

    [Fact]
    public async Task AnObjectivesReasonSurvivesIntoTheDraftAndItsHandOff()
    {
        var ask = Stories.PlannedChapterWorld.Ask();
        var plan = await Stories.PlannedChapterWorld.PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Stories.PlannedChapterWorld.Lines(plan.Objectives.Count, (number, objective) => objective["reason"] = Reason)),
            RoundScriptedLlmProvider.Saying(Stories.PlannedChapterWorld.NoObjections));

        var outcome = await Stories.PlannedChapterWorld.Generator(provider).GenerateAsync(ask, Now, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var objective = outcome.Draft!.Beats[0];
        Assert.Equal(Reason, objective.Reason);
        Assert.StartsWith(Reason, objective.HandOff());
    }

    [Fact]
    public void TheRewordingBriefNeverPresentsAnOrderAndLeavesTheHandOffToTheNarrator()
    {
        var brief = FlavourBriefs.For(
            new Announcement(AdventureCallout.KeyPrefix + "the-lantern-route", "The log is closed. Next: dock at Maren Anchorage. A reader there knows the burst."),
            personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.DoesNotContain("where the Commander goes next", brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("never present the next objective as an order you were given", brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("\"I'm told\"", brief.Instruction, StringComparison.Ordinal);
    }
}
