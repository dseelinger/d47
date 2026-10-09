using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Loadout;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Fleet › Ships › Compare: every ship as last seen, ordered by jump or cargo and filtered by cargo (#562).</summary>
[Trait("Category", "Integration")]
public class TheFleetComparesByJumpOrCargoTests
{
    private sealed record Surface(Window Window, PanelView Panel, GameStateStore Store);

    private static readonly string[] Journal =
    [
        """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
        """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Sol","SystemAddress":10477373803,"StarPos":[0,0,0],"Docked":true,"StationName":"Abraham Lincoln"}""",
        """{"timestamp":"2026-08-18T09:01:00Z","event":"Loadout","Ship":"mandalay","ShipID":1,"ShipName":"Far Walker","ShipIdent":"FW-22","HullValue":200000000,"ModulesValue":88120000,"Rebuy":14406000,"UnladenMass":318.2,"CargoCapacity":32,"MaxJumpRange":71.8,"FuelCapacity":{"Main":32.0,"Reserve":0.5},"Modules":[]}""",
        """{"timestamp":"2026-08-18T09:02:00Z","event":"Loadout","Ship":"type9","ShipID":2,"ShipName":"Deep Hold","ShipIdent":"DH-09","HullValue":76555842,"ModulesValue":335884158,"Rebuy":20622000,"UnladenMass":1120.6,"CargoCapacity":400,"MaxJumpRange":18.4,"FuelCapacity":{"Main":64.0,"Reserve":0.77},"Modules":[]}""",
        """{"timestamp":"2026-08-18T09:03:00Z","event":"StoredShips","StationName":"Abraham Lincoln","MarketID":1,"StarSystem":"Sol","ShipsHere":[{"ShipID":3,"ShipType":"sidewinder","Name":"Spare","Value":32000,"Hot":false}],"ShipsRemote":[{"ShipID":1,"ShipType":"mandalay","Name":"Far Walker","StarSystem":"Colonia","ShipMarketID":2,"TransferPrice":1000,"TransferTime":3600,"Value":288120000,"Hot":false}]}""",
    ];

    private static Surface Open(IEnumerable<string> journal, bool drill = true)
    {
        var root = TempFolders.Create("d47-fleet-compare-tests");
        var store = new GameStateStore();

        foreach (var line in journal)
        {
            Apply(store, line);
        }

        CommanderGameState? State() => store.Active;

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(Path.Combine(root, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            State);
        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance), checklists, State);
        var kit = new OnFootPlanService(
            new OnFootBuildStore(Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance), checklists, State);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableLoadout(ships, checklists, State, kit);

        var window = new Window { Content = panel, Width = 1280, Height = 900 };
        window.Show();

        panel.Tab = PanelTab.Assets;
        Dispatcher.UIThread.RunJobs();
        panel.Nav.SelectRoot(LoadoutPages.FleetRoot);
        Dispatcher.UIThread.RunJobs();

        if (!drill)
        {
            return new Surface(window, panel, store);
        }

        var tile = panel.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "CompareShips");
        tile.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, store);
    }

    private static void Apply(GameStateStore store, string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        store.Apply(parsed!);
    }

    private static ComparePage Page(PanelView panel) =>
        panel.GetVisualDescendants().OfType<ComparePage>().Single();

    private static List<string> Text(Control page) =>
        [.. page.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    /// <summary>The ship names in the order the table draws them.</summary>
    private static List<string> Order(Control page) =>
        [.. Text(page).Where(text => text is "FAR WALKER" or "DEEP HOLD" or "SPARE")];

    [AvaloniaFact]
    public void TheCompareTileOnShipsDrillsToEveryShipByJumpRange()
    {
        var surface = Open(Journal);
        var page = Page(surface.Panel);
        var text = Text(page);

        Assert.Equal(ComparePage.Key, surface.Panel.Nav.Trail[^1].Key);
        Assert.Contains("COMPARE SHIPS", text);
        Assert.Contains("3 OF 3", text);
        Assert.Equal(["FAR WALKER", "DEEP HOLD", "SPARE"], Order(page));
        Assert.Contains("JUMP ▼", text);
        Assert.Contains("CARGO", text);
        Assert.Contains("71.80 LY", text);
        Assert.Contains("1120.6 T", text);
        Assert.Contains("412,440,000", text);
        Assert.Contains("20,622,000", text);
        Assert.Contains("SOL · ABRAHAM LINCOLN", text);
        Assert.Contains("COLONIA", text);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void CargoReordersAndAMinimumFilters()
    {
        var surface = Open(Journal);
        var page = Page(surface.Panel);

        page.Order(1);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["DEEP HOLD", "FAR WALKER", "SPARE"], Order(page));
        Assert.Contains("CARGO ▼", Text(page));

        page.Minimum(ComparePage.Tonnes.ToList().IndexOf(32));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["DEEP HOLD", "FAR WALKER"], Order(page));
        Assert.Contains("2 OF 3", Text(page));

        page.Minimum(ComparePage.Tonnes.ToList().IndexOf(512));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(Order(page));
        Assert.Contains(ComparePage.Filtered, Text(page));
        Assert.Contains("0 OF 3", Text(page));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void ShipsInTheCurrentSystemAreCyanAndAnUnseenShipIsDashes()
    {
        using var look = AppLook.Put();

        var surface = Open(Journal);
        var ships = ComparePage.Ships(surface.Store.Active);

        var aboard = ships.Single(ship => ship.Name == "Deep Hold");
        var parked = ships.Single(ship => ship.Name == "Spare");
        var away = ships.Single(ship => ship.Name == "Far Walker");

        Assert.True(aboard.InCurrentSystem);
        Assert.True(parked.InCurrentSystem);
        Assert.False(away.InCurrentSystem);
        Assert.Null(parked.Cargo);
        Assert.Null(parked.Jump);

        var cyan = Ink(Page(surface.Panel), "SPARE", "SOL · ABRAHAM LINCOLN");
        Assert.Equal(Brush(ThemeManager.CyanKey), cyan);
        Assert.Equal(Brush(ThemeManager.AKey), Ink(Page(surface.Panel), "FAR WALKER", "COLONIA"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void ANewLoadoutRedrawsTheOpenPage()
    {
        var surface = Open(Journal.Take(3));

        Assert.Equal(["FAR WALKER"], Order(Page(surface.Panel)));

        Apply(surface.Store, Journal[3]);
        Assert.True(surface.Panel.TickLoadout());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["FAR WALKER", "DEEP HOLD"], Order(Page(surface.Panel)));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void NoShipSeenSaysSo()
    {
        var surface = Open(Journal.Take(2));

        Assert.Contains(ComparePage.Unseen, Text(Page(surface.Panel)));
        Assert.Contains("0 OF 0", Text(Page(surface.Panel)));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheCompareShipsPageIsCapturedByJumpAndByCargo()
    {
        using var look = AppLook.Put();

        var surface = Open(Journal);

        foreach (var (order, minimum, name) in new[] { (0, 0, "by-jump"), (1, 3, "by-cargo-min64") })
        {
            Page(surface.Panel).Order(order);
            Page(surface.Panel).Minimum(minimum);
            Dispatcher.UIThread.RunJobs();

            var path = $"fleet-compare-{name}.png";

            using (var frame = surface.Window.CaptureRenderedFrame()!)
            {
                frame.SaveCapture(path);
            }
        }

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheShipsIndexIsCapturedWithItsCompareTile()
    {
        using var look = AppLook.Put();

        var surface = Open(Journal, drill: false);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(surface.Panel.GetVisualDescendants().OfType<Button>(), button => button.Name == "CompareShips");

        var path = "fleet-ships-compare-tile.png";

        using (var frame = surface.Window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture(path);
        }

        surface.Window.Close();
    }

    /// <summary>The ink of the Where cell in the row naming <paramref name="ship"/>.</summary>
    private static IBrush? Ink(ComparePage page, string ship, string where)
    {
        var row = page.GetVisualDescendants().OfType<Grid>()
            .Single(grid => grid.Children.OfType<StackPanel>()
                .Any(name => name.Children.OfType<TextBlock>().Any(block => block.Text == ship)));

        return row.Children.OfType<TextBlock>().Single(block => block.Text == where).Foreground;
    }

    private static IBrush? Brush(string key) =>
        Avalonia.Application.Current!.Resources.TryGetResource(key, null, out var brush) ? brush as IBrush : null;
}
