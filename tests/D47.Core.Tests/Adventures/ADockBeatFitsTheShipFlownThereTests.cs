using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.PlannerWorld;

namespace D47.Core.Tests.Adventures;

public sealed class ADockBeatFitsTheShipFlownThereTests
{
    private static readonly uint[] Seeds = [.. Enumerable.Range(1, 40).Select(seed => (uint)seed)];

    [Fact]
    public void TheSameDockStandsAfterABoardStepForTheSmallShip()
    {
        var bond = new DrawnActivity(TriggerKind.OnFootBond, null, 8) { PlaceBefore = true };
        var small = Places() with
        {
            Systems = [.. Places().Systems.Select(system => system with { AtWar = true })],
            Stations = [Station("Alpha Port", "Fixture Alpha", 2, 200, 110, frontline: true, large: 0)],
        };
        var aboard = Inputs(bond) with { Ship = new ShipLoadout { Type = "anaconda", Modules = [] } };

        Assert.Equal(ChapterRules.Pad, ChapterPlanner.Plan(aboard, small, 3).Refusal?.Rule);

        var board = new AdventureTrigger { Kind = TriggerKind.Board, ShipType = "sidewinder" };

        foreach (var seed in Seeds)
        {
            var steps = Steps(Planned(aboard with { Board = board }, small, seed)).ToList();

            Assert.Equal(TriggerKind.Board, steps[0].Kind);
            Assert.Contains(steps, step => step.Kind == TriggerKind.Dock);
        }
    }
}
