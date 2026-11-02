using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class TheRouteFileIsCheckedAgainstEliteTargetsTests
{
    private static readonly NavRoute Plotted = new()
    {
        Hops = [new RouteHop("Sol", "G"), new RouteHop("Alpha Centauri", "G"), new RouteHop("Barnard's Star", "M")],
        ReadAt = DateTimeOffset.Parse("3311-01-01T00:00:00.5Z"),
    };

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Target(string system, int? jumps, int second = 1) => Event(
        jumps is { } count
            ? $$"""{"timestamp":"3311-01-01T00:00:{{second:00}}Z","event":"FSDTarget","Name":"{{system}}","StarClass":"K","RemainingJumpsInRoute":{{count}}}"""
            : $$"""{"timestamp":"3311-01-01T00:00:{{second:00}}Z","event":"FSDTarget","Name":"{{system}}","StarClass":"K"}""");

    private static LiveRoute Folded(params JournalEvent[] events)
    {
        var live = new LiveRoute();
        live.Fold(events, Plotted, "Sol", null);
        return live;
    }

    [Fact]
    public void ATargetThatMatchesTheFileIsNotAChange()
    {
        var live = Folded(Target("Alpha Centauri", 2));

        Assert.Equal(new RouteTarget("Alpha Centauri", "K", 2), live.Target);
        Assert.False(live.Changed);
    }

    [Fact]
    public void TheSameNameWithADifferentCountIsAChange() =>
        Assert.True(Folded(Target("Alpha Centauri", 3)).Changed);

    [Fact]
    public void ANameTheFileDoesNotHaveNextIsAChange() =>
        Assert.True(Folded(Target("Wolf 359", 2)).Changed);

    [Fact]
    public void ARouteClearDropsTheTarget()
    {
        var live = Folded(
            Target("Wolf 359", 2),
            Event("""{"timestamp":"3311-01-01T00:00:02Z","event":"NavRouteClear"}"""));

        Assert.Null(live.Target);
        Assert.False(live.Changed);
    }

    [Fact]
    public void ANewPlotDropsTheTarget()
    {
        var live = Folded(
            Target("Wolf 359", 2),
            Event("""{"timestamp":"3311-01-01T00:00:02Z","event":"NavRoute"}"""));

        Assert.Null(live.Target);
        Assert.False(live.Changed);
    }

    [Fact]
    public void ATargetOffAnyRouteDropsTheRouteTarget()
    {
        var live = Folded(Target("Wolf 359", 2), Target("Wolf 359", null, second: 2));

        Assert.Null(live.Target);
        Assert.False(live.Changed);
    }

    [Fact]
    public void ANewFileWriteClearsTheChange()
    {
        var live = Folded(Target("Wolf 359", 2, second: 1));
        Assert.True(live.Changed);

        var rewritten = Plotted with { ReadAt = DateTimeOffset.Parse("3311-01-01T00:00:05Z") };
        live.Fold([], rewritten, "Sol", null);

        Assert.Same(rewritten, live.File);
        Assert.Null(live.Target);
        Assert.False(live.Changed);
    }

    [Fact]
    public void ATargetFromTheSecondTheFileWasWrittenSurvivesItsLaterRead()
    {
        var live = new LiveRoute();
        live.Fold([Target("Alpha Centauri", 3, second: 0)], NavRoute.None, "Sol", null);

        // Read on the next tick, written half a second into the second the target came in.
        live.Fold([], Plotted, "Sol", null);

        Assert.NotNull(live.Target);
        Assert.True(live.Changed);
    }

    [Fact]
    public void InTheTunnelTheTargetIsCheckedFromTheDestination()
    {
        var live = new LiveRoute();

        // Before the tunnel's own target arrives, the target is the destination and is checked from Sol.
        live.Fold([Target("Alpha Centauri", 2)], Plotted, "Sol", "Alpha Centauri");
        Assert.False(live.Changed);

        live.Fold([Target("Barnard's Star", 1, second: 8)], Plotted, "Sol", "Alpha Centauri");
        Assert.False(live.Changed);

        live.Fold([], Plotted, "Alpha Centauri", null);
        Assert.False(live.Changed);
    }
}
