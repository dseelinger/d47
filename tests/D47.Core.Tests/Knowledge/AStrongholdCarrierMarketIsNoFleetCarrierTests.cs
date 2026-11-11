using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>A Stronghold Carrier's market follows the pledge rule rather than the fleet carrier rule, and the answer says what it left out.</summary>
public class AStrongholdCarrierMarketIsNoFleetCarrierTests
{
    private static MarketSnapshot Of(string type) => new() { Station = "Somewhere", System = "Anywhere", Type = type };

    [Fact]
    public void ArdentsStrongholdCarrierTypeIsAStrongholdCarrierAndNoFleetCarrier()
    {
        var market = Of("Stronghold Carrier");

        Assert.True(market.IsStrongholdCarrier);
        Assert.False(market.IsCarrier);
    }

    [Fact]
    public void ADrakeClassCarrierIsStillAFleetCarrier()
    {
        var market = Of("Drake-Class Carrier");

        Assert.False(market.IsStrongholdCarrier);
        Assert.True(market.IsCarrier);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("Aisling Duval", false)]
    [InlineData("Li Yong-Rui", true)]
    public void OnlyThePledgedPowersCarrierIsKept(string? pledged, bool otherPowers)
    {
        var market = new MarketSnapshot
        {
            Station = "Stronghold Carrier",
            System = "Aisling Hold",
            Type = "Planetary Outpost",
            ControllingPower = "Aisling Duval",
            PowerState = "Stronghold",
        };

        Assert.Equal(otherPowers, market.IsOtherPowersStrongholdCarrier(pledged));
    }

    private sealed class LeftOut(CommodityAnswer answer) : StubTradePlanService
    {
        public override Task<CommodityAnswer> FindCommodityAsync(
            CommoditySearch search,
            CancellationToken cancellationToken) => Task.FromResult(answer);
    }

    private sealed class SilentGalaxy : StubGalaxyService
    {
        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult("Ega", 0, []));

        public override Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(null);

        public override Task<StationSearchResult> FindStationsAsync(
            StationQuery query,
            CancellationToken cancellationToken) => Task.FromResult(new StationSearchResult("Ega", 0, []));
    }

    private static async Task<string> AskAsync(CommodityAnswer answer)
    {
        var surface = TestSurface.For(new MemoryInstall());

        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(
                new SilentGalaxy(), () => "Ega", surface.Settings, new LeftOut(answer), currentStation: null, board: null,
                now: null),
        ]);

        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["commodity"] = "Gold" };

        var result = await registry.InvokeAsync(
            "find_nearest_station", new ToolArguments(values), TestContext.Current.CancellationToken);

        return result.Content ?? string.Empty;
    }

    [Fact]
    public async Task APledgedAnswerSaysItLeftOutOtherPowersCarriers()
    {
        var said = await AskAsync(new CommodityAnswer([], 30, 0, true) { LeftOut = 2, Pledged = true });

        Assert.Contains("Left out 2 Stronghold Carriers of other Powers.", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnpledgedAnswerSaysCarriersDockOnlyPledgedCommanders()
    {
        var said = await AskAsync(new CommodityAnswer([], 30, 0, true) { LeftOut = 1, Pledged = false });

        Assert.Contains(
            "Left out 1 Stronghold Carrier, which dock only pledged Commanders.", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NothingLeftOutSaysNothingAboutCarriers()
    {
        var said = await AskAsync(new CommodityAnswer([], 30, 0, true));

        Assert.DoesNotContain("Stronghold", said, StringComparison.Ordinal);
    }
}
