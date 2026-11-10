using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class TheAnswerSaysWhichStrongholdCarriersWereLeftOutTests
{
    private sealed class Answering(StationSearchResult result) : StubGalaxyService
    {
        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private static async Task<string> SaidAsync(int leftOut, bool pledged = true)
    {
        var surface = TestSurface.For(new MemoryInstall());


        var station = new StationSummary { Name = "Hutton Orbital", SystemName = "Alpha Centauri" };
        var galaxy = new Answering(new StationSearchResult("Sol", 1, [station]) { LeftOut = leftOut, Pledged = pledged });

        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(galaxy, () => "Sol", surface.Settings)]);

        var result = await registry.InvokeAsync(
            "find_nearest_station",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["module"] = "Frame Shift Drive" }),
            TestContext.Current.CancellationToken);

        return result.Content;
    }

    [Fact]
    public async Task TheCountOfCarriersLeftOutIsSaid()
    {
        Assert.Contains("Left out 2 Stronghold Carriers of other Powers.", await SaidAsync(2), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OneCarrierIsSaidInTheSingular()
    {
        Assert.Contains("Left out 1 Stronghold Carrier of other Powers.", await SaidAsync(1), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnpledgedCommanderIsToldCarriersDockOnlyPledgedCommanders()
    {
        Assert.Contains(
            "Left out 2 Stronghold Carriers, which dock only pledged Commanders.",
            await SaidAsync(2, pledged: false),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task NothingIsSaidWhenNothingWasLeftOut()
    {
        Assert.DoesNotContain("Left out", await SaidAsync(0), StringComparison.Ordinal);
    }
}
