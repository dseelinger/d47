using D47.Core.Adventures;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A choice the model writes becomes a choice step with its options; the line before it must not be the narrator's.</summary>
public sealed class AGeneratedChoiceIsOfferedByTheShipOrACharacterTests
{
    private static string Chapter(string speaker) => $$"""
        {"opening": "Again.", "reply": "Here.", "beats": [
          {"title": "The Parcel", "function": "setup", "kind": "missiontaken", "handle": "parcel", "mission": "Mission_Courier",
           "lines": [{"text": "They are waiting at the jump-in. Fight them, or drop the parcel and run.", "speaker": "{{speaker}}"}]},
          {"title": "The Jump-in", "function": "finale", "kind": "choice", "line": "Done, one way or the other.", "options": [
            {"label": "fight", "then": [{"kind": "missioncomplete", "handle": "parcel"}]},
            {"label": "flee", "then": [{"kind": "missionabandon", "handle": "parcel"}], "ending": "Gone to ground", "line": "Nobody follows."}
          ]}
        ]}
        """;

    [Fact]
    public async Task AChoiceIsReadWithItsOptionsAndTheirSteps()
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(Chapter("ship")));

        var outcome = await Generate(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var choice = Assert.Single(outcome.Draft!.Beats[1].Steps);
        Assert.True(choice.IsChoice);
        Assert.Equal(["fight", "flee"], choice.Options.Select(option => option.Label));
        Assert.Equal([TriggerKind.MissionComplete], choice.Options[0].Steps.Select(step => step.Trigger.Kind));
        Assert.Equal("parcel", choice.Options[1].Steps[0].Trigger.Handle);
        Assert.Equal("Gone to ground", choice.Options[1].Ending);
        Assert.Equal("Nobody follows.", choice.Options[1].EndingLine);
    }

    [Fact]
    public async Task AChoiceTheNarratorWouldOfferIsRefused()
    {
        var narrated = Chapter("narrator");
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(narrated),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(narrated, 2)),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(narrated, 2)));

        var outcome = await Generate(provider);

        Assert.False(outcome.Succeeded);
        Assert.Contains("Objective 2 begins with a choice, and the line before it, which offers the choice, would be read by the narrator.", outcome.Refusal);
    }

    private static Task<AdventureOutcome> Generate(RoundScriptedLlmProvider provider) =>
        AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy()).GenerateAsync(
            new AdventureAsk(AdventureReach.Session, Story: new AdventureStory(Id, Card.Title, Card.Describe(), Secret.Secret, 2, 10, 5)),
            Now,
            CancellationToken.None);
}
