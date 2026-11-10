using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

public class StrongholdCarriersFollowThePledgeTests
{
    private static StationSummary Carrier(string power) => new()
    {
        Name = "Stronghold Carrier",
        SystemName = "Somewhere",
        Type = "Planetary Outpost",
        ControllingPower = power,
        PowerState = "Stronghold",
    };

    private static readonly StationSummary Outpost = new()
    {
        Name = "Hutton Orbital",
        SystemName = "Alpha Centauri",
        Type = "Planetary Outpost",
    };

    private static readonly PowerplayPledge Archer = new("Jerome Archer");

    private const string MissionResponse =
        """
        {"count":1,"results":[{"name":"Somewhere","id64":42,"distance":3.5,"controlling_power":"Zemina Torval",
         "power_state":"Stronghold","stations":[{"name":"Stronghold Carrier","type":"Drake-Class Carrier"},
         {"name":"Hutton Orbital","type":"Orbis Starport"}]}]}
        """;

    private sealed class Held : IGalaxyService
    {
        public int Calls { get; private set; }

        public StationQuery? Last { get; private set; }

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            Calls++;
            Last = query;
            IReadOnlyList<StationSummary> rows = [Carrier("Zemina Torval"), Carrier("Jerome Archer"), Outpost];
            return Task.FromResult(new StationSearchResult("Sol", rows.Count, rows));
        }

        public Task<IReadOnlyList<OutfitterStation>> FindOutfittersAsync(
            IReadOnlyList<OutfittingItem> items,
            string referenceSystem,
            double maxDistance,
            PadSize? minPad,
            int? maxReportAgeHours,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OutfitterStation>>(
                [new(Carrier("Zemina Torval"), []), new(Outpost, [])]);

        public Task<MissionStationScan> FindMissionStationsAsync(
            MissionStationQuery query, CancellationToken cancellationToken)
        {
            using var document = JsonDocument.Parse(MissionResponse);
            return Task.FromResult(SpanshResponse.ReadMissionStations(document));
        }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<FactionReport> FactionAsync(string faction, CancellationToken cancellationToken, bool refresh = false, IProgress<FactionProgress>? progress = null) =>
            throw new NotSupportedException();
    }

    private static StationQuery Query(string? stationTypes = null, string? power = null)
    {
        var filters = power is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["power"] = power };

        Assert.True(
            StationQuery.TryParse(
                new StationSearch
                {
                    ReferenceSystem = "Sol",
                    StationTypes = stationTypes,
                    Filters = filters,
                },
                out var query,
                out var failure),
            failure);

        return query;
    }

    private static async Task<StationSearchResult> Search(
        PowerplayPledge pledge, string? stationTypes = null, string? power = null, Held? held = null) =>
        await new StationPowerFilter(held ?? new Held(), () => pledge)
            .FindStationsAsync(Query(stationTypes, power), TestContext.Current.CancellationToken);

    private static string[] Names(StationSearchResult result) =>
        [.. result.Stations.Select(station => $"{station.Name}/{station.ControllingPower}")];

    [Fact]
    public async Task AnUnpledgedCommanderGetsTheOutpostOnlyWhateverTheSearchNames()
    {
        Assert.Equal(["Hutton Orbital/"], Names(await Search(PowerplayPledge.Unpledged, "Outpost")));
        Assert.Equal(["Hutton Orbital/"], Names(await Search(PowerplayPledge.Unpledged, power: "Zemina Torval")));
    }

    [Fact]
    public async Task APledgedCommanderGetsTheirOwnPowersCarrierAndTheOutpost()
    {
        Assert.Equal(
            ["Stronghold Carrier/Jerome Archer", "Hutton Orbital/"],
            Names(await Search(Archer, "Outpost")));
    }

    [Fact]
    public async Task NamingAnotherPowerDoesNotKeepItsCarrier()
    {
        Assert.Equal(
            ["Stronghold Carrier/Jerome Archer", "Hutton Orbital/"],
            Names(await Search(Archer, power: "Zemina Torval")));
    }

    [Fact]
    public async Task AnUnpledgedCarrierSearchSendsNothingAndSaysWhy()
    {
        var held = new Held();

        var result = await Search(PowerplayPledge.Unpledged, "Stronghold carrier", held: held);

        Assert.Equal(0, held.Calls);
        Assert.Equal(
            "Stronghold Carriers dock only Commanders pledged to their Power, and you are not pledged.",
            result.Refusal);
        Assert.Empty(result.Stations);
    }

    [Fact]
    public async Task APledgedCarrierSearchSendsThePledgedPowerAsTheControllingPower()
    {
        var held = new Held();

        await Search(Archer, "Stronghold carrier", held: held);

        Assert.Contains(
            "\"system_controlling_power\":{\"value\":[\"Jerome Archer\"]}",
            SpanshRequest.Stations(held.Last!),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task APledgedCarrierSearchNamingAnotherPowerSendsNothingAndSaysWhy()
    {
        var held = new Held();

        var result = await Search(Archer, "Stronghold carrier", "Zemina Torval", held);

        Assert.Equal(0, held.Calls);
        Assert.Equal("You can dock only at Jerome Archer's Stronghold Carriers.", result.Refusal);
    }

    [Fact]
    public async Task OutfittersDropAnotherPowersCarrierUnpledgedAndPledgedToAnotherPower()
    {
        foreach (var pledge in new[] { PowerplayPledge.Unpledged, Archer })
        {
            var rows = await Outfitters(pledge);

            Assert.Equal(["Hutton Orbital"], rows.Select(row => row.Station.Name));
        }
    }

    [Fact]
    public async Task OutfittersKeepTheCarrierForItsOwnPower()
    {
        var rows = await Outfitters(new PowerplayPledge("Zemina Torval"));

        Assert.Equal(["Stronghold Carrier", "Hutton Orbital"], rows.Select(row => row.Station.Name));
    }

    private static Task<IReadOnlyList<OutfitterStation>> Outfitters(PowerplayPledge pledge) =>
        new StationPowerFilter(new Held(), () => pledge).FindOutfittersAsync(
            [],
            "Sol",
            50,
            null,
            null,
            TestContext.Current.CancellationToken);

    [Fact]
    public void AMissionStationCarriesItsSystemsPowerAndState()
    {
        using var document = JsonDocument.Parse(MissionResponse);

        var carrier = SpanshResponse.ReadMissionStations(document).Systems[0].Stations[0];

        Assert.Equal("Stronghold", carrier.PowerState);
        Assert.Equal("Zemina Torval", carrier.ControllingPower);
        Assert.True(carrier.IsStrongholdCarrier);
    }

    [Fact]
    public async Task MissionStationsDropTheCarrierUnpledgedAndKeepItForItsOwnPower()
    {
        var unpledged = await new StationPowerFilter(new Held(), () => PowerplayPledge.Unpledged)
            .FindMissionStationsAsync(null!, TestContext.Current.CancellationToken);
        var own = await new StationPowerFilter(new Held(), () => new PowerplayPledge("Zemina Torval"))
            .FindMissionStationsAsync(null!, TestContext.Current.CancellationToken);

        Assert.Equal(["Hutton Orbital"], unpledged.Systems[0].Stations.Select(station => station.Name));
        Assert.Equal(["Stronghold Carrier", "Hutton Orbital"], own.Systems[0].Stations.Select(station => station.Name));
    }
}
