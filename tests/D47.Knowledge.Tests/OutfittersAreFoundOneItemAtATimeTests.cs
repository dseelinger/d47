using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

public class OutfittersAreFoundOneItemAtATimeTests
{
    private static OutfittingItem Item(string text)
    {
        Assert.True(OutfittingItem.TryParse(text, out var item, out var failure), failure);

        return item;
    }

    private static string Station(long market, string name, double distance, string modules, string ships = "", string type = "Orbis Starport") =>
        $$"""
        {"name":"{{name}}","system_name":"Sol","market_id":{{market}},"type":"{{type}}","distance":{{distance}},
         "outfitting_updated_at":"2026-10-20 08:00:00+00","shipyard_updated_at":"2026-10-21 09:30:00+00",
         "modules":[{{modules}}],"ships":[{{ships}}]}
        """;

    private static string Module(string name, int size, string rating) =>
        $$"""{"name":"{{name}}","class":{{size}},"rating":"{{rating}}","ed_symbol":"x","category":"Core Internal"}""";

    private static string Answer(params string[] stations) =>
        $$"""{"count":{{stations.Length}},"reference":{"name":"Sol"},"results":[{{string.Join(",", stations)}}]}""";

    private sealed class Spansh(params string[] answers) : HttpMessageHandler
    {
        private int _next;

        public List<string> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(answers[_next++], Encoding.UTF8, "application/json"),
            };
        }
    }

    private static SpanshGalaxyService Service(Spansh handler) =>
        new(NullLogger<SpanshGalaxyService>.Instance, new HttpClient(handler) { BaseAddress = new Uri("https://spansh.co.uk/") });

    [Fact]
    public async Task EachItemIsOneSearchAndAStationSellingTwoIsOneRow()
    {
        var drive = Item("5A Frame Shift Drive");
        var scoop = Item("2D Fuel Scoop");

        var handler = new Spansh(
            Answer(
                Station(1, "Galileo", 0.4, Module("Frame Shift Drive", 5, "A") + "," + Module("Fuel Scoop", 2, "D")),
                Station(2, "Far Port", 9.0, Module("Frame Shift Drive", 5, "A"))),
            Answer(
                Station(1, "Galileo", 0.4, Module("Frame Shift Drive", 5, "A") + "," + Module("Fuel Scoop", 2, "D")),
                Station(3, "Near Port", 2.0, Module("Fuel Scoop", 2, "D"))));

        var found = await Service(handler).FindOutfittersAsync(
            [drive, scoop], "Sol", 30, PadSize.Large, null, TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(["Galileo", "Near Port", "Far Port"], found.Select(row => row.Station.Name));
        Assert.Equal([drive, scoop], found[0].Sells);
        Assert.Equal([scoop], found[1].Sells);
        Assert.Equal([drive], found[2].Sells);
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T08:00:00Z"), found[0].ReportedAt);
    }

    [Fact]
    public async Task AStationListingTheWrongRatingDoesNotSellTheOtherItem()
    {
        var drive = Item("5A Frame Shift Drive");
        var scoop = Item("2D Fuel Scoop");

        var handler = new Spansh(
            Answer(Station(1, "Galileo", 0.4, Module("Frame Shift Drive", 5, "A") + "," + Module("Fuel Scoop", 2, "C"))),
            Answer());

        var found = await Service(handler).FindOutfittersAsync(
            [drive, scoop], "Sol", 30, null, null, TestContext.Current.CancellationToken);

        Assert.Equal([drive], Assert.Single(found).Sells);
    }

    [Fact]
    public async Task AHullIsMatchedAgainstTheShipsTheStationSells()
    {
        var python = Item("Python");
        var drive = Item("Frame Shift Drive");

        var handler = new Spansh(
            Answer(Station(1, "Galileo", 0.4, Module("Frame Shift Drive", 4, "B"), "\"Python\",\"Cobra MkIII\"")),
            Answer());

        var found = await Service(handler).FindOutfittersAsync(
            [python, drive], "Sol", 30, null, null, TestContext.Current.CancellationToken);

        Assert.Equal([python, drive], Assert.Single(found).Sells);
    }

    [Fact]
    public async Task AFleetCarrierTheServiceReturnsAnywayIsLeftOut()
    {
        var handler = new Spansh(
            Answer(Station(1, "Carrier", 0.1, Module("Fuel Scoop", 2, "D"), type: "Drake-Class Carrier")));

        var found = await Service(handler).FindOutfittersAsync(
            [Item("2D Fuel Scoop")], "Sol", 30, null, null, TestContext.Current.CancellationToken);

        Assert.Empty(found);
    }

    [Fact]
    public void AModuleIsRequestedByNameSizeAndRatingWithAtMostTwentyFiveStations()
    {
        var body = JsonDocument.Parse(SpanshRequest.Stations(
            StationQuery.Selling(Item("5A Frame Shift Drive"), "Sol", 40, PadSize.Large, null))).RootElement;
        var filters = body.GetProperty("filters");
        var module = filters.GetProperty("modules");

        Assert.Equal("Sol", body.GetProperty("reference_system").GetString());
        Assert.Equal(25, body.GetProperty("size").GetInt32());
        Assert.Equal("40", filters.GetProperty("distance").GetProperty("max").GetString());
        Assert.Equal("Frame Shift Drive", module.GetProperty("name").GetProperty("value")[0].GetString());
        Assert.Equal("5", module.GetProperty("class").GetProperty("value")[0].GetString());
        Assert.Equal("A", module.GetProperty("rating").GetProperty("value")[0].GetString());
        Assert.False(filters.TryGetProperty("ships", out _));
        Assert.True(filters.TryGetProperty("has_large_pad", out _));
        Assert.DoesNotContain(
            filters.GetProperty("type").GetProperty("value").EnumerateArray(),
            type => type.GetString() == "Drake-Class Carrier");
    }

    [Fact]
    public void AHullIsRequestedAsAShipFilterAndNoModule()
    {
        var filters = JsonDocument.Parse(SpanshRequest.Stations(
            StationQuery.Selling(Item("Python"), "Sol", 40, null, null))).RootElement.GetProperty("filters");

        Assert.Equal("Python", filters.GetProperty("ships").GetProperty("value")[0].GetString());
        Assert.False(filters.TryGetProperty("modules", out _));
    }

    [Fact]
    public void AModuleSearchWithAnAgeFiltersOnWhenItsOutfittingWasReported()
    {
        var now = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
        var filters = JsonDocument.Parse(SpanshRequest.Stations(
            StationQuery.Selling(Item("5A Frame Shift Drive"), "Sol", 40, null, 72), now)).RootElement.GetProperty("filters");

        Assert.Equal("2026-10-27T12:00:00", filters.GetProperty("outfitting_updated_at").GetProperty("value")[0].GetString());
        Assert.False(filters.TryGetProperty("shipyard_updated_at", out _));
    }

    [Fact]
    public void AHullSearchWithAnAgeFiltersOnWhenItsShipyardWasReported()
    {
        var now = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
        var filters = JsonDocument.Parse(SpanshRequest.Stations(
            StationQuery.Selling(Item("Python"), "Sol", 40, null, 72), now)).RootElement.GetProperty("filters");

        Assert.Equal("2026-10-27T12:00:00", filters.GetProperty("shipyard_updated_at").GetProperty("value")[0].GetString());
        Assert.False(filters.TryGetProperty("outfitting_updated_at", out _));
    }

    [Fact]
    public void ASearchWithNoAgeSendsNoReportDateFilter()
    {
        var filters = JsonDocument.Parse(SpanshRequest.Stations(
            StationQuery.Selling(Item("5A Frame Shift Drive"), "Sol", 40, null, null))).RootElement.GetProperty("filters");

        Assert.False(filters.TryGetProperty("outfitting_updated_at", out _));
    }

    [Fact]
    public async Task EveryItemsQueryCarriesTheAge()
    {
        var drive = Item("5A Frame Shift Drive");
        var scoop = Item("2D Fuel Scoop");
        var handler = new Spansh(Answer(), Answer());

        await Service(handler).FindOutfittersAsync([drive, scoop], "Sol", 30, null, 48, TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Contains("outfitting_updated_at", request));
    }
}
