using D47.Core.Adventures;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>An objective the model writes with "then" steps becomes one beat of several steps, each checked as its own objective.</summary>
public sealed class AGeneratedBeatCanHaveSeveralStepsTests
{
    private const string Steps = """
        {"opening": "Again.", "reply": "Here.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Back.",
           "then": [{"kind": "bounty", "count": 3}, {"kind": "mine", "count": 5}, {"kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage"}]},
          {"title": "The Ore", "function": "finale", "kind": "mine", "count": 20, "line": "Refined.", "then": []}
        ]}
        """;

    private const string AFurtherStepWithNoReason = """
        {"opening": "Again.", "reply": "Here.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Back.",
           "then": [{"kind": "dock", "system": "Dyson's Hollow", "station": "Maren Anchorage"}]},
          {"title": "The Ore", "function": "finale", "kind": "mine", "count": 20, "line": "Refined."}
        ]}
        """;

    [Fact]
    public async Task ThenStepsAreGroupedIntoTheBeatTheyFollow()
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(Steps));

        var outcome = await Generate(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var beats = outcome.Draft!.Beats;
        Assert.Equal(2, beats.Count);
        Assert.Equal(
            [TriggerKind.Arrive, TriggerKind.Bounty, TriggerKind.Mine, TriggerKind.Dock],
            beats[0].Steps.Select(step => step.Trigger.Kind));
        Assert.Equal("Back.", beats[0].Line);
        Assert.Single(beats[1].Steps);
        Assert.Contains("\"then\"", provider.Requests[1].Prompt.History[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFurtherTravelStepNeedsItsOwnReason()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(AFurtherStepWithNoReason), RoundScriptedLlmProvider.Saying(Repairs.Replacing(AFurtherStepWithNoReason, 1)), RoundScriptedLlmProvider.Saying(Repairs.Replacing(AFurtherStepWithNoReason, 1)));

        var outcome = await Generate(provider);

        Assert.False(outcome.Succeeded);
        Assert.Contains("Objective 2 (The Lantern, a further step) is a travel objective with no \"reason\"", outcome.Refusal);
    }

    private static Task<AdventureOutcome> Generate(RoundScriptedLlmProvider provider) =>
        AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy()).GenerateAsync(
            new AdventureAsk(AdventureReach.Session, Story: new AdventureStory(Id, Card.Title, Card.Describe(), Secret.Secret, 2, 10, 5)),
            Now,
            CancellationToken.None);
}
