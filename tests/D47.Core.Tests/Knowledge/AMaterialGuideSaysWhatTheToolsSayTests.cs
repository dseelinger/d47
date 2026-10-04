using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AMaterialGuideSaysWhatTheToolsSayTests
{
    [Fact]
    public void AGrade4RawMaterialTradesUpDownAndAcross()
    {
        var detail = MaterialGuide.For("selenium", null)!;

        Assert.Equal(4, detail.Grade);
        Assert.Equal(150, detail.Capacity);
        Assert.Null(detail.Held);
        Assert.Equal(new MaterialTrade(TradeDirection.Up, 5, 6, 1), Assert.Single(detail.Trades, t => t.Direction == TradeDirection.Up));
        Assert.Equal(new MaterialTrade(TradeDirection.Down, 3, 1, 3), Assert.Single(detail.Trades, t => t.Direction == TradeDirection.Down));
        Assert.Equal(new MaterialTrade(TradeDirection.Across, 4, 6, 1), Assert.Single(detail.Trades, t => t.Direction == TradeDirection.Across));
        Assert.NotEmpty(detail.HowToFarm);
    }

    [Fact]
    public void AGrade5MaterialHasNoTradeUp()
    {
        var detail = MaterialGuide.For("adaptiveencryptors", null)!;

        Assert.Equal(5, detail.Grade);
        Assert.DoesNotContain(detail.Trades, t => t.Direction == TradeDirection.Up);
        Assert.Contains(detail.Trades, t => t.Direction == TradeDirection.Down);
    }

    [Fact]
    public void AGuardianMaterialHasNoTrades()
    {
        var detail = MaterialGuide.For("guardian_powercell", null)!;

        Assert.Empty(detail.Trades);
    }

    [Fact]
    public void AnUnknownMaterialHasNoDetail()
    {
        Assert.Null(MaterialGuide.For("not a material at all", null));
    }

    [Fact]
    public async Task WithTheSearchOffTheGalaxyIsNeverAsked()
    {
        var searchOn = false;
        IGalaxyService? galaxy = searchOn ? new Galaxy([]) : null;

        var places = await MaterialGuide.NearestAsync("selenium", null, galaxy, TestContext.Current.CancellationToken, near: "Sol");

        Assert.Same(MaterialPlaces.None, places);
    }

    [Fact]
    public async Task ARawMaterialsPlacesComeRichestFirst()
    {
        var galaxy = new Galaxy(
        [
            new BodySummary { Name = "Poor 1", SystemName = "Poor", Distance = 3, Materials = [("Selenium", 1.5)] },
            new BodySummary { Name = "Rich 2", SystemName = "Rich", Distance = 9, Materials = [("Selenium", 4.25)] },
        ]);

        var places = await MaterialGuide.NearestAsync("selenium", null, galaxy, TestContext.Current.CancellationToken, near: "Sol");

        Assert.Equal(["Rich 2", "Poor 1"], places.Places.Select(p => p.Place));
        Assert.Equal("Rich", places.FirstSystem);
        Assert.Equal("4.3%", places.Places[0].Note);
        Assert.Equal(9, places.Places[0].Distance);
    }

    private sealed class Galaxy(IReadOnlyList<BodySummary> bodies) : IGalaxyService
    {
        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult(query.ReferenceSystem, bodies.Count, bodies));

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
