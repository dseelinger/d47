using System.Globalization;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.App.Tests.TheGalaxyMapDrawsWhereTheCommanderHasBeenTests;

namespace D47.App.Tests;

/// <summary>The Deaths layer draws a mark for each system the Commander died in, and follows the travel log.</summary>
public class TheDeathsLayerDrawsAMarkForEachSystemYouDiedInTests
{
    private static readonly DateTimeOffset Origin = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero).AddDays(-600);

    private static string Died(DateTimeOffset at) =>
        "{\"timestamp\":\"" + at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) + "\",\"event\":\"Died\",\"KillerName\":\"Pirate\"}";

    private const string Load = "{\"timestamp\":\"2025-01-01T09:00:00Z\",\"event\":\"LoadGame\",\"FID\":\"F1\",\"Commander\":\"Kestrel Vane\"}";

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

        return parsed!;
    }

    private static GalaxyMapPage Shown(Control panel) => panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single();

    private static IReadOnlyList<GalaxyMapLayout.Mark> DeathMarks(GalaxyMapPage page)
    {
        var layout = (GalaxyMapLayout)typeof(GalaxyMapView).GetField("_layout", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page.Map())!;

        return [.. layout.Marks.Where(mark => mark.Kind == GalaxyMapMarkKind.Death)];
    }

    private static Game WithDeaths()
    {
        var game = Played();

        game.Travel.Fold([Load, Died(Origin.AddHours(16)), Died(Origin.AddHours(17)), Died(Origin.AddHours(30))]);

        return game;
    }

    private static GalaxyMapPage Opened(Game game)
    {
        game.Layers = SavedLayers();
        var (_, panel, _) = InWindow(game);
        var routing = new PanelRouting(() => false, () => []);
        routing.RouteNavigation(panel.Nav, action => action(), leads: true, openChart: panel.OpenChart);

        routing.Open(PageRef.StarChart(StarChartKind.Deaths));
        Dispatcher.UIThread.RunJobs();

        return Shown(panel);
    }

    [AvaloniaFact]
    public void ADeathIsMarkedInTheSystemOfTheArrivalBeforeItAndNamedInTheLegend()
    {
        var page = Opened(WithDeaths());

        Assert.Equal(["Bubble 10", "Bubble 5"], DeathMarks(page).Select(mark => mark.Star.Name).Order(StringComparer.Ordinal));
        Assert.True(page.Switch("Deaths").IsChecked);
        Assert.Contains("DIED HERE", page.LegendEntries());
    }

    [AvaloniaFact]
    public void TheStripSaysHowManyTimesWhereThereWasMoreThanOneDeath()
    {
        var page = Opened(WithDeaths());
        var twice = DeathMarks(page).Single(mark => mark.Star.Name == "Bubble 5");
        var once = DeathMarks(page).Single(mark => mark.Star.Name == "Bubble 10");

        Assert.Equal("Bubble 5", page.Map().TapAt(twice.At)?.Name);
        Assert.Equal(["DIED HERE: 2 times"], page.StripKinds());

        Assert.Equal("Bubble 10", page.Map().TapAt(once.At)?.Name);
        Assert.Equal(["DIED HERE: "], page.StripKinds());
    }

    [AvaloniaFact]
    public void ALiveDeathAddsItsMarkOnTheNextTick()
    {
        var game = Played();
        var page = Opened(game);

        Assert.Empty(DeathMarks(page));

        game.Travel.Apply([Event(Load), Event(Died(new DateTimeOffset(2026, 10, 21, 12, 30, 0, TimeSpan.Zero)))]);

        Assert.True(page.Tick());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Wregoe TC-X b29-0"], DeathMarks(page).Select(mark => mark.Star.Name));
    }

    [AvaloniaFact]
    public void ADeathBeforeAnyArrivalDrawsNoMark()
    {
        var game = Played();
        game.Travel.Fold([Load, Died(Origin.AddDays(-5))]);

        Assert.Empty(DeathMarks(Opened(game)));
    }
}
