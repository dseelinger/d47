using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;
using static D47.Core.Tests.Stories.TheFinaleEndsWhereItsFirstChapterSaidTests;

namespace D47.Core.Tests.Stories;

/// <summary>A chapter before the scan is drawn nothing and written as before; a drawn chapter is planned around its activities, and a refused one is drawn again.</summary>
public sealed class ChaptersAreWrittenAroundDrawnActivitiesTests
{
    private static string PromptOf(RoundScriptedLlmProvider provider, int round) => provider.Requests[round].Prompt.History[0].Text;

    [Fact]
    public async Task ALastFinaleChapterWhoseDrawIncludesAnIllegalFamilyIsWrittenAndLandsOnTheDestination()
    {
        var heist = new DrawnActivity(TriggerKind.Mission, "Mission_OnFoot_Heist", 2) { PlaceBefore = true };
        var destination = new AdventureDestination(9001, "Edge Moon", 1, "Edge Moon 1 a");
        var ask = PlannedChapterWorld.Ask(PlannedChapterWorld.Story with { Slots = [heist], FinaleChapter = 4, FinaleChapters = 4, Destination = destination });
        var plan = await PlannedChapterWorld.PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(PlannedChapterWorld.Lines(plan.Objectives.Count)), RoundScriptedLlmProvider.Saying(PlannedChapterWorld.NoObjections));

        var outcome = await PlannedChapterWorld.Generator(provider).GenerateAsync(ask, Now, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(destination.Landing(), outcome.Draft!.Beats[^1].Steps[^1].Trigger);

        var steps = outcome.Draft.Beats.SelectMany(beat => beat.Steps).Select(step => step.Trigger).ToList();
        var at = steps.FindIndex(step => step.MissionFamily == "Mission_OnFoot_Heist");

        Assert.Equal(2, steps[at].Count);
        Assert.Equal("Fixture Beta", steps[at - 1].System);
    }

    [Fact]
    public async Task AFirstFinaleChapterWithNoDestinationIsPlannedAndKeepsTheOneCodeChose()
    {
        var ask = PlannedChapterWorld.Ask(PlannedChapterWorld.Story with { FinaleChapter = 1, FinaleChapters = 1 });
        var plan = await PlannedChapterWorld.PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(PlannedChapterWorld.Lines(plan.Objectives.Count)), RoundScriptedLlmProvider.Saying(PlannedChapterWorld.NoObjections));

        var outcome = await PlannedChapterWorld.Generator(provider).GenerateAsync(ask, Now, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.NotNull(plan.Destination);
        Assert.Equal(plan.Destination, outcome.Destination);
        Assert.Equal(plan.Destination!.Landing(), outcome.Draft!.Beats[^1].Steps[^1].Trigger);
        Assert.DoesNotContain("destination", PromptOf(provider, 0).Split("Answer with one JSON object")[^1]);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ARefusedSlotsObjectiveIsDrawnAgainForThatSlotOnly()
    {
        const string Key = "the-hard-way";

        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(PlannedChapterWorld.NewWords()), RoundScriptedLlmProvider.Saying(PlannedChapterWorld.NoObjections)));
        fixtures.SaveRunning(["chapter-one", Key], 2);

        Assert.Null(fixtures.Book.Write("F1", StoryFixtures.GeneratedChapter(Key, "The Hard Way", Id, new AdventureSpine { Premise = "A debt.", Want = "To pay it.", Stake = "Whether it can be.", Turn = "It is owed.", Ending = "Forgiven." },
            Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
            Beat("First Blood", "catalyst", new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 1 }, "One."),
            Beat("The Wing", "midpoint", new AdventureTrigger { Kind = TriggerKind.Bond, Count = 3 }, "Three bonds."),
            Beat("Home", "finale", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Hollow, System = "Dyson's Hollow" }, "End."))));
        Assert.Null(fixtures.Book.Begin("F1", Key, Now));
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(1)), "F1");
        fixtures.Book.Observe(AdventureFixtures.Event($$"""{ "timestamp":"{{Stamp(Now.AddMinutes(2))}}", "event":"Bounty", "TotalReward":100 }"""), "F1");
        fixtures.Director.Clock = () => Now.AddHours(1);

        await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken);

        // The bounty was done before the refusal, so it is not planned again; the redraw is neither it nor the refused bond.
        var slot = Assert.Single(Assert.Single(fixtures.Asks).Story!.Slots!);

        Assert.NotEqual(TriggerKind.Bond, slot.Kind);
        Assert.NotEqual(TriggerKind.Bounty, slot.Kind);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AChapterBeforeTheScanIsDrawnNothing()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Lines())));
        fixtures.Director.Game = () => Loaded(1_000_000);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Null(Assert.Single(fixtures.Asks).Story!.Slots);
    }
}
