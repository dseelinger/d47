using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class OutfittersAreAskedThroughEveryGalaxyWrapperTests
{
    private sealed class Outfitting : IGalaxyService
    {
        public List<string> Asked { get; } = [];

        public Task<IReadOnlyList<OutfitterStation>> FindOutfittersAsync(
            IReadOnlyList<OutfittingItem> items,
            string referenceSystem,
            double maxDistance,
            PadSize? minPad,
            int? maxReportAgeHours,
            CancellationToken cancellationToken)
        {
            Asked.Add($"{string.Join("+", items)} near {referenceSystem} within {maxDistance} on {minPad}");

            return Task.FromResult<IReadOnlyList<OutfitterStation>>(
                [new OutfitterStation(new StationSummary { Name = "Galileo", SystemName = "Sol" }, items)]);
        }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    [Fact]
    public async Task TheStationsComeFromTheServiceInside()
    {
        Assert.True(OutfittingItem.TryParse("Fuel Scoop", out var scoop, out _));
        var inner = new Outfitting();
        var names = new GalaxySearchNames(new StationPowerFilter(inner, () => null));

        var found = await ((IGalaxyService)names).FindOutfittersAsync(
            [scoop], "Sol", 30, PadSize.Medium, null, TestContext.Current.CancellationToken);

        Assert.Equal("Galileo", Assert.Single(found).Station.Name);
        Assert.Equal(["Fuel Scoop near Sol within 30 on Medium"], inner.Asked);
        Assert.Contains("Sol", names.Names);
    }
}
