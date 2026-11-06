using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.PlannerWorld;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A finale chapter after the first is planned toward the story's destination: the last finale chapter lands on it, and one that
/// starts beyond reach of it ends closer to it than it started.
/// </summary>
public sealed class AFinaleChapterIsPlannedTowardItsDestinationTests
{
    private static readonly AdventureDestination Destination = new(9, "Fixture Far", 4, "Fixture Far 4");

    private static readonly StarPosition FarAway = new(400, 0, 0);

    private static ChapterInputs Finale(int chapter) => Inputs(Bounty, Codex) with { FinaleChapter = chapter, FinaleChapters = 4, Destination = Destination };

    [Fact]
    public void TheLastFinaleChapterEndsLandingOnTheDestination()
    {
        var plan = Planned(Finale(4), Places() with { DestinationAt = FarAway });
        var last = plan.Objectives[^1];

        Assert.Equal(PlannedPurpose.Destination, last.Purpose);
        Assert.Equal(Destination.Landing(), Assert.Single(last.Steps));
        Assert.Equal(ChapterFunctions.Resolution, last.Function);
        Assert.Equal(Destination, plan.Destination);
    }

    [Fact]
    public void AMiddleFinaleChapterBeyondReachEndsCloserToTheDestination()
    {
        var places = Places() with
        {
            DestinationAt = FarAway,
            Pull = new ChapterArea(ChapterAreaState.Listed, [Station("Waypoint Port", "Fixture Waypoint", 7, 700, 145)], []),
        };

        foreach (var seed in Enumerable.Range(1, 20).Select(seed => (uint)seed))
        {
            var plan = Planned(Finale(2), places, seed);
            var last = plan.Objectives[^1];
            var end = last.Steps[^1];
            var at = places.Stations.Concat(places.Pull.Stations).Where(station => station.SystemName == end.System).Select(station => station.Position)
                .Concat(places.Systems.Where(system => system.Name == end.System).Select(system => system.Position))
                .Concat(places.Bodies.Where(body => body.SystemName == end.System).Select(body => body.Position))
                .OfType<StarPosition>().First();

            Assert.Equal(PlannedPurpose.Closer, last.Purpose);
            Assert.True(at.DistanceTo(FarAway) < Here.DistanceTo(FarAway), Describe(plan));
        }
    }

    [Fact]
    public void AMiddleFinaleChapterWithNoPlaceTowardTheDestinationIsRefused()
    {
        var places = new ChapterPlaces
        {
            Systems = [Place(Start, 1, 100), Place("Fixture Gamma", 4, 90)],
            DestinationAt = FarAway,
            Pull = new ChapterArea(ChapterAreaState.NoneFound, [], []),
        };

        var result = ChapterPlanner.Plan(Finale(2), places, 7);

        Assert.False(result.Succeeded);
        Assert.Equal(ChapterRules.Finale, result.Refusal!.Rule);
    }

    [Fact]
    public void FinaleChapterOneWrittenAgainWithItsDestinationIsHeldToNoEnd()
    {
        var plan = Planned(Finale(1), Places() with { DestinationAt = FarAway });

        Assert.DoesNotContain(plan.Objectives, objective => objective.Purpose is PlannedPurpose.Closer or PlannedPurpose.Destination);
    }

    [Fact]
    public void AMiddleFinaleChapterWithinReachOfTheDestinationIsHeldToNoEnd()
    {
        var plan = Planned(Finale(2), Places() with { DestinationAt = new StarPosition(130, 0, 0) });

        Assert.DoesNotContain(plan.Objectives, objective => objective.Purpose is PlannedPurpose.Closer or PlannedPurpose.Destination);
    }
}
