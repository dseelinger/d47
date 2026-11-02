using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class OutfittersAreAskedThroughEveryGalaxyWrapperTests
{
    private sealed class Outfitting : StubGalaxyService, IGalaxyService
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
