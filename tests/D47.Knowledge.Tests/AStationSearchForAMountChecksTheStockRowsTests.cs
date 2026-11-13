using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

public class AStationSearchForAMountChecksTheStockRowsTests
{
    private static OutfittingItem Item(string text)
    {
        Assert.True(OutfittingItem.TryParse(text, out var item, out var failure), failure);

        return item;
    }

    private static string Station(long market, string name, string modules) =>
        $$"""
        {"name":"{{name}}","system_name":"Sol","market_id":{{market}},"type":"Orbis Starport","distance":{{market}},
         "outfitting_updated_at":"2026-10-20 08:00:00+00","modules":[{{modules}}],"ships":[]}
        """;

    private static string Module(string name, int size, string rating, string? mode = null) =>
        mode is null
            ? $$"""{"name":"{{name}}","class":{{size}},"rating":"{{rating}}","ed_symbol":"x"}"""
            : $$"""{"name":"{{name}}","class":{{size}},"rating":"{{rating}}","ed_symbol":"x","weapon_mode":"{{mode}}"}""";

    private static string Cannon(string mode) => Module("Multi-Cannon", 2, "F", mode);

    private static string Answer(int count, params string[] stations) =>
        $$"""{"count":{{count}},"reference":{"name":"Sol"},"results":[{{string.Join(",", stations)}}]}""";

    /// <summary>A full page of stations, the first <paramref name="gimballed"/> of them listing the gimballed one.</summary>
    private static string Page(int first, int gimballed, int count) =>
        Answer(count, [.. Enumerable.Range(0, GalaxyQuery.MaxSize)
            .Select(i => Station(first + i, $"Port {first + i}", Cannon(i < gimballed ? "Gimbal" : "Turret")))]);

    private sealed class Spansh(params string[] answers) : HttpMessageHandler
    {
        private int _next;

        public List<JsonElement> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.Clone());

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(answers[_next++], Encoding.UTF8, "application/json"),
            };
        }
    }

    private static SpanshGalaxyService Service(Spansh handler) =>
        new(NullLogger<SpanshGalaxyService>.Instance, new HttpClient(handler) { BaseAddress = new Uri("https://spansh.co.uk/") });

    private static StationQuery Query(string items, int limit = 5, int page = 1)
    {
        Assert.True(
            StationQuery.TryParse(new OutfittingSearch { ReferenceSystem = "Sol", Items = items, Size = limit }, out var query, out var failure),
            failure);

        return query with { Page = page };
    }

    [Fact]
    public async Task AStationListingOnlyTheOtherMountIsLeftOut()
    {
        var handler = new Spansh(Answer(
            3,
            Station(1, "Turret Port", Cannon("Turret")),
            Station(2, "Galileo", Cannon("Turret") + "," + Cannon("Gimbal")),
            Station(3, "Bare Port", Module("Multi-Cannon", 2, "F"))));

        var result = await Service(handler).FindStationsAsync(Query("2F Multi-Cannon gimballed"), TestContext.Current.CancellationToken);

        Assert.Equal(["Galileo"], result.Stations.Select(station => station.Name));
        Assert.True(result.Uncounted);
        Assert.False(result.More);
        Assert.Equal("Sol", result.Reference);
        Assert.Equal(GalaxyQuery.MaxSize, Assert.Single(handler.Requests).GetProperty("size").GetInt32());
    }

    [Fact]
    public async Task FurtherPagesAreReadUntilTheLimitIsKept()
    {
        var handler = new Spansh(Page(0, 0, 400), Page(100, 3, 400), Page(200, 3, 400));

        var result = await Service(handler).FindStationsAsync(Query("2F Multi-Cannon gimballed"), TestContext.Current.CancellationToken);

        Assert.Equal([0, 1, 2], handler.Requests.Select(request => request.GetProperty("page").GetInt32()));
        Assert.Equal(["Port 100", "Port 101", "Port 102", "Port 200", "Port 201"], result.Stations.Select(station => station.Name));
        Assert.True(result.More);
    }

    [Fact]
    public async Task NoMoreThanThreeRequestsAreMade()
    {
        var handler = new Spansh(Page(0, 0, 400), Page(100, 1, 400), Page(200, 1, 400));

        var result = await Service(handler).FindStationsAsync(Query("2F Multi-Cannon gimballed"), TestContext.Current.CancellationToken);

        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(2, result.Stations.Count);
        Assert.False(result.More);
    }

    [Fact]
    public async Task ALaterPageShowsTheKeptStationsPastTheFirst()
    {
        var handler = new Spansh(Page(0, 7, 100));

        var result = await Service(handler).FindStationsAsync(Query("2F Multi-Cannon gimballed", page: 2), TestContext.Current.CancellationToken);

        Assert.Equal(["Port 5", "Port 6"], result.Stations.Select(station => station.Name));
        Assert.False(result.More);
    }

    [Fact]
    public async Task WithNoMountTheRequestAndCountAreUnchanged()
    {
        var handler = new Spansh(Answer(40, Station(1, "Turret Port", Cannon("Turret"))));

        var result = await Service(handler).FindStationsAsync(Query("2F Multi-Cannon"), TestContext.Current.CancellationToken);

        Assert.Equal(5, Assert.Single(handler.Requests).GetProperty("size").GetInt32());
        Assert.Equal(40, result.Total);
        Assert.False(result.Uncounted);
    }

    [Fact]
    public async Task AnOutfitterSearchMatchIsNotEnoughForACheckedMount()
    {
        var gimballed = Item("2F Multi-Cannon gimballed");
        var scoop = Item("6A Fuel Scoop");

        var handler = new Spansh(
            Answer(2, Station(1, "Turret Port", Cannon("Turret")), Station(2, "Galileo", Cannon("Gimbal"))),
            Answer(1, Station(1, "Turret Port", Module("Fuel Scoop", 6, "A"))));

        var found = await Service(handler).FindOutfittersAsync(
            [gimballed, scoop], "Sol", 30, null, null, TestContext.Current.CancellationToken);

        Assert.Equal([scoop], found.Single(row => row.Station.Name == "Turret Port").Sells);
        Assert.Equal([gimballed], found.Single(row => row.Station.Name == "Galileo").Sells);
    }

    [Fact]
    public async Task AModuleWithOneMountIsFoundWhereTheRowHasNone()
    {
        var launcher = Item("Heat Sink Launcher turreted");

        var handler = new Spansh(Answer(1, Station(1, "Galileo", Module("Heat Sink Launcher", 0, "I"))));

        var found = await Service(handler).FindOutfittersAsync(
            [launcher], "Sol", 30, null, null, TestContext.Current.CancellationToken);

        Assert.Equal([launcher], Assert.Single(found).Sells);
    }
}
