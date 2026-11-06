using D47.Core.Adventures;
using D47.Core.Goals;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A chapter of a story whose card sets <c>goals</c> carries a Goals step, and a draft without its objective is refused.</summary>
public sealed class AGoalsStoryChapterHoldsItsGoalsStepTests
{
    [Fact]
    public async Task AGoalsStoryChapterIsPlannedWithItsGoalsStepAndTheWriterIsToldWhichGoal()
    {
        var goal = new GoalClimbStep("rank.combat", "Elite V in Combat")
        {
            Objective = new AdventureTrigger { Kind = TriggerKind.Rank, Career = "Combat", Rank = 2 },
        };
        var ask = PlannedChapterWorld.Ask(PlannedChapterWorld.Story with { Goals = [goal] });
        var plan = await PlannedChapterWorld.PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(PlannedChapterWorld.Lines(plan.Objectives.Count)), RoundScriptedLlmProvider.Saying(PlannedChapterWorld.NoObjections));

        var outcome = await PlannedChapterWorld.Generator(provider).GenerateAsync(ask, Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains(outcome.Draft!.Beats.SelectMany(beat => beat.Steps), step => step.Trigger is { Kind: TriggerKind.Rank, Career: "Combat", Rank: 2 });
        Assert.Contains("works the Commander's goal \"Elite V in Combat\"", provider.Requests[0].Prompt.History[0].Text);
    }

    [Trait("Category", "Integration")]
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnlyAGoalsCardsChapterCarriesAStep(bool goals)
    {
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Saying(NextLines), RoundScriptedLlmProvider.Saying(NothingObjected)),
            card: Card with { Length = "2-weeks", Goals = goals });
        fixtures.Director.Game = () => Loaded(1_000_000);
        fixtures.Director.Ratings = () => null;

        // A two-week story's beacon scan is narrated at the pick, so chapter one is the first after it.
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var story = fixtures.Asks[0].Story!;

        if (!goals)
        {
            Assert.Null(story.Goals);
            return;
        }

        // Every arc is unknown to a state with no ranks read, so the first two in the catalogue are worked.
        Assert.Equal(["rank.combat", "rank.trade"], story.Goals!.Select(goal => goal.Key));
        // Bounties are the only Combat row this Commander can be drawn, so the first arc's second slot goes to the second arc.
        Assert.Equal(["Combat", "Trade", "Trade"], story.Slots!.Select(slot => story.Goals!.First(goal => goal.Earns(ChapterActivities.Find(slot.Kind, slot.Family, slot.Organic)!)).Draw));
    }
}
