using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>search_outfitting and find_signal take a page, and a later page joins the answer on the board (#1992).</summary>
public class OutfittingAndSignalAnswersContinueOnTheirNextPageTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly StarPosition Origin = new(0, 0, 0);

    private static SignalSighting Sighting(int n) =>
        new(n, $"System {n:000}", new StarPosition(n, 0, 0), "USS", "Nav Beacon", SignalKind.NavBeacon, Now);

    private static SignalSighting[] Sightings(int count) => [.. Enumerable.Range(1, count).Select(Sighting)];

    private static List<string> Systems(SignalSearchResult result) => [.. result.Signals.Select(finding => finding.System)];

    [Fact]
    public void PageThreeOfSixtySignalsAtTwentyFiveListsMatchesFiftyOneToSixty()
    {
        var result = SignalSearch.Find(Origin, null, _ => true, 1000, 25, Sightings(60), null, null, 3);

        Assert.Equal(60, result.Total);
        Assert.Equal([.. Enumerable.Range(51, 10).Select(n => $"System {n:000}")], Systems(result));
        Assert.False(result.More);
    }

    [Fact]
    public void PageOneOfSixtySignalsHasMore()
    {
        var result = SignalSearch.Find(Origin, null, _ => true, 1000, 25, Sightings(60), null);

        Assert.Equal(25, result.Signals.Count);
        Assert.True(result.More);
    }

    [Fact]
    public void AFullLastPageOfSignalsHasNoMore()
    {
        var result = SignalSearch.Find(Origin, null, _ => true, 1000, 25, Sightings(50), null, null, 2);

        Assert.Equal(25, result.Signals.Count);
        Assert.False(result.More);
    }

    [Fact]
    public void APagePastTheEndOfSignalsIsEmptyWithNoMore()
    {
        var result = SignalSearch.Find(Origin, null, _ => true, 1000, 25, Sightings(10), null, null, 4);

        Assert.Empty(result.Signals);
        Assert.False(result.More);
    }

    [Fact]
    public void ASignalPageJoinsTheHeldAnswerAndDropsASignalAlreadyHeld()
    {
        var board = new GalaxySearchBoard();
        var search = new Dictionary<string, string>(StringComparer.Ordinal) { ["signal"] = "Nav beacon" };

        FindingAt(1);

        GalaxySearchPosting Page(int page, params int[] systems) =>
            new(
                GalaxySearchKind.Signals,
                search,
                null,
                new SignalSearchResult(null, 3, [.. systems.Select(FindingAt)]),
                "said",
                Now) { Pages = page, More = page == 1 };

        board.Post(Page(1, 1, 2));

        Assert.True(board.Extend(Page(2, 2, 3)));

        var held = (SignalSearchResult)board.Last(GalaxySearchKind.Signals)!.Result;

        Assert.Equal(["System 1", "System 2", "System 3"], held.Signals.Select(finding => finding.System));
        Assert.Equal(2, board.Last(GalaxySearchKind.Signals)!.Pages);
        Assert.False(board.Last(GalaxySearchKind.Signals)!.More);

        static SignalFinding FindingAt(int n) => new("Nav Beacon", $"System {n}", n, Now, false);
    }

    private sealed class OutfittingGalaxy : StubGalaxyService, IGalaxyService
    {
        public List<StationQuery> Asked { get; } = [];

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            Asked.Add(query);

            // Page 2 repeats the last station of page 1, as a list that shifted between pages would.
            long[] markets = query.Page == 1 ? [1, 2] : [2, 3];

            return Task.FromResult(new StationSearchResult(
                "Sol",
                3,
                [.. markets.Select(market => new StationSummary { Name = $"Station {market}", SystemName = "Sol", MarketId = market })])
            {
                More = query.Page == 1,
            });
        }

        public Task<StarPosition?> PositionAsync(string system, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task OutfittingPageTwoAsksTheServicePageTwoAndExtendsTheAnswer()
    {
        var galaxy = new OutfittingGalaxy();
        var board = new GalaxySearchBoard();
        var settings = TestSurface.For(new MemoryInstall()).Settings;

        var registry = CapabilityRegistry.Build(
            [GalaxyCapability.Create(galaxy, () => "Sol", settings, now: () => Now, searches: board)]);

        async Task<ToolResult> Ask(params (string Name, string Value)[] values) =>
            await registry.InvokeAsync(
                "search_outfitting",
                new ToolArguments(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal)),
                TestContext.Current.CancellationToken);

        var first = await Ask(("items", "6A Fuel Scoop"));
        var second = await Ask(("items", "6A Fuel Scoop"), ("page", "2"));

        Assert.False(first.IsError, first.Content);
        Assert.False(second.IsError, second.Content);
        Assert.Equal([1, 2], galaxy.Asked.Select(query => query.Page));

        var last = board.Last(GalaxySearchKind.Outfitting)!;
        var stations = ((StationSearchResult)last.Result).Stations;

        Assert.Equal(["Station 1", "Station 2", "Station 3"], stations.Select(station => station.Name));
        Assert.Equal(2, last.Pages);
        Assert.False(last.More);
        Assert.False(last.Arguments.ContainsKey("page"));
    }
}
