using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// Where the Commander is in a plotted route (Phase 37, "Progress").
/// <para>
/// The arithmetic the Routing tab draws, tested without a window — which is the whole reason it
/// lives in Core rather than in the page.
/// </para>
/// </summary>
public class RouteProgressTests
{
    private static RouteHop Hop(string system, double x = 0, double y = 0, double z = 0, string? starClass = "G") =>
        new(system, starClass) { Position = (x, y, z) };

    private static NavRoute Route(params RouteHop[] hops) => new() { Hops = hops };

    [Fact]
    public void NothingPlottedIsNotAPositionOfMinusOneJumps()
    {
        var progress = RouteProgress.For(NavRoute.None, "Sol");

        Assert.Equal(RouteProgress.None, progress);
        Assert.Equal(0, progress.JumpsRemaining);
        Assert.Null(progress.DistanceRemaining);
    }

    [Fact]
    public void StandingOnTheFirstHopLeavesEveryOtherHopAhead()
    {
        var progress = RouteProgress.For(
            Route(Hop("Sol"), Hop("Alpha Centauri", 4), Hop("Barnard's Star", 10)),
            "Sol");

        Assert.Equal(0, progress.Index);
        Assert.Equal(2, progress.JumpsRemaining);
        Assert.False(progress.OffRoute);
    }

    [Fact]
    public void ProgressIsMeasuredFromWhereTheCommanderIsRatherThanFromTheFile()
    {
        // The route file is not rewritten as the Commander advances, so "how far is left" is a
        // question about their position in it. Half way along a three-hop route is one jump left,
        // and the file says nothing different from when it was written.
        var progress = RouteProgress.For(
            Route(Hop("Sol"), Hop("Alpha Centauri", 4), Hop("Barnard's Star", 10)),
            "Alpha Centauri");

        Assert.Equal(1, progress.Index);
        Assert.Equal(1, progress.JumpsRemaining);
        Assert.Equal(6, progress.DistanceRemaining);
    }

    [Fact]
    public void ArrivingAtTheEndIsAPositionRatherThanAnAbsence()
    {
        var progress = RouteProgress.For(
            Route(Hop("Sol"), Hop("Alpha Centauri", 4)),
            "Alpha Centauri");

        Assert.Equal(1, progress.Index);
        Assert.Equal(0, progress.JumpsRemaining);
        Assert.False(progress.OffRoute);

        // No leg left to measure. Null rather than zero, because zero light years to go and
        // nothing to measure are different answers.
        Assert.Null(progress.DistanceRemaining);
    }

    [Fact]
    public void JumpingOffTheRouteLeavesAllOfItAhead()
    {
        var progress = RouteProgress.For(
            Route(Hop("Sol"), Hop("Alpha Centauri", 4), Hop("Barnard's Star", 10)),
            "Shinrarta Dezhra");

        Assert.True(progress.OffRoute);
        Assert.Equal(-1, progress.Index);

        // Every hop, because none has been reached — the same reading NavRoute.Ahead takes, and
        // it beats claiming the route is complete.
        Assert.Equal(3, progress.JumpsRemaining);
    }

    [Fact]
    public void AMissingCoordinateBlanksTheTotalRatherThanShorteningIt()
    {
        // A leg of unknown length makes the total unknown. Summing what is known would report a
        // shorter trip than the real one, which reads as an answer rather than as a gap.
        var progress = RouteProgress.For(
            Route(Hop("Sol"), new RouteHop("Nowhere", "G"), Hop("Barnard's Star", 10)),
            "Sol");

        Assert.Equal(2, progress.JumpsRemaining);
        Assert.Null(progress.DistanceRemaining);
    }

    [Fact]
    public void TheCurrentSystemIsMatchedTheWayEliteWritesIt()
    {
        // Elite's casing is not guaranteed to match between the journal and the route file.
        var progress = RouteProgress.For(Route(Hop("Sol"), Hop("Alpha Centauri", 4)), "SOL");

        Assert.Equal(0, progress.Index);
    }

    [Fact]
    public void NoCurrentSystemIsOffTheRouteRatherThanTheStartOfIt()
    {
        // Before the journal has said where the Commander is, assuming they are at hop zero would
        // report a route already begun.
        var progress = RouteProgress.For(Route(Hop("Sol"), Hop("Alpha Centauri", 4)), null);

        Assert.True(progress.OffRoute);
        Assert.Equal(2, progress.JumpsRemaining);
    }
}
