using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Knowledge;
using Xunit;
using static D47.App.Tests.TheGalaxyMapDrawsWhereTheCommanderHasBeenTests;

namespace D47.App.Tests;

/// <summary>The Galaxy map's LAYERS tile opens a picker that chooses which chart kinds are on the map and in the rail (#2352).</summary>
public class TheGalaxyMapsLayersAreChosenOnAPickerTests
{
    private static Button LayersTile(Control page) =>
        page.GetVisualDescendants().OfType<Button>().Single(button => (AutomationProperties.GetName(button) ?? string.Empty).StartsWith("LAYERS · ", StringComparison.Ordinal));

    private static IReadOnlyList<string> RailTitles(GalaxyMapPage page) =>
        [.. page.GetVisualDescendants().OfType<CheckBox>().Select(AutomationProperties.GetName).OfType<string>()];

    private static GalaxyMapPage Shown(PanelView panel) => panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single();

    private static PickerPage OpenPicker(PanelView panel)
    {
        LayersTile(Shown(panel)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        return panel.GetVisualDescendants().OfType<PickerPage>().Single();
    }

    /// <summary>Opens the picker, sets the box for <paramref name="title"/> and takes it.</summary>
    private static void Pick(PanelView panel, string title, bool ticked)
    {
        var picker = OpenPicker(panel);

        picker.GetVisualDescendants().OfType<CheckBox>().Single(box => AutomationProperties.GetName(box) == title).IsChecked = ticked;
        Dispatcher.UIThread.RunJobs();
        picker.GetControl<Button>("AcceptButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheTileCountsEightWithNothingSavedAndEveryRailRowHasASwatch()
    {
        var game = Played();
        game.Layers = SavedLayers();
        var (_, _, page) = InWindow(game);

        Assert.Equal("LAYERS · 8", AutomationProperties.GetName(LayersTile(page)));
        Assert.Equal(StarCharts.Default.Select(kind => StarCharts.Info(kind).Title), RailTitles(page));
        Assert.All(page.GetVisualDescendants().OfType<CheckBox>(), box => Assert.Single(box.GetVisualDescendants().OfType<Glyph>()));
    }

    [AvaloniaFact]
    public void ThePickerListsEveryKindByGroupWithTheOnesOnTheMapTicked()
    {
        var game = Played();
        game.Layers = SavedLayers();
        var (_, panel, _) = InWindow(game);

        var picker = OpenPicker(panel);
        var boxes = picker.GetVisualDescendants().OfType<CheckBox>().ToList();
        var words = picker.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();

        Assert.Equal("Layers", panel.Nav.Trail[^1].Word);
        Assert.Contains(GalaxyMapPage.LayersHelp, words);
        Assert.Contains("YOURS", words);
        Assert.Contains("GALAXY", words);
        Assert.Contains("GUARDIAN", words);
        Assert.Contains("LAST SEARCH", words);
        Assert.Equal(18, boxes.Count);
        Assert.All(boxes, box => Assert.Equal(AutomationProperties.GetName(box) is not ("Guardian beacons" or "Farming sites" or "Missions" or "Deaths" or "Systems search" or "Stations search" or "Bodies search" or "Signals search" or "Outfitting search" or "Rares search"), box.IsChecked));
        Assert.Equal(18, picker.GetVisualDescendants().OfType<Glyph>().Count());
        Assert.False(picker.GetControl<Button>("DefaultButton").IsVisible);
    }

    [AvaloniaFact]
    public void RemovingEngineersTakesItOffTheRailAndTheMapAndAddingItBackTicksIt()
    {
        var game = Keeping();
        game.Layers = SavedLayers();
        var (_, panel, _) = InWindow(game);
        var engineer = BubbleEngineer();

        Pick(panel, "Engineers", ticked: false);

        var page = Shown(panel);
        page.Map().View(GalaxyViewport.Around(engineer.Position!.Value));

        Assert.Equal("LAYERS · 7", AutomationProperties.GetName(LayersTile(page)));
        Assert.DoesNotContain("Engineers", RailTitles(page));
        Assert.DoesNotContain(MarkedAt(page, engineer.System!, engineer.Position!.Value), word => word.StartsWith("ENGINEER", StringComparison.Ordinal));
        Assert.DoesNotContain(StarChartKind.Engineers, game.Layers.Read().OnMap);

        Pick(panel, "Engineers", ticked: true);

        page = Shown(panel);
        page.Map().View(GalaxyViewport.Around(engineer.Position!.Value));

        Assert.Equal("LAYERS · 8", AutomationProperties.GetName(LayersTile(page)));
        Assert.Equal(StarCharts.Default.Select(kind => StarCharts.Info(kind).Title), RailTitles(page));
        Assert.True(page.Switch("Engineers").IsChecked);
        Assert.Contains(MarkedAt(page, engineer.System!, engineer.Position!.Value), word => word.StartsWith("ENGINEER", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void AnUntickedLayerTakenOffAndPutBackComesInTicked()
    {
        var game = Played();
        game.Layers = SavedLayers();
        var (_, panel, first) = InWindow(game);

        first.Switch("Bookmarks").IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        Pick(panel, "Bookmarks", ticked: false);
        Pick(panel, "Bookmarks", ticked: true);

        Assert.True(Shown(panel).Switch("Bookmarks").IsChecked);
        Assert.Empty(game.Layers.Read().Unticked);
    }

    [AvaloniaFact]
    public void TheChoiceIsKeptByTheNextPageBuilt()
    {
        var game = Played();
        game.Layers = SavedLayers();
        var (_, panel, _) = InWindow(game);

        Pick(panel, "Engineers", ticked: false);

        var (_, _, second) = InWindow(game);

        Assert.Equal("LAYERS · 7", AutomationProperties.GetName(LayersTile(second)));
        Assert.DoesNotContain("Engineers", RailTitles(second));
    }

    [AvaloniaFact]
    public void TheBigPanelHasTheTileThePickerAndTheRailWithThreeHundredThirtyPixelSegments()
    {
        var game = Played();
        game.Layers = SavedLayers();
        var (surface, panel, page) = OnThePanel(game);

        Assert.Equal(330, page.Framing().Bounds.Width);
        Assert.Equal(330, page.Range().Bounds.Width);
        Assert.Equal(152, LayersTile(page).Bounds.Width);
        Assert.Equal(StarCharts.Default.Select(kind => StarCharts.Info(kind).Title), RailTitles(page));

        Assert.True(Tap(surface, panel, LayersTile(page)));
        var picker = panel.GetVisualDescendants().OfType<PickerPage>().Single();
        Assert.Equal(18, picker.GetVisualDescendants().OfType<CheckBox>().Count());
    }
}
