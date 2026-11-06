using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.PlannerWorld;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A rewrite's new objectives are planned in code: as many as the objectives replaced where the content allows, with their labels,
/// away from the refused objective's place, and ending in a system the Commander asked for however far it is.
/// </summary>
public sealed class AChangedChaptersObjectivesArePlannedInCodeTests
{
    private static readonly uint[] Seeds = [.. Enumerable.Range(1, 40).Select(seed => (uint)seed)];

    [Fact]
    public void ARewriteAimsForTheReplacedCountAndTakesTheReplacedLabels()
    {
        string[] labels = ["turn", "setback", "resolution"];

        foreach (var seed in Seeds)
        {
            var plan = Planned(Inputs(Bounty) with { Functions = labels }, Places(), seed);

            Assert.Equal(labels, plan.Objectives.Select(objective => objective.Function));
        }
    }

    [Fact]
    public void ContentBeyondTheReplacedCountAddsComplicationsBeforeTheLastLabel()
    {
        var plan = Planned(Inputs(Bounty, Codex, Salvage) with { Functions = ["resolution"] }, Places());

        Assert.Equal(["complication", "complication", "resolution"], plan.Objectives.Select(objective => objective.Function));
    }

    [Fact]
    public void ARewriteWithNoActivityLeftIsPlannedWithStoryStops()
    {
        var plan = Planned(Inputs() with { Functions = ["setback", "resolution"] }, Places());

        Assert.Equal(2, plan.Objectives.Count);
        Assert.All(plan.Objectives, objective => Assert.Equal(PlannedPurpose.Stop, objective.Purpose));
    }

    [Fact]
    public void TheRefusedObjectivesPlaceIsNotUsedAgain()
    {
        var dock = new AdventureTrigger { Kind = TriggerKind.Dock, MarketId = 200, System = "Fixture Alpha", Station = "Alpha Port" };
        var arrive = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 3, System = "Fixture Beta" };

        foreach (var seed in Seeds)
        {
            var steps = Steps(Planned(Inputs() with { Functions = ["complication", "turn", "resolution"], Avoid = [dock, arrive] }, Places(), seed)).ToList();

            Assert.DoesNotContain(steps, step => step is { Kind: TriggerKind.Dock, MarketId: 200 });
            Assert.DoesNotContain(steps, step => step is { Kind: TriggerKind.Arrive, System: "Fixture Beta" });
        }
    }

    [Fact]
    public void ASystemTheCommanderAskedForEndsThePlanInOneArrivalBeyondTheReach()
    {
        var heading = new AdventureHeading("Fixture Far", 6, new StarPosition(400, 0, 0));

        foreach (var seed in Seeds)
        {
            var last = Planned(Inputs(Bounty) with { Functions = ["turn", "resolution"], Heading = heading }, Places(), seed).Objectives[^1];

            var arrive = Assert.Single(last.Steps);
            Assert.Equal(TriggerKind.Arrive, arrive.Kind);
            Assert.Equal("Fixture Far", arrive.System);
        }

        Assert.True(Here.DistanceTo(heading.Position) > Reach);
    }

    [Fact]
    public void TheLabelsFollowTheCountTheTurnKeptWhenObjectivesAreFewer()
    {
        Assert.Equal(["turn", "setback", "resolution"], ChapterFunctions.Relabel(["turn", "setback", "resolution"], 3));
        Assert.Equal(["turn", "complication", "resolution"], ChapterFunctions.Relabel(["turn", "resolution"], 3));
        Assert.Equal(["turn", "setback", "resolution"], ChapterFunctions.Relabel(["complication", "turn", "setback", "complication", "resolution"], 3));
        Assert.Equal(["resolution"], ChapterFunctions.Relabel(["turn", "resolution"], 1));
        Assert.Equal(["complication", "complication"], ChapterFunctions.Relabel([], 2));
    }

    [Fact]
    public void EachRewriteOfAnObjectiveDrawsFromItsOwnSeed()
    {
        var chapter = new Adventure { Key = "the-chapter", Name = "The Chapter", Beats = [] };
        var again = chapter with { RewrittenAt = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero) };

        uint[] seeds =
        [
            AdventureGenerator.RewriteSeed(new AdventureRewrite(chapter, 1)),
            AdventureGenerator.RewriteSeed(new AdventureRewrite(chapter, 2)),
            AdventureGenerator.RewriteSeed(new AdventureRewrite(chapter, 1, Step: 1)),
            AdventureGenerator.RewriteSeed(new AdventureRewrite(again, 1)),
        ];

        Assert.Equal(seeds.Length, seeds.Distinct().Count());
        Assert.Equal(seeds[0], AdventureGenerator.RewriteSeed(new AdventureRewrite(chapter, 1)));
    }

    [Fact]
    public void AChapterWrittenAgainOrAfterAFailureIsPlannedFromTheNextSeed()
    {
        var story = PlannedChapterWorld.Story;
        var first = new Adventure { Key = "first-draft", Name = "First Draft", Beats = [] };
        var second = new Adventure { Key = "second-draft", Name = "Second Draft", Beats = [] };

        uint[] seeds =
        [
            AdventureGenerator.PlanSeed(PlannedChapterWorld.Ask(story)),
            AdventureGenerator.PlanSeed(PlannedChapterWorld.Ask(story) with { Failed = new AdventureChapter(first, []) }),
            AdventureGenerator.PlanSeed(PlannedChapterWorld.Ask(story) with { SetAside = new AdventureSetAside(new AdventureChapter(first, []), [], null, null) }),
            AdventureGenerator.PlanSeed(PlannedChapterWorld.Ask(story) with { SetAside = new AdventureSetAside(new AdventureChapter(second, []), [], null, null) }),
        ];

        Assert.NotEqual(seeds[0], seeds[1]);
        Assert.Equal(seeds[1], seeds[2]);
        Assert.NotEqual(seeds[2], seeds[3]);
    }
}
