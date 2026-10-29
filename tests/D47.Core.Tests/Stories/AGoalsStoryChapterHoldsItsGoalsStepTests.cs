using D47.Core.Adventures;
using D47.Core.Goals;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A chapter of a story whose card sets <c>goals</c> carries a Goals step, and a draft without its objective is refused.</summary>
public sealed class AGoalsStoryChapterHoldsItsGoalsStepTests
{
    private const string BeatsWithoutTheRank = """
        {"opening": "Again.", "reply": "Here.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Back."},
          {"title": "Any Mission", "function": "turn", "kind": "mission", "count": 2, "mission": null, "line": "Work."},
          {"title": "The Bounties", "function": "resolution", "kind": "bounty", "count": 3, "line": "Paid."}
        ]}
        """;

    [Fact]
    public async Task ADraftWithoutTheGoalsObjectiveIsRefusedNamingIt()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsWithoutTheRank),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(BeatsWithoutTheRank, 2)),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(BeatsWithoutTheRank, 2)));
        var goal = new GoalClimbStep("rank.combat", "Elite V in Combat")
        {
            Objective = new AdventureTrigger { Kind = TriggerKind.Rank, Career = "Combat", Rank = 2 },
        };

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy()).GenerateAsync(
            new AdventureAsk(
                AdventureReach.Session,
                AdventureLength.Short,
                Story: new AdventureStory(Id, Card.Title, Card.Describe(), Secret.Secret, 3, 10, 5, Goal: goal)),
            Now,
            CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("has no objective for its Goals step: a \"rank\" objective for Combat Rank 2 of 13, Novice", outcome.Refusal);
        Assert.Contains("works the Commander's goal \"Elite V in Combat\"", provider.Requests[1].Prompt.History[0].Text);
    }

    [Trait("Category", "Integration")]
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnlyAGoalsCardsChapterCarriesAStep(bool goals)
    {
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Saying(NextSpine), RoundScriptedLlmProvider.Saying(NextBeats), RoundScriptedLlmProvider.Saying(NothingObjected)),
            card: Card with { Length = "2-weeks", Goals = goals });
        fixtures.Director.Game = () => Loaded(1_000_000);

        // A two-week story's beacon scan is narrated at the pick, so chapter one is the first after it.
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var story = fixtures.Asks[0].Story!;

        if (!goals)
        {
            Assert.Null(story.Goal);
            return;
        }

        // Every arc is unknown to a state with no ranks read, so the first in the catalogue is worked.
        Assert.Equal("rank.combat", story.Goal?.Key);
        Assert.Equal("Combat", story.Goal?.Draw);
        Assert.All(story.Slots!, slot => Assert.True(story.Goal!.Earns(ChapterActivities.Find(slot.Kind, slot.Family, slot.Organic)!)));
    }
}
