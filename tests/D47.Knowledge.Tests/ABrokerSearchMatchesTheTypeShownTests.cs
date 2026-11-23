using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>A technology broker search keeps the stations whose broker reads as the asked type (#2717).</summary>
public class ABrokerSearchMatchesTheTypeShownTests
{
    private const int InColonies = 9480;

    private const int LabelledHumanElsewhere = 149;

    /// <summary>A station with the Technology Broker service, labelled <paramref name="label"/> by the index.</summary>
    private static string Station(int id, bool colonised, string? label, string extra = "") =>
        $$"""
        {"name":"Port {{id}}","system_name":"System {{id}}","market_id":{{id}},"type":"Coriolis Starport",
         "distance":{{id}},"system_is_colonised":{{(colonised ? "true" : "false")}},
         {{(label is null ? "" : $"\"technology_broker\":\"{label}\",")}}
         "services":[{"name":"Technology Broker"}]{{extra}}}
        """;

    private static string Answer(int count, IEnumerable<string> stations) =>
        $$"""{"count":{{count}},"reference":{"name":"Sol"},"results":[{{string.Join(",", stations)}}]}""";

    /// <summary>A full read of stations labelled Guardian, those <paramref name="colony"/> picks in colonised systems.</summary>
    private static string Read(int first, Func<int, bool> colony) =>
        Answer(500, Enumerable.Range(first, GalaxyQuery.MaxSize).Select(i => Station(i, colony(i), "Guardian")));

    /// <summary>Answers a <c>size</c> 1 request with a count chosen by its body, and each other request from <c>reads</c> in turn.</summary>
    private sealed class Spansh(params string[] reads) : HttpMessageHandler
    {
        private int _next;

        public List<JsonElement> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.Clone();
            Requests.Add(body);

            var filters = body.GetProperty("filters");
            var answer = body.GetProperty("size").GetInt32() != 1
                ? reads[_next++]
                : filters.TryGetProperty("technology_broker", out _)
                ? Answer(LabelledHumanElsewhere, [])
                : Answer(InColonies, []);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(answer, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static SpanshGalaxyService Service(Spansh handler) =>
        new(NullLogger<SpanshGalaxyService>.Instance, new HttpClient(handler) { BaseAddress = new Uri("https://spansh.co.uk/") });

    private static StationQuery Query(StationSearch search)
    {
        Assert.True(StationQuery.TryParse(search with { ReferenceSystem = "Sol" }, out var query, out var failure), failure);

        return query;
    }

    private static JsonElement Filters(JsonElement request) => request.GetProperty("filters");

    private static IEnumerable<string> ServiceNames(JsonElement filters) =>
        filters.GetProperty("services").EnumerateArray()
            .Select(service => service.GetProperty("name").GetProperty("value")[0].GetString()!);

    [Fact]
    public async Task AHumanSearchReadsTheServiceAndKeepsOnlyRowsThatReadHuman()
    {
        var handler = new Spansh(Answer(
            4,
            [
                Station(1, colonised: true, "Guardian"),
                Station(2, colonised: false, "Guardian"),
                Station(3, colonised: false, "Human"),
                Station(4, colonised: true, null),
            ]));

        var result = await Service(handler).FindStationsAsync(
            Query(new StationSearch { TechnologyBroker = "human" }),
            TestContext.Current.CancellationToken);

        Assert.Equal(["Port 1", "Port 3", "Port 4"], result.Stations.Select(station => station.Name));

        var read = Filters(handler.Requests[0]);
        Assert.Equal(GalaxyQuery.MaxSize, handler.Requests[0].GetProperty("size").GetInt32());
        Assert.False(read.TryGetProperty("technology_broker", out _));
        Assert.False(read.TryGetProperty("system_is_colonised", out _));
        Assert.Equal(["Technology Broker"], ServiceNames(read));
    }

    [Fact]
    public async Task AHumanSearchTotalIsTheTwoCountsSummed()
    {
        var handler = new Spansh(Answer(1, [Station(1, colonised: true, null)]));

        var result = await Service(handler).FindStationsAsync(
            Query(new StationSearch { TechnologyBroker = "Human", MinPad = "Large" }),
            TestContext.Current.CancellationToken);

        Assert.Equal(InColonies + LabelledHumanElsewhere, result.Total);
        Assert.False(result.Uncounted);

        var counts = handler.Requests.Where(request => request.GetProperty("size").GetInt32() == 1).Select(Filters).ToList();
        Assert.Equal(2, counts.Count);
        Assert.All(counts, filters => Assert.Equal("true", filters.GetProperty("has_large_pad").GetProperty("value")[0].GetString()));

        var colonies = Assert.Single(counts, filters => !filters.TryGetProperty("technology_broker", out _));
        Assert.Equal(JsonValueKind.True, colonies.GetProperty("system_is_colonised").GetProperty("value").ValueKind);
        Assert.Equal(["Technology Broker"], ServiceNames(colonies));

        var labelled = Assert.Single(counts, filters => filters.TryGetProperty("technology_broker", out _));
        Assert.Equal("Human", labelled.GetProperty("technology_broker").GetProperty("value")[0].GetString());
        Assert.Equal(JsonValueKind.False, labelled.GetProperty("system_is_colonised").GetProperty("value").ValueKind);
    }

    [Fact]
    public async Task AHumanSearchFillsItsPageAcrossReads()
    {
        var handler = new Spansh(Read(0, i => i >= 98), Read(100, i => i < 103));

        var result = await Service(handler).FindStationsAsync(
            Query(new StationSearch { TechnologyBroker = "Human", Size = 5 }),
            TestContext.Current.CancellationToken);

        Assert.Equal(["Port 98", "Port 99", "Port 100", "Port 101", "Port 102"], result.Stations.Select(station => station.Name));
        Assert.Equal(
            [0, 1],
            handler.Requests.Where(request => request.GetProperty("size").GetInt32() != 1)
                .Select(request => request.GetProperty("page").GetInt32()));
    }

    [Fact]
    public async Task AHumanSearchThatAlreadyNamesTheServiceSendsItOnce()
    {
        var handler = new Spansh(Answer(0, []));

        await Service(handler).FindStationsAsync(
            Query(new StationSearch { TechnologyBroker = "Human", Services = "Technology Broker, Refuel" }),
            TestContext.Current.CancellationToken);

        Assert.Equal(["Technology Broker", "Refuel"], ServiceNames(Filters(handler.Requests[0])));
    }

    [Fact]
    public async Task AHumanSearchInColonisedSystemsIsOneRequestAndSpanshsCount()
    {
        var handler = new Spansh(Answer(812, [Station(1, colonised: true, "Guardian")]));

        var result = await Service(handler).FindStationsAsync(
            Query(new StationSearch
            {
                TechnologyBroker = "Human",
                Filters = new Dictionary<string, string> { ["colonised"] = "true" },
            }),
            TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        var filters = Filters(request);
        Assert.False(filters.TryGetProperty("technology_broker", out _));
        Assert.Equal(["Technology Broker"], ServiceNames(filters));
        Assert.Equal("true", filters.GetProperty("system_is_colonised").GetProperty("value")[0].GetString());
        Assert.Equal(812, result.Total);
        Assert.False(result.Uncounted);
        Assert.Equal(["Port 1"], result.Stations.Select(station => station.Name));
    }

    [Fact]
    public async Task AGuardianSearchIsOneRequestAndSpanshsCount()
    {
        var handler = new Spansh(Answer(178, [Station(1, colonised: false, "Guardian")]));

        var result = await Service(handler).FindStationsAsync(
            Query(new StationSearch { TechnologyBroker = "Guardian" }),
            TestContext.Current.CancellationToken);

        var filters = Filters(Assert.Single(handler.Requests));
        Assert.Equal("Guardian", filters.GetProperty("technology_broker").GetProperty("value")[0].GetString());
        Assert.Equal(JsonValueKind.False, filters.GetProperty("system_is_colonised").GetProperty("value").ValueKind);
        Assert.Equal(178, result.Total);
        Assert.False(result.Uncounted);
    }

    [Fact]
    public async Task AHumanSearchWithACheckedMountAppliesBothChecksAndStaysUncounted()
    {
        const string gimballed = """{"name":"Multi-Cannon","class":2,"rating":"F","ed_symbol":"x","weapon_mode":"Gimbal"}""";
        const string turreted = """{"name":"Multi-Cannon","class":2,"rating":"F","ed_symbol":"x","weapon_mode":"Turret"}""";

        var handler = new Spansh(Answer(
            3,
            [
                Station(1, colonised: true, null, $",\"modules\":[{gimballed}]"),
                Station(2, colonised: true, null, $",\"modules\":[{turreted}]"),
                Station(3, colonised: false, "Guardian", $",\"modules\":[{gimballed}]"),
            ]));

        var result = await Service(handler).FindStationsAsync(
            Query(new StationSearch
            {
                TechnologyBroker = "Human", Module = "Multi-Cannon", ModuleClass = "2", ModuleRating = "F", ModuleMount = "gimballed",
            }),
            TestContext.Current.CancellationToken);

        Assert.Equal(["Port 1"], result.Stations.Select(station => station.Name));
        Assert.True(result.Uncounted);
        Assert.False(Filters(Assert.Single(handler.Requests)).TryGetProperty("technology_broker", out _));
    }
}
