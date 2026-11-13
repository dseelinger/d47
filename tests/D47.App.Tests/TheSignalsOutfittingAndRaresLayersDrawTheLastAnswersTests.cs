using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;
using static D47.App.Tests.TheGalaxyMapDrawsWhereTheCommanderHasBeenTests;

namespace D47.App.Tests;

/// <summary>
/// The signals, outfitting and rares layers draw the last answer the search board has for their kind: rows with a
/// position as they come, signals by the travel log and the position lookups, and no search made for any of them.
/// </summary>
public class TheSignalsOutfittingAndRaresLayersDrawTheLastAnswersTests
{
    private static readonly IReadOnlyDictionary<string, string> Asked = new Dictionary<string, string> { ["near"] = "Wregoe TC-X b29-0" };

    private static IReadOnlyList<GalaxyMapLayout.Mark> Marks(GalaxyMapPage page, GalaxyMapMarkKind kind)
    {
        var layout = (GalaxyMapLayout)typeof(GalaxyMapView).GetField("_layout", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page.Map())!;

        return [.. layout.Marks.Where(mark => mark.Kind == kind)];
    }

    private static RareListingRow Rare(string name, string system, double x) => new(
        new RareEntry
        {
            Symbol = name.ToLowerInvariant(),
            Name = name,
            System = system,
            Station = $"{name} Port",
            MarketId = 1,
            Position = new StarPosition(x, 10, 20),
        },
        null,
        x,
        null);

    private static GalaxySearchPosting Rares(params RareListingRow[] rows) => new(
        GalaxySearchKind.Rares,
        Asked,
        "Wregoe TC-X b29-0",
        new RareSearchResult(rows, true),
        "Found them.",
        DateTimeOffset.UnixEpoch);

    private static GalaxySearchPosting Outfitting(params (string Station, string System, StarPosition? At)[] rows) => new(
        GalaxySearchKind.Outfitting,
        Asked,
        "Wregoe TC-X b29-0",
        new StationSearchResult(
            "Wregoe TC-X b29-0",
            rows.Length,
            [.. rows.Select(row => new StationSummary { Name = row.Station, SystemName = row.System, Position = row.At })]),
        "Found them.",
        DateTimeOffset.UnixEpoch);

    private static GalaxySearchPosting Signals(params (string Signal, string System)[] rows) => new(
        GalaxySearchKind.Signals,
        Asked,
        "Wregoe TC-X b29-0",
        new SignalSearchResult(
            "Wregoe TC-X b29-0",
            rows.Length,
            [.. rows.Select(row => new SignalFinding(row.Signal, row.System, 10, DateTimeOffset.UnixEpoch, false))]),
        "Found them.",
        DateTimeOffset.UnixEpoch);

    private static GalaxyMapPage Opened(Game game, StarChartKind kind)
    {
        game.Layers ??= SavedLayers();
        var (_, _, page) = InWindow(game);

        page.ShowChart(kind);
        Dispatcher.UIThread.RunJobs();

        return page;
    }

    private static string SearchLine(GalaxyMapPage page) =>
        page.Lines().SingleOrDefault(line => line.Contains("result", StringComparison.Ordinal) || line.Contains("search", StringComparison.Ordinal)) ?? string.Empty;

    [AvaloniaFact]
    public void ARaresAnswerOfThreeRowsDrawsThreeMarks()
    {
        var game = Played();
        var page = Opened(game, StarChartKind.RaresSearch);

        game.Searches.Post(Rares(Rare("Alpha", "Rare A", 5), Rare("Beta", "Rare B", 15), Rare("Gamma", "Rare C", 25)));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, Marks(page, GalaxyMapMarkKind.RaresResult).Count);
        Assert.True(page.Switch("Rares search").IsChecked);
        Assert.Contains("RARES RESULT", page.LegendEntries());
        Assert.Equal(string.Empty, SearchLine(page));
    }

    [AvaloniaFact]
    public void AnOutfittingRowWithNoPositionIsCountedAsUnplaced()
    {
        var game = Played();
        var page = Opened(game, StarChartKind.OutfittingSearch);

        game.Searches.Post(Outfitting(("Ray Gateway", "Placed", new StarPosition(5, 5, 5)), ("Far Port", "Unplaced", null)));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Placed"], Marks(page, GalaxyMapMarkKind.OutfittingResult).Select(mark => mark.Star.Name));
        Assert.Contains("OUTFITTING RESULT", page.LegendEntries());
        Assert.Equal("1 outfitting result has no position and is not drawn.", SearchLine(page));
    }

    [AvaloniaFact]
    public void ASignalInTheTravelLogIsDrawnAndOneInNeitherTheLogNorTheLookupsIsCounted()
    {
        var game = Played();
        var page = Opened(game, StarChartKind.SignalsSearch);

        game.Searches.Post(Signals(("Resource Extraction Site", "Bubble 5"), ("Distress Call", "Nowhere Known")));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Bubble 5"], Marks(page, GalaxyMapMarkKind.SignalResult).Select(mark => mark.Star.Name));
        Assert.Contains("SIGNAL RESULT", page.LegendEntries());
        Assert.Equal("1 signal result has no position and is not drawn.", SearchLine(page));
    }

    [AvaloniaFact]
    public void ASignalInASystemTheLookupsKnowIsDrawn()
    {
        var game = Played();

        game.Layers = SavedLayers();
        game.Layers.Write(new GalaxyMapLayers([StarChartKind.SignalsSearch]));
        game.GalaxyOffered = true;
        game.Galaxy.Known["Nowhere Known"] = new StarPosition(-28.6, 37.2, 66.3);
        game.Searches.Post(Signals(("Distress Call", "Nowhere Known")));

        var (_, _, page) = InWindow(game);
        Dispatcher.UIThread.RunJobs();

        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (game.Lookups.PositionOf("Nowhere Known").State == GalaxyMapLookup.Waiting && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(20);
        }

        Dispatcher.UIThread.RunJobs();
        page.Tick();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(GalaxyMapLookup.Found, game.Lookups.PositionOf("Nowhere Known").State);
        Assert.Equal(["Nowhere Known"], Marks(page, GalaxyMapMarkKind.SignalResult).Select(mark => mark.Star.Name));
    }

    [AvaloniaFact]
    public void WithNoAnswerEachLayerDrawsNothingAndSaysSo()
    {
        var game = Played();

        Assert.Equal("No signals search has been made yet.", SearchLine(Opened(game, StarChartKind.SignalsSearch)));

        game = Played();
        Assert.Equal("No outfitting search has been made yet.", SearchLine(Opened(game, StarChartKind.OutfittingSearch)));

        game = Played();
        Assert.Equal("No rares search has been made yet.", SearchLine(Opened(game, StarChartKind.RaresSearch)));
    }

    [AvaloniaFact]
    public void ANewAnswerReplacesTheMarks()
    {
        var game = Played();
        var page = Opened(game, StarChartKind.RaresSearch);

        game.Searches.Post(Rares(Rare("Alpha", "Rare A", 5), Rare("Beta", "Rare B", 15)));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, Marks(page, GalaxyMapMarkKind.RaresResult).Count);

        game.Searches.Post(Rares(Rare("Gamma", "Rare C", 25)));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Rare C"], Marks(page, GalaxyMapMarkKind.RaresResult).Select(mark => mark.Star.Name));
    }

    [AvaloniaFact]
    public void TheMapAsksGalaxySearchForNothingForRaresOutfittingOrSignalsItCanPlace()
    {
        var game = Played();
        game.GalaxyOffered = true;
        game.Layers = SavedLayers();
        game.Layers.Write(new GalaxyMapLayers([StarChartKind.RaresSearch, StarChartKind.OutfittingSearch, StarChartKind.SignalsSearch]));

        game.Searches.Post(Rares(Rare("Alpha", "Rare A", 5), Rare("Beta", "Rare B", 15), Rare("Gamma", "Rare C", 25)));
        game.Searches.Post(Outfitting(("Ray Gateway", "Placed", new StarPosition(5, 5, 5)), ("Far Port", "Unplaced", null)));
        game.Searches.Post(Signals(("Resource Extraction Site", "Bubble 5")));

        var (_, _, page) = InWindow(game);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, Marks(page, GalaxyMapMarkKind.RaresResult).Count);
        Assert.Single(Marks(page, GalaxyMapMarkKind.OutfittingResult));
        Assert.Single(Marks(page, GalaxyMapMarkKind.SignalResult));
        Assert.Equal(0, game.Galaxy.Calls);
        Assert.Equal(GalaxyMapLookup.NotAsked, game.Lookups.PositionOf("Unplaced").State);
    }
}
