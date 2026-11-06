using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.PlannerWorld;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A journal from <c>tests/fixtures/planner</c> folded into the game state gives the planner its inputs, and the plan is
/// built from fixed places with no model and no galaxy search: neither is constructed here.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AChapterIsPlannedFromAFoldedJournalTests
{
    private static readonly AdventureStory Story = new(
        "fixture-story", "Fixture Story", "Public", "Hidden", Chapter: 3, DaysRunning: 4, DaysSinceBeacon: 2,
        Stage: "Midpoint", Slots: [Bounty, new DrawnActivity(TriggerKind.Mission, "Mission_Courier", 3)]);

    [Fact]
    public void TheFoldedStateBuildsAFullPlan()
    {
        var state = Folded();
        var inputs = ChapterInputs.Of(state, new AdventureAsk(Story: Story));

        Assert.Equal(Start, inputs.System);
        Assert.Equal(Here, inputs.Position);
        Assert.Equal(AdventureGenerator.Radius(AdventureReach.NearHere, 22.5, carrier: false), inputs.Reach);
        Assert.Equal(PadSize.Medium, inputs.NeedsPad());
        Assert.Equal(StoryStage.Midpoint, inputs.Stage);
        Assert.Equal(4700001, Assert.Single(inputs.Missions).Id);

        var places = Places() with
        {
            Missions = [new ChapterMissionStop(4700001, "Fixture Second", 47002, new StarPosition(125, 0, 0), "Fixture Gate", 4700002)],
        };
        var plan = Planned(inputs, places);

        Assert.InRange(plan.Objectives.Count, ChapterPlanner.FewestObjectives, ChapterPlanner.MostObjectives);
        Assert.Contains(plan.Objectives, objective => objective.Function == ChapterFunctions.Midpoint);
        Assert.Contains(Steps(plan), step => step is { Kind: TriggerKind.Bounty, Count: 5 });
        Assert.Contains(Steps(plan), step => step is { Kind: TriggerKind.Mission, MissionFamily: "Mission_Courier", Count: 3 });
        Assert.Equal(Describe(plan), Describe(Planned(ChapterInputs.Of(Folded(), new AdventureAsk(Story: Story)), places)));
    }

    private static CommanderGameState Folded()
    {
        var store = new GameStateStore();

        foreach (var line in EmbeddedFixture.Lines("planner.Journal.2026-11-01T100000.01.log"))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var journalEvent));
            store.Apply(journalEvent!);
        }

        return store.Active!;
    }
}
