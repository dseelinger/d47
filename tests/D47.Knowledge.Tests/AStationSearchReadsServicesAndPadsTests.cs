using System.Text.Json;
using D47.Core.Knowledge;
using D47.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>
/// A captured station search (material trader and technology broker within 60 ly of Sol, 2026-10-06), with
/// the market, modules and ships arrays removed (#810).
/// </summary>
public class AStationSearchReadsServicesAndPadsTests
{
    private static StationSearchResult Read() =>
        SpanshResponse.ReadStations(Fixture.Json("spansh-stations-trader-and-broker-near-sol.json"));

    [Fact]
    public void EachStationCarriesItsServicesPadsFactionAndReportDate()
    {
        var result = Read();

        Assert.Equal(41, result.Total);

        var station = result.Stations[0];

        Assert.Equal("Magnus Gateway", station.Name);
        Assert.Equal("EZ Aquarii", station.SystemName);
        Assert.Contains("Material Trader", station.Services);
        Assert.Contains("Technology Broker", station.Services);
        Assert.Contains("Restock", station.Services);
        Assert.Equal(5, station.LargePads);
        Assert.Equal(11, station.MediumPads);
        Assert.Equal("Future of EZ Aquarii", station.ControllingFaction);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 14, 35, 5, TimeSpan.Zero), station.UpdatedAt);
    }
}
