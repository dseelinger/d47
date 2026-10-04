using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Fleet › Materials › Farming route: site cards nearest first, filtered by kind (#561).</summary>
public class TheFarmingRouteStartsNearestTests
{
    private sealed record Surface(Window Window, PanelView Panel, GameStateStore Store);

    private const string AtHip36601 =
        """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"HIP 36601","SystemAddress":1,"StarPos":[337.8125,562.96875,-1457.84375],"Docked":false}""";

    private const string AtSol =
        """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Sol","SystemAddress":10477373803,"StarPos":[0,0,0],"Docked":false}""";

    private static Surface Open(string location)
    {
        var root = TempFolders.Create("d47-farming-route-tests");
        var store = new GameStateStore();

        Apply(store, """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""");
        Apply(store, location);

        CommanderGameState? State() => store.Active;

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(Path.Combine(root, "checklist-proposals.json"), NullLogger<ChecklistProposalStore>.Instance),
            State);
        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance), checklists, State);
        var kit = new OnFootPlanService(
            new OnFootBuildStore(Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance), checklists, State);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableCopy(new D47.Core.Capabilities.Builtin.RecordingClipboard());
        panel.EnableLoadout(ships, checklists, State, kit);

        var window = new Window { Content = panel, Width = 1280, Height = 1150 };
        window.Show();

        panel.Tab = PanelTab.Assets;
        Dispatcher.UIThread.RunJobs();
        Assert.True(panel.Nav.SelectRoot(LoadoutPages.GapRoot));
        Dispatcher.UIThread.RunJobs();

        Page(panel).Select(MaterialsPage.FarmingView);
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, store);
    }

    private static void Apply(GameStateStore store, string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        store.Apply(parsed!);
    }

    private static MaterialsPage Page(PanelView panel) =>
        panel.GetVisualDescendants().OfType<MaterialsPage>().Single();

    private static List<string> Text(Control page) =>
        [.. page.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    /// <summary>The material names the cards show, top to bottom.</summary>
    private static List<string> Cards(Control page, FarmingRoute route)
    {
        var names = route.Stops.Select(stop => stop.Material.ToUpperInvariant()).ToHashSet();
        return [.. Text(page).Where(names.Contains)];
    }

    [AvaloniaFact]
    public void TheSidebarCarriesTheFarmingRouteWithItsSiteCount()
    {
        var surface = Open(AtSol);

        var sidebar = Page(surface.Panel).GetVisualDescendants().OfType<Sidebar>().Single();
        var text = Text(sidebar);

        Assert.Contains("FARMING", text, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("FARMING ROUTE", text, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(MaterialsPage.Count(FarmingRoute.For(null).Stops.Count), text);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void SitesAreListedNearestFirstFromTheCommander()
    {
        var surface = Open(AtSol);
        var page = Page(surface.Panel);
        var route = FarmingRoute.For(surface.Store.Active);

        Assert.Equal([.. route.Stops.Select(stop => stop.Material.ToUpperInvariant())], Cards(page, route));
        Assert.Contains(MaterialsPage.FarmingHead, Text(page));
        Assert.Contains(MaterialsPage.Distance(route.Stops[0])!, Text(page));
        Assert.DoesNotContain(MaterialsPage.Here, Text(page));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheCurrentSystemReadsHere()
    {
        var surface = Open(AtHip36601);
        var page = Page(surface.Panel);
        var route = FarmingRoute.For(surface.Store.Active);

        Assert.True(route.Stops[0].IsHere);
        Assert.Contains(MaterialsPage.Here, Text(page));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheSegmentNarrowsTheListToOneKind()
    {
        var surface = Open(AtSol);
        var page = Page(surface.Panel);

        page.Filter(1);
        Dispatcher.UIThread.RunJobs();

        var raw = FarmingRoute.For(surface.Store.Active, "Raw");
        var all = FarmingRoute.For(surface.Store.Active);

        Assert.NotEmpty(raw.Stops);
        Assert.All(raw.Stops, stop => Assert.Equal("Raw", stop.Kind));
        Assert.Equal([.. raw.Stops.Select(stop => stop.Material.ToUpperInvariant())], Cards(page, all));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheRouteRedrawsWhenTheSystemChanges()
    {
        var surface = Open(AtSol);
        var page = Page(surface.Panel);

        Apply(surface.Store, AtHip36601);
        page.Refresh();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(MaterialsPage.Here, Text(page));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void EverySystemNameCarriesACopyGlyphLevelWithIt()
    {
        var surface = Open(AtSol);
        var page = Page(surface.Panel);
        var route = FarmingRoute.For(surface.Store.Active);

        var glyphs = page.GetVisualDescendants().OfType<Button>()
            .Where(button => CopyGlyph.GetCopies(button) is not null)
            .ToList();

        Assert.Equal([.. route.Stops.Select(stop => stop.System)], glyphs.Select(CopyGlyph.GetCopies));

        foreach (var glyph in glyphs)
        {
            var line = (StackPanel)glyph.GetVisualParent()!;
            var system = line.Children.OfType<TextBlock>().First();
            var systemMiddle = system.TranslatePoint(new Point(0, system.Bounds.Height / 2), page)!.Value.Y;
            var glyphMiddle = glyph.TranslatePoint(new Point(0, glyph.Bounds.Height / 2), page)!.Value.Y;

            Assert.InRange(Math.Abs(systemMiddle - glyphMiddle), 0, 1);
        }

        surface.Window.Close();
    }

    [Fact]
    public void ATradeDownNamesBothGrades()
    {
        var polonium = FarmingRoute.For(null).Stops.Single(stop => stop.Site.MaterialSymbol == "polonium");

        Assert.StartsWith("1 × G4 raw › 3 × G3", MaterialsPage.TradeDownText(polonium), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheRouteIsCapturedAllAndRaw()
    {
        using var look = AppLook.Put();

        var surface = Open(AtHip36601);

        foreach (var (index, name) in new[] { (0, "all"), (1, "raw") })
        {
            Page(surface.Panel).Filter(index);
            Dispatcher.UIThread.RunJobs();

            var path = Path.Combine(TestSurface.CaptureDirectory, $"materials-farming-route-{name}.png");

            using (var frame = surface.Window.CaptureRenderedFrame()!)
            {
                frame.Save(path, new PngBitmapEncoderOptions());
            }

            Assert.True(File.Exists(path));
        }

        surface.Window.Close();
    }
}
