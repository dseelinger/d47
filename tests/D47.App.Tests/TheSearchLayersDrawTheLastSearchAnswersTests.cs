using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;
using static D47.App.Tests.TheGalaxyMapDrawsWhereTheCommanderHasBeenTests;

namespace D47.App.Tests;

/// <summary>The search layers draw the last answer the search board has for their kind and ask galaxy search for nothing.</summary>
public class TheSearchLayersDrawTheLastSearchAnswersTests
{
    private static readonly StarPosition Wregoe = new(-40.6, 37.2, 75.3);

    private static readonly IReadOnlyDictionary<string, string> Asked = new Dictionary<string, string> { ["near"] = "Wregoe TC-X b29-0" };

    private static StarPosition Near(int index) => new(Wregoe.X + ((index % 5) * 9) - 18, Wregoe.Y, Wregoe.Z + ((index / 5) * 9) - 18);

    private static IReadOnlyList<GalaxyMapLayout.Mark> Marks(GalaxyMapPage page, GalaxyMapMarkKind kind)
    {
        var layout = (GalaxyMapLayout)typeof(GalaxyMapView).GetField("_layout", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page.Map())!;

        return [.. layout.Marks.Where(mark => mark.Kind == kind)];
    }

    /// <summary>A systems answer of <paramref name="rows"/> systems, the last <paramref name="unplaced"/> of them with no position.</summary>
    private static GalaxySearchPosting Systems(string prefix, int rows, int unplaced, int total, bool more = false) => new(
        GalaxySearchKind.Systems,
        Asked,
        "Wregoe TC-X b29-0",
        new GalaxySearchResult(
            "Wregoe TC-X b29-0",
            total,
            [.. Enumerable.Range(0, rows).Select(index => new SystemSummary { Name = $"{prefix} {index}", Position = index < rows - unplaced ? Near(index) : null })]),
        "Found them.",
        DateTimeOffset.UnixEpoch)
    {
        More = more,
    };

    private static GalaxySearchPosting Stations(params (string Station, string System, StarPosition? At)[] rows) => new(
        GalaxySearchKind.Stations,
        Asked,
        "Wregoe TC-X b29-0",
        new StationSearchResult(
            "Wregoe TC-X b29-0",
            rows.Length,
            [.. rows.Select(row => new StationSummary { Name = row.Station, SystemName = row.System, Position = row.At })]),
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
    public void TwentyFiveRowsOfAHundredAndFortyWithThreeUnplacedDrawTwentyTwoMarks()
    {
        var game = Played();
        var page = Opened(game, StarChartKind.SystemsSearch);

        game.Searches.Post(Systems("Answer", 25, 3, 140));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(22, Marks(page, GalaxyMapMarkKind.SystemResult).Count);
        Assert.Equal(22, Marks(page, GalaxyMapMarkKind.SystemResult).Select(mark => mark.Star.Name).Distinct().Count());
        Assert.True(page.Switch("Systems search").IsChecked);
        Assert.Contains("SYSTEM RESULT", page.LegendEntries());
        Assert.Equal(
            "3 system results have no position and are not drawn. The map has the first 25 of 140 system results; the Search tab's Systems page has the rest.",
            SearchLine(page));
    }

    [AvaloniaFact]
    public void PostingASecondAnswerReplacesTheMarks()
    {
        var game = Played();
        var page = Opened(game, StarChartKind.SystemsSearch);

        game.Searches.Post(Systems("First", 4, 0, 4));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(4, Marks(page, GalaxyMapMarkKind.SystemResult).Count);

        game.Searches.Post(Systems("Second", 2, 0, 2));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Second 0", "Second 1"], Marks(page, GalaxyMapMarkKind.SystemResult).Select(mark => mark.Star.Name).Order());
        Assert.Equal(string.Empty, SearchLine(page));
    }

    [AvaloniaFact]
    public void ALaterPageAddsItsMarks()
    {
        var game = Played();
        var page = Opened(game, StarChartKind.SystemsSearch);
        var first = Systems("Paged", 3, 0, 5, more: true);

        game.Searches.Post(first);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, Marks(page, GalaxyMapMarkKind.SystemResult).Count);
        Assert.Contains("first 3 of 5", SearchLine(page), StringComparison.Ordinal);

        var later = first with
        {
            Result = new GalaxySearchResult("Wregoe TC-X b29-0", 5, [.. Enumerable.Range(3, 2).Select(index => new SystemSummary { Name = $"Paged {index}", Position = Near(index) })]),
            Pages = 2,
            More = false,
        };

        Assert.True(game.Searches.Extend(later));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(5, Marks(page, GalaxyMapMarkKind.SystemResult).Count);
        Assert.Equal(string.Empty, SearchLine(page));
    }

    [AvaloniaFact]
    public void WithNoAnswerTheLayerDrawsNothingAndSaysSo()
    {
        var game = Played();
        var page = Opened(game, StarChartKind.StationsSearch);

        Assert.Empty(Marks(page, GalaxyMapMarkKind.StationResult));
        Assert.Equal("No stations search has been made yet.", SearchLine(page));
    }

    [AvaloniaFact]
    public void TheBigPanelSaysItInShortSentences()
    {
        var game = Played();
        game.Layers = SavedLayers();
        game.Searches.Post(Systems("Answer", 25, 3, 140));

        var (surface, _, page) = OnThePanel(game);
        page.ShowChart(StarChartKind.SystemsSearch);
        page.ShowChart(StarChartKind.BodiesSearch);
        surface.Render();

        Assert.Equal(22, Marks(page, GalaxyMapMarkKind.SystemResult).Count);
        Assert.Equal(
            "3 system results have no position. 25 of 140 system results; the Search tab has the rest. No bodies search yet.",
            SearchLine(page));
    }

    [AvaloniaFact]
    public void TheStripNamesTheKindAndTheStationsInTheSelectedSystem()
    {
        var game = Played();
        game.Searches.Post(Stations(("Ray Gateway", "Answer 1", Near(1)), ("Other Port", "Answer 1", Near(1)), ("Far Port", "Answer 2", Near(2))));

        var page = Opened(game, StarChartKind.StationsSearch);
        var mark = Marks(page, GalaxyMapMarkKind.StationResult).Single(drawn => drawn.Star.Name == "Answer 1");
        var picked = page.Map().TapAt(mark.At);

        Assert.Equal("Answer 1", picked?.Name);
        Assert.Equal(["STATION RESULTS: Ray Gateway and Other Port"], page.StripKinds());
    }

    [AvaloniaFact]
    public void TheMapAsksGalaxySearchForNothingForTheLayer()
    {
        var game = Played();
        game.GalaxyOffered = true;
        game.Layers = SavedLayers();
        game.Layers.Write(new GalaxyMapLayers([StarChartKind.SystemsSearch]));

        var (window, panel, page) = InWindow(game);

        game.Searches.Post(Systems("Unknown", 3, 2, 3));
        Dispatcher.UIThread.RunJobs();

        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        window.Content = panel;
        Dispatcher.UIThread.RunJobs();

        Assert.Single(Marks(page, GalaxyMapMarkKind.SystemResult));
        Assert.Equal("2 system results have no position and are not drawn.", SearchLine(page));
        Assert.Equal(0, game.Galaxy.Calls);
        Assert.All(["Unknown 1", "Unknown 2"], system => Assert.Equal(GalaxyMapLookup.NotAsked, game.Lookups.PositionOf(system).State));
    }
}
