using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.PlannerWorld;

namespace D47.Core.Tests.Adventures;

public sealed class APermitSystemIsNeverAStopTests
{
    private const string Permit = "Shinrarta Dezhra";

    private static readonly uint[] Seeds = [.. Enumerable.Range(1, 40).Select(seed => (uint)seed)];

    [Fact]
    public void APermitSystemTheCommanderIsInIsNoStopEither()
    {
        var places = Places() with
        {
            Systems = [.. Places().Systems, Place(Permit, 90, 100)],
            Stations = [.. Places().Stations, Station("Jameson Memorial", Permit, 90, 900, 100)],
        };
        var inputs = Inputs(Bounty) with { System = Permit };

        foreach (var seed in Seeds)
        {
            Assert.DoesNotContain(Placed(Planned(inputs, places, seed)), step => step.System == Permit);
        }
    }

    [Fact]
    public void AVisitedPermitSystemIsOfferedAndStands()
    {
        var only = new ChapterPlaces
        {
            Systems = [Place(Permit, 90, 105)],
            Stations = [Station("Jameson Memorial", Permit, 90, 900, 105)],
        };

        Assert.Null(ChapterPlanner.Plan(Inputs(Bounty), only, 3).Plan);

        var visited = only with { Visited = [Permit] };

        foreach (var seed in Seeds)
        {
            Assert.Contains(Placed(Planned(Inputs(Bounty), visited, seed)), step => step.System == Permit);
        }
    }
}
