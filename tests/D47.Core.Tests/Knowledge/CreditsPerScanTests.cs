using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class CreditsPerScanTests
{
    private sealed class Routes : StubRouteService
    {
        public ExobiologyQuery? LastQuery { get; private set; }

        public required ExobiologyRoute Route { get; init; }

        public override Task<ExobiologyRoute?> PlotExobiologyAsync(ExobiologyQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult<ExobiologyRoute?>(Route);
        }
    }

    private static ExobiologyStop Stop(string name, int jumps, params long[] values) =>
        new(name, jumps, [
            new ExobiologyBody(name + " 1", null)
            {
                LandmarkValue = values.Sum(),
                Species = [.. values.Select((value, i) => new ExobiologySpecies("G", "S" + i, 1, value))],
            },
        ]);

    private static ExobiologyRoute Many(int count) =>
        new([.. Enumerable.Range(0, count).Select(i => Stop("S" + i, 1, 2_000_000))]);

    [Fact]
    public void TheStopThatPaysMoreForEachSpeciesIsKept()
    {
        var route = new ExobiologyRoute([
            Stop("Seven", 2, 8_142_857, 8_142_857, 8_142_857, 8_142_857, 8_142_857, 8_142_857, 8_142_858),
            Stop("Three", 2, 16_666_667, 16_666_667, 16_666_666),
        ]);

        Assert.Equal("Three", Assert.Single(route.KeepBestPerScan(1).Stops).System);
    }

    [Fact]
    public void KeptStopsStayInTheOriginalOrderAndKeepTheJumps()
    {
        var route = new ExobiologyRoute([
            Stop("A", 1, 5_000_000),
            Stop("B", 2, 1_000_000),
            Stop("C", 3, 9_000_000),
            Stop("D", 4, 1_000_000),
        ]) { UnlistedJumps = 1 };

        var kept = route.KeepBestPerScan(2);

        Assert.Equal(["A", "C"], kept.Stops.Select(stop => stop.System));
        Assert.Equal([1, 5], kept.Stops.Select(stop => stop.Jumps));
        Assert.Equal(5, kept.UnlistedJumps);
        Assert.Equal(route.TotalJumps, kept.TotalJumps);
    }

    [Fact]
    public void AStopWithNoListedSpeciesRanksLast()
    {
        var route = new ExobiologyRoute([new ExobiologyStop("Empty", 1, []), Stop("Some", 1, 1_000_000)]);

        Assert.Equal("Some", Assert.Single(route.KeepBestPerScan(1).Stops).System);
        Assert.Equal(0, route.Stops[0].PerScan);
    }

    [Fact]
    public void ARouteAtTheCountIsUnchanged()
    {
        var route = Many(5);

        Assert.Same(route, route.KeepBestPerScan(5));
    }

    private static async Task<(Routes, ToolResult)> Plot(ExobiologyRoute route, string json)
    {
        var routes = new Routes { Route = route };
        var settings = TestSurface.For(new MemoryInstall()).Settings;
        var result = await CapabilityRegistry
            .Build([ExobiologyCapability.Create(routes, () => null, settings)])
            .InvokeAsync("plot_exobiology_route", ToolArguments.FromJson(json), TestContext.Current.CancellationToken);

        return (routes, result);
    }

    [Fact]
    public async Task WithoutTheArgumentTheRequestAndRouteAreUntouched()
    {
        var (routes, result) = await Plot(Many(15), """{"from":"Sol","max_results":10}""");

        Assert.Equal(10, routes.LastQuery!.MaxResults);
        Assert.DoesNotContain("credits per scan", result.Content);
        Assert.Contains("15 systems", result.Content);
    }

    [Theory]
    [InlineData(10, 30)]
    [InlineData(20, 50)]
    public async Task WithTheArgumentTheServiceIsAskedForMore(int stops, int asked)
    {
        var (routes, result) = await Plot(Many(stops), $$"""{"from":"Sol","max_results":{{stops}},"credits_per_scan":true}""");

        Assert.Equal(asked, routes.LastQuery!.MaxResults);
        Assert.Contains("credits per scan", result.Content);
        Assert.Contains("cr a scan", result.Content);
    }
}
