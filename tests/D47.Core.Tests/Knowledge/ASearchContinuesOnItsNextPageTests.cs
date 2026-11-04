using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// A later page of search_systems, search_stations or find_body adds its rows to the answer on the board
/// rather than replacing it, and the search history keeps the search once.
/// </summary>
public class ASearchContinuesOnItsNextPageTests
{
    private static readonly DateTimeOffset AskedAt = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Answers each page with that page's rows, and records the queries it was given.</summary>
    private sealed class PagedGalaxy : StubGalaxyService, IGalaxyService
    {
        public List<GalaxyQuery> Asked { get; } = [];

        public int Total { get; set; } = 1234;

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            Asked.Add(query);
            var first = (query.Page - 1) * query.Size;

            return Task.FromResult(new GalaxySearchResult(
                "Sol",
                Total,
                [.. Enumerable.Range(first + 1, query.Size).Select(row => new SystemSummary { Name = $"System {row}", Distance = row })])
            {
                More = GalaxyQuery.HasMore(query.Page, query.Size, query.Size, Total),
            });
        }

        public StationQuery? LastStationQuery { get; private set; }

        public StationSearchResult Stations { get; set; } = new("Sol", 0, []);

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            LastStationQuery = query;
            return Task.FromResult(Stations);
        }

        public BodySearchResult Bodies { get; set; } = new("Sol", 0, []);

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(Bodies);

        public Task<D47.Core.Journal.StarPosition?> PositionAsync(string system, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<OutfitterStation>> FindOutfittersAsync(
            IReadOnlyList<OutfittingItem> items,
            string referenceSystem,
            double maxDistance,
            PadSize? minPad,
            int? maxReportAgeHours,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed record Rig(
        CapabilityRegistry Registry, PagedGalaxy Galaxy, GalaxySearchBoard Board, SearchHistory History)
    {
        public int Posted { get; set; }

        public int Extended { get; set; }
    }

    private static Rig Build()
    {
        var galaxy = new PagedGalaxy();
        var board = new GalaxySearchBoard();
        var history = new SearchHistory(
            Path.Combine(@"C:\d47-test", "search-history.json"),
            new MemoryFileSystem(),
            NullLogger<SearchHistory>.Instance);
        var settings = TestSurface.For(new MemoryInstall()).Settings;


        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(galaxy, () => "Sol", settings, now: () => AskedAt, searches: board, history: history),
        ]);

        var rig = new Rig(registry, galaxy, board, history);
        board.Posted += _ => rig.Posted++;
        board.Extended += _ => rig.Extended++;
        return rig;
    }

    private static Task<ToolResult> Ask(Rig rig, string tool, params (string Name, string Value)[] values) =>
        rig.Registry.InvokeAsync(
            tool,
            new ToolArguments(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

    private static StationSummary Station(string name, long market, string? power = null) =>
        new()
        {
            Name = name,
            SystemName = "Sol",
            MarketId = market,
            ControllingPower = power,
            PowerState = power is null ? null : "Stronghold",
        };

    [Fact]
    public async Task SystemsPageTwoExtendsTheAnswerOnTheBoard()
    {
        var rig = Build();

        await Ask(rig, "search_systems", ("allegiance", "Federation"));
        var second = await Ask(rig, "search_systems", ("allegiance", "Federation"), ("page", "2"));

        Assert.False(second.IsError, second.Content);
        Assert.StartsWith("Systems 26 to 50 of 1234", second.Content, StringComparison.Ordinal);

        var last = rig.Board.Last(GalaxySearchKind.Systems)!;
        var systems = ((GalaxySearchResult)last.Result).Systems;

        Assert.Equal(50, systems.Count);
        Assert.Equal("System 1", systems[0].Name);
        Assert.Equal("System 50", systems[^1].Name);
        Assert.Equal(2, last.Pages);
        Assert.True(last.More);
        Assert.False(last.Arguments.ContainsKey("page"));
        Assert.Equal(1, rig.Posted);
        Assert.Equal(1, rig.Extended);
    }

    [Fact]
    public async Task PagesOneAndTwoOfOneSearchLeaveOneHistoryEntry()
    {
        var rig = Build();

        await Ask(rig, "search_systems", ("allegiance", "Federation"));
        await Ask(rig, "search_systems", ("allegiance", "Federation"), ("page", "2"));

        var entry = Assert.Single(rig.History.All());
        Assert.False(entry.Arguments.ContainsKey("page"));
    }

    [Fact]
    public async Task APageTwoOfAnotherSearchLeavesTheBoardUnchanged()
    {
        var rig = Build();

        await Ask(rig, "search_systems", ("allegiance", "Federation"));
        var first = rig.Board.Last(GalaxySearchKind.Systems);

        await Ask(rig, "search_systems", ("allegiance", "Federation"), ("order_by", "name"), ("page", "2"));

        Assert.Same(first, rig.Board.Last(GalaxySearchKind.Systems));
        Assert.Equal(0, rig.Extended);
    }

    [Fact]
    public async Task ThePageAskedForReachesTheQuery()
    {
        var rig = Build();

        await Ask(rig, "search_systems", ("limit", "25"), ("page", "3"));

        Assert.Equal(3, rig.Galaxy.Asked[^1].Page);
        Assert.Equal(25, rig.Galaxy.Asked[^1].Size);
    }

    [Fact]
    public async Task ALaterPageThatIsTheLastSaysSo()
    {
        var rig = Build();
        rig.Galaxy.Total = 50;

        await Ask(rig, "search_systems");
        var second = await Ask(rig, "search_systems", ("page", "2"));

        Assert.EndsWith("That is the last of them.", second.Content, StringComparison.Ordinal);
        Assert.False(rig.Board.Last(GalaxySearchKind.Systems)!.More);
    }

    [Fact]
    public async Task ABodiesByMaterialAnswerHasNoMoreAndRefusesPageTwo()
    {
        var rig = Build();
        rig.Galaxy.Bodies = new BodySearchResult(
            "Sol",
            400,
            [.. Enumerable.Range(1, BodyQuery.RichestOf).Select(row => new BodySummary { Name = $"Body {row}", SystemName = "Sol" })])
        {
            More = true,
        };

        var first = await Ask(rig, "find_body", ("material", "Polonium"), ("order_by", "material"));

        Assert.False(first.IsError, first.Content);
        Assert.False(((BodySearchResult)rig.Board.Last(GalaxySearchKind.Bodies)!.Result).More);
        Assert.False(rig.Board.Last(GalaxySearchKind.Bodies)!.More);

        var second = await Ask(rig, "find_body", ("material", "Polonium"), ("order_by", "material"), ("page", "2"));

        Assert.True(second.IsError);
        Assert.Contains($"nearest {BodyQuery.RichestOf}", second.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStationsPageTwoAddsOnlyStationsNotAlreadyShown()
    {
        var rig = Build();
        rig.Galaxy.Stations = new StationSearchResult("Sol", 60, [Station("Abraham Lincoln", 1), Station("Daedalus", 2)]) { More = true };

        await Ask(rig, "search_stations", ("services", "Shipyard"), ("limit", "2"));

        rig.Galaxy.Stations = new StationSearchResult("Sol", 60, [Station("Daedalus", 2), Station("Mars High", 3)]) { More = true };
        var second = await Ask(rig, "search_stations", ("services", "Shipyard"), ("limit", "2"), ("page", "2"));

        Assert.Equal(2, rig.Galaxy.LastStationQuery!.Page);
        Assert.StartsWith("Stations 3 to 4 of 60", second.Content, StringComparison.Ordinal);
        Assert.Equal(
            ["Abraham Lincoln", "Daedalus", "Mars High"],
            ((StationSearchResult)rig.Board.Last(GalaxySearchKind.Stations)!.Result).Stations.Select(station => station.Name));
    }

    [Fact]
    public async Task AStationsPageTwoDropsAnotherPowersStrongholdCarrierAndKeepsTheRest()
    {
        var inner = new PagedGalaxy
        {
            Stations = new StationSearchResult(
                "Sol",
                60,
                [Station("Mars High", 3), Station("Stronghold Carrier", 4, "Aisling Duval"), Station("Galileo", 5)])
            {
                More = true,
            },
        };

        var query = Parsed(5) with { Page = 2 };
        var result = await new StationPowerFilter(inner, () => D47.Core.Journal.PowerplayPledge.Unpledged)
            .FindStationsAsync(query, TestContext.Current.CancellationToken);

        Assert.Equal(["Mars High", "Galileo"], result.Stations.Select(station => station.Name));
        Assert.Equal(1, result.LeftOut);
        Assert.True(result.More);
        Assert.Equal(5, inner.LastStationQuery!.Size);
        Assert.Equal(2, inner.LastStationQuery.Page);
    }

    [Fact]
    public async Task PageOneHasMoreWhenItStoppedBeforeTheEndOfWhatItWasGiven()
    {
        var inner = new PagedGalaxy
        {
            Stations = new StationSearchResult("Sol", 3, [Station("Mars High", 3), Station("Galileo", 5), Station("Daedalus", 2)]),
        };

        var filter = new StationPowerFilter(inner, () => D47.Core.Journal.PowerplayPledge.Unpledged);

        var stopped = await filter.FindStationsAsync(Parsed(2), TestContext.Current.CancellationToken);
        var whole = await filter.FindStationsAsync(Parsed(3), TestContext.Current.CancellationToken);

        Assert.True(stopped.More);
        Assert.False(whole.More);
    }

    private static StationQuery Parsed(int size)
    {
        Assert.True(StationQuery.TryParse(
            new StationSearch { ReferenceSystem = "Sol", Services = "Shipyard", Size = size },
            out var query,
            out var failure), failure);

        return query;
    }

    [Fact]
    public void AServicePageShorterThanItsSizeIsTheLast()
    {
        Assert.True(GalaxyQuery.HasMore(1, 25, 25, 1234));
        Assert.False(GalaxyQuery.HasMore(2, 25, 24, 1234));
        Assert.False(GalaxyQuery.HasMore(2, 25, 25, 50));
        Assert.True(GalaxyQuery.HasMore(400, 25, 25, GalaxyQuery.CountCeiling));
    }
}
