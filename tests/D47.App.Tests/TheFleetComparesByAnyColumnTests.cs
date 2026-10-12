using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
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

/// <summary>Fleet › Ships › Compare: every ship as last seen, sorted from any column header and filtered by cargo (#562, #1112).</summary>
public class TheFleetComparesByAnyColumnTests
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
        var store = new GameStateStore();

        foreach (var line in journal)
        {
            Apply(store, line);
        }

        var panel = Panel(store, TestSurface.MemoryFolder("d47-fleet-compare-tests"));

        var window = new Window { Content = panel, Width = 1280, Height = 900 };
        window.Show();

        panel.Tab = PanelTab.Assets;
        Dispatcher.UIThread.RunJobs();
        panel.Nav.SelectRoot(LoadoutPages.FleetRoot);
        Dispatcher.UIThread.RunJobs();

        if (drill)
        {
            Drill(panel);
        }

        return new Surface(window, panel, store);
    }

    private static PanelView Panel(GameStateStore store, string root)
    {
        CommanderGameState? State() => store.Active;

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(Path.Combine(root, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            State);
        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance), checklists, State);
        var kit = new OnFootPlanService(
            new OnFootBuildStore(Path.Combine(root, "on-foot.json"), new MemoryFileSystem(), NullLogger<OnFootBuildStore>.Instance), checklists, State);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableLoadout(ships, checklists, State, kit);
        return panel;
    }

    private static void Drill(PanelView panel)
    {
        var tile = panel.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "CompareShips");
        tile.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The header cell for <paramref name="label"/>, as last drawn.</summary>
    private static Button Head(ComparePage page, string label) =>
        page.GetVisualDescendants().OfType<Button>().Single(button => button.Name == $"CompareHead.{label}");

    private static void Press(PanelView panel, string label)
    {
        Head(Page(panel), label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static Point Centre(Control control, Visual within) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), within)!.Value;

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

        Press(surface.Panel, "Cargo");

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
    public void ValueSortsMostValuableFirstThenCheapestThenBackToJump()
    {
        var surface = Open(Journal);
        var page = Page(surface.Panel);

        Assert.DoesNotContain("ORDER BY", Text(page));
        Assert.Empty(page.GetVisualDescendants().OfType<Segment>());

        Press(surface.Panel, "Value");

        Assert.Equal(["DEEP HOLD", "FAR WALKER", "SPARE"], Order(page));
        Assert.Contains("VALUE ▼", Text(page));
        Assert.Contains("JUMP", Text(page));
        Assert.DoesNotContain("JUMP ▼", Text(page));

        Press(surface.Panel, "Value");

        // Spare has no Value figure, so it stays last.
        Assert.Equal(["FAR WALKER", "DEEP HOLD", "SPARE"], Order(page));
        Assert.Contains("VALUE ▲", Text(page));

        Press(surface.Panel, "Value");

        Assert.Equal(["FAR WALKER", "DEEP HOLD", "SPARE"], Order(page));
        Assert.Contains("JUMP ▼", Text(page));
        Assert.Contains("VALUE", Text(page));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void PressingJumpFromTheDefaultSortsItInItsFirstDirection()
    {
        var surface = Open(Journal);
        var page = Page(surface.Panel);

        Press(surface.Panel, "Jump");

        Assert.Equal(["FAR WALKER", "DEEP HOLD", "SPARE"], Order(page));
        Assert.Contains("JUMP ▼", Text(page));

        Press(surface.Panel, "Jump");

        Assert.Equal(["DEEP HOLD", "FAR WALKER", "SPARE"], Order(page));
        Assert.Contains("JUMP ▲", Text(page));

        Press(surface.Panel, "Jump");

        Assert.Equal(["FAR WALKER", "DEEP HOLD", "SPARE"], Order(page));
        Assert.Contains("JUMP ▼", Text(page));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheSortLastsUntilThePageIsOpenedAgain()
    {
        var surface = Open(Journal);

        Press(surface.Panel, "Value");
        Assert.Contains("VALUE ▼", Text(Page(surface.Panel)));

        Assert.True(surface.Panel.Nav.Back());
        Dispatcher.UIThread.RunJobs();
        Drill(surface.Panel);

        Assert.Contains("JUMP ▼", Text(Page(surface.Panel)));
        Assert.Equal(["FAR WALKER", "DEEP HOLD", "SPARE"], Order(Page(surface.Panel)));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void InTheHeadsetTheHeadersAreFortyFourTallAndPressToSort()
    {
        var store = new GameStateStore();

        foreach (var line in Journal)
        {
            Apply(store, line);
        }

        var panel = Panel(store, TestSurface.MemoryFolder("d47-fleet-compare-headset"));
        panel.Classes.Add("headset");

        using var surface = new OffscreenSurface(panel, new PixelSize(1280, 900));

        panel.Tab = PanelTab.Assets;
        surface.Render();
        Dispatcher.UIThread.RunJobs();
        panel.Nav.SelectRoot(LoadoutPages.FleetRoot);
        Dispatcher.UIThread.RunJobs();
        surface.Render();

        var tile = panel.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "CompareShips");
        Assert.True(surface.Click(Centre(tile, panel)), "the ray's press on the Compare tile was refused.");
        Dispatcher.UIThread.RunJobs();
        surface.Render();

        var page = Page(panel);

        foreach (var head in ComparePage.Heads)
        {
            var height = Head(page, head.Label).Bounds.Height;
            Assert.True(height >= TypeScale.MinimumTarget, $"the {head.Label} header is {height} tall.");
        }

        Assert.True(surface.Click(Centre(Head(page, "Value"), panel)), "the ray's press on the Value header was refused.");
        Dispatcher.UIThread.RunJobs();
        surface.Render();

        Assert.Equal(["DEEP HOLD", "FAR WALKER", "SPARE"], Order(page));
        Assert.Contains("VALUE ▼", Text(page));
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
    public void TheCompareShipsPageIsCapturedByJumpByCargoAndByValue()
    {
        using var look = AppLook.Put();

        var surface = Open(Journal);

        foreach (var (press, minimum, name) in new[] { ((string?)null, 0, "by-jump"), ("Cargo", 3, "by-cargo-min64"), ("Value", 0, "by-value") })
        {
            if (press is not null)
            {
                Press(surface.Panel, press);
            }

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
