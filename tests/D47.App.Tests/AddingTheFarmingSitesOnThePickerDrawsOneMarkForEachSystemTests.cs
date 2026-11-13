using System.Reflection;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Knowledge;
using Xunit;
using static D47.App.Tests.TheGalaxyMapDrawsWhereTheCommanderHasBeenTests;

namespace D47.App.Tests;

/// <summary>The Farming sites layer is added on the Layers picker or by the star chart tool and draws a mark for each system in the farming table.</summary>
public class AddingTheFarmingSitesOnThePickerDrawsOneMarkForEachSystemTests
{
    private const string Farming = "Farming sites";

    private static readonly IReadOnlyList<StarChartSystem> Systems = StarCharts.Systems(StarChartKind.FarmingSites);

    private static GalaxyMapPage Shown(Control panel) => panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single();

    private static IReadOnlyList<GalaxyMapLayout.Mark> FarmingMarks(GalaxyMapPage page)
    {
        var layout = (GalaxyMapLayout)typeof(GalaxyMapView).GetField("_layout", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page.Map())!;

        return [.. layout.Marks.Where(mark => mark.Kind == GalaxyMapMarkKind.FarmingSite)];
    }

    private static void AddOnThePicker(PanelView panel)
    {
        var tile = Shown(panel).GetVisualDescendants().OfType<Button>().Single(button => (AutomationProperties.GetName(button) ?? string.Empty).StartsWith("LAYERS · ", StringComparison.Ordinal));
        tile.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var picker = panel.GetVisualDescendants().OfType<D47.App.Controls.PickerPage>().Single();
        picker.GetVisualDescendants().OfType<CheckBox>().Single(box => AutomationProperties.GetName(box) == Farming).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        picker.GetControl<Button>("AcceptButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void AddingTheLayerOnThePickerDrawsOneMarkForEachSystemAtEveryZoom()
    {
        var game = Played();
        game.Layers = SavedLayers();
        var (_, panel, _) = InWindow(game);

        Assert.DoesNotContain(StarChartKind.FarmingSites, game.Layers.Read().OnMap);

        AddOnThePicker(panel);

        var page = Shown(panel);
        page.Map().View(GalaxyViewport.Fitting([.. Systems.Select(system => system.At)])!);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Systems.Count, FarmingMarks(page).Count);
        Assert.Equal(Systems.Select(system => system.Name).Order(StringComparer.Ordinal), FarmingMarks(page).Select(mark => mark.Star.Name).Order(StringComparer.Ordinal));
        Assert.True(page.Switch(Farming).IsChecked);
        Assert.Contains("FARMING SITE", page.LegendEntries());

        page.Map().View(GalaxyViewport.WholeGalaxy);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Systems.Count, FarmingMarks(page).Count);
    }

    [AvaloniaFact]
    public void TheStripNamesTheKindAndListsEachSiteInTheSelectedSystem()
    {
        var game = Played();
        game.Layers = SavedLayers();
        var (_, panel, _) = InWindow(game);
        var routing = new PanelRouting(() => false, () => []);
        routing.RouteNavigation(panel.Nav, action => action(), leads: true, openChart: panel.OpenChart);

        routing.Open(PageRef.StarChart(StarChartKind.FarmingSites));
        Dispatcher.UIThread.RunJobs();

        var page = Shown(panel);
        var system = Systems.First(candidate => FarmingSites.All.Count(site => site.System == candidate.Name) > 1);
        var picked = page.Map().TapAt(FarmingMarks(page).Single(mark => mark.Star.Name == system.Name).At);

        Assert.Equal(system.Name, picked?.Name);

        var kinds = page.StripKinds();
        var sites = FarmingSites.All.Where(site => site.System == system.Name).ToList();

        Assert.Equal(sites.Count, kinds.Count);
        Assert.All(kinds, kind => Assert.StartsWith("FARMING SITE", kind, StringComparison.Ordinal));
        Assert.All(sites, site => Assert.Contains(kinds, kind => kind.Contains(site.Label, StringComparison.Ordinal)));
    }
}
