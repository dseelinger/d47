using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class TheAnswerSaysWhichStrongholdCarriersWereLeftOutTests
{
    private sealed class Answering(StationSearchResult result) : IGalaxyService
    {
        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(result);

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(
            MissionStationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static async Task<string> SaidAsync(int leftOut)
    {
        var surface = TestSurface.For(new MemoryInstall());

        surface.Settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var station = new StationSummary { Name = "Hutton Orbital", SystemName = "Alpha Centauri" };
        var galaxy = new Answering(new StationSearchResult("Sol", 1, [station]) { LeftOut = leftOut });

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
    public async Task NothingIsSaidWhenNothingWasLeftOut()
    {
        Assert.DoesNotContain("Left out", await SaidAsync(0), StringComparison.Ordinal);
    }
}
