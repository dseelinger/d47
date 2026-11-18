using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Interface;
using D47.Core.Storage;
using D47.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The hull picker and the Switch page's positions table take the COLUMNS control and keep what it stores (#2527).</summary>
public class ThePickerAndSwitchTablesAreArrangedTests
{
    private const string PickId = "loadout.intend";

    private static readonly string[] HullColumns = ["Ship", "Size", "Jump", "Speed", "Boost", "Armour", "Shields", "Seats", "Price"];

    private static readonly string[] SwitchColumns = ["Mark", "Position", "Action", "Means", "Page"];

    private static readonly DateTimeOffset At = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private static ViewStateStore ViewState() =>
        new(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static T Named<T>(Visual root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    private static void Press(Visual root, string name)
    {
        var button = Named<Button>(root, name);
        Assert.True(button.IsEffectivelyEnabled, $"{name} is disabled.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static string[] Keys(Visual root, string id) =>
        [.. root.GetVisualDescendants().OfType<TableGrid>().First(grid => grid.Layout.Id == id && grid.IsHeader).Layout.Columns.Select(column => column.Key)];

    private static (PanelView Panel, ListBox Pick) AskForAHull(ViewStateStore views, bool headset)
    {
        var paths = new AppPaths(TestSurface.MemoryFolder("d47-picker-columns"));
        var checklists = TestSurface.Checklists(paths.Data);
        var ships = TestSurface.ShipPlans(checklists, paths.Data, () => null);
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, () => null, null);
        panel.EnableColumnArrangements(new ColumnArrangementMemory(views, headset));

        var look = AppLook.Put();
        var window = new Window { Content = panel, Width = 1600, Height = 1000 };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        panel.Tab = PanelTab.Assets;
        Dispatcher.UIThread.RunJobs();

        panel.GetVisualDescendants().OfType<Button>().First(button => button.Content as string == "Plan a ship you do not own")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        return (panel, panel.GetVisualDescendants().OfType<ListBox>().Single(box => AutomationProperties.GetName(box) == "Ship"));
    }

    private static List<string> Offered(ListBox pick) => pick.ItemsSource!.Cast<string>().ToList();

    [AvaloniaFact]
    public void WithNothingStoredThePickerDrawsEverySuppliedColumnInOrder()
    {
        var (panel, _) = AskForAHull(ViewState(), headset: false);

        Assert.Equal(HullColumns, Keys(panel, PickId));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMovedAndRemovedPickerColumnIsKeptWhenThePickerOpensAgain(bool headset)
    {
        var views = ViewState();
        var (first, _) = AskForAHull(views, headset);

        Press(first, ColumnArranger.NameOf(PickId, "Tile"));
        Press(first, "PickHead.Choose.Price");
        Press(first, ColumnArranger.NameOf(PickId, "MoveLeft"));
        Press(first, "PickHead.Choose.Seats");
        Press(first, ColumnArranger.NameOf(PickId, "Remove"));
        Press(first, ColumnArranger.NameOf(PickId, "Done"));

        string[] arranged = ["Ship", "Size", "Jump", "Speed", "Boost", "Armour", "Shields", "Price"];
        Assert.Equal(arranged, Keys(first, PickId));

        var (again, _) = AskForAHull(views, headset);
        Assert.Equal(arranged, Keys(again, PickId));

        Press(again, ColumnArranger.NameOf(PickId, "Tile"));
        Press(again, ColumnArranger.NameOf(PickId, "Default"));
        Assert.Equal(HullColumns, Keys(again, PickId));
    }

    [AvaloniaFact]
    public void TheHullPickerOffersNoColumnItWasNotGiven()
    {
        var (panel, _) = AskForAHull(ViewState(), headset: false);

        Press(panel, ColumnArranger.NameOf(PickId, "Tile"));
        Press(panel, ColumnArranger.NameOf(PickId, "Add"));

        var offered = panel.GetVisualDescendants().OfType<CheckBox>()
            .Where(box => box.Name?.StartsWith(ColumnArranger.NameOf(PickId, "Show."), StringComparison.Ordinal) == true)
            .Select(box => box.Name![ColumnArranger.NameOf(PickId, "Show.").Length..])
            .Order()
            .ToList();

        Assert.Equal(HullColumns.Order(), offered);
    }

    [AvaloniaFact]
    public void ASortChosenOnThePickerFollowsItsColumnWhenItMoves()
    {
        var (panel, pick) = AskForAHull(ViewState(), headset: false);

        Press(panel, "PickHead.Price");
        Assert.Equal("Sidewinder", Offered(pick)[0]);

        Press(panel, ColumnArranger.NameOf(PickId, "Tile"));
        Press(panel, "PickHead.Choose.Price");
        Press(panel, ColumnArranger.NameOf(PickId, "MoveLeft"));
        Press(panel, ColumnArranger.NameOf(PickId, "Done"));

        Assert.Equal("Price", Keys(panel, PickId)[7]);
        Assert.Equal("Sidewinder", Offered(pick)[0]);
        Assert.Equal("PRICE ▲", ((TextBlock)Named<Button>(panel, "PickHead.Price").Content!).Text);
    }

    [AvaloniaFact]
    public void ARemovedPickerColumnStillNarrowsTheListByItsText()
    {
        var (panel, pick) = AskForAHull(ViewState(), headset: false);

        Press(panel, ColumnArranger.NameOf(PickId, "Tile"));
        Press(panel, "PickHead.Choose.Size");
        Press(panel, ColumnArranger.NameOf(PickId, "Remove"));

        panel.GetVisualDescendants().OfType<TextBox>().Single(box => AutomationProperties.GetName(box) == "Filter").Text = "small";
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Sidewinder", Offered(pick));
        Assert.DoesNotContain("Anaconda", Offered(pick));
    }

    private static Window ShowSwitches(ViewStateStore views, bool headset)
    {
        var root = TestSurface.MemoryFolder("d47-switch-columns");
        var switches = new SwitchStore(Path.Combine(root, "switches.json"), new MemoryFileSystem(), NullLogger<SwitchStore>.Instance);

        SwitchMapping Mapping(string name, params SwitchPosition[] positions) => new()
        {
            Name = name,
            DeviceId = "stick-1",
            Device = "VID 0x4098 PID 0xBD65, 32 buttons, 0 hats, 0 axes",
            Positions = positions,
        };

        switches.Save(
        [
            Mapping("LDG GEAR", new SwitchPosition(9, "landing_gear", DesiredState.On), new SwitchPosition(8, "landing_gear", DesiredState.Off)),
            Mapping("HOOK", new SwitchPosition(3), new SwitchPosition(2)),
        ]);
        switches.Poll();

        var page = new SwitchPage(
            switches,
            new FakeHotasReader(),
            new SwitchReconciler(NullLogger<SwitchReconciler>.Instance),
            () => At,
            Path.Combine(root, "capture.txt"),
            [],
            columnArrangements: new ColumnArrangementMemory(views, headset));

        var look = AppLook.Put();
        var window = new Window
        {
            Content = page,
            Width = 1100,
            Height = 1400,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    [AvaloniaFact]
    public void WithNothingStoredTheSwitchPositionsDrawTheirFiveColumnsAndEveryLabelledHeaderSorts()
    {
        var window = ShowSwitches(ViewState(), headset: false);

        Assert.Equal(SwitchColumns, Keys(window, SwitchPage.PositionsTable));

        var heads = window.GetVisualDescendants().OfType<TableGrid>().Where(grid => grid.Layout.Id == SwitchPage.PositionsTable && grid.IsHeader).ToList();

        Assert.Equal(2, heads.Count);
        Assert.All(heads, head => Assert.Equal(4, head.GetVisualDescendants().OfType<Button>().Count()));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnArrangedSwitchTableIsKeptWhenThePageOpensAgain(bool headset)
    {
        var views = ViewState();
        var first = ShowSwitches(views, headset);
        var tile = ColumnArranger.NameOf(SwitchPage.PositionsTable, "Tile");

        Press(first, tile);
        Press(first, "SwitchHead.Choose.Means");
        Press(first, ColumnArranger.NameOf(SwitchPage.PositionsTable, "MoveLeft"));
        Press(first, "SwitchHead.Choose.Page");
        Press(first, ColumnArranger.NameOf(SwitchPage.PositionsTable, "Remove"));
        Press(first, ColumnArranger.NameOf(SwitchPage.PositionsTable, "Done"));

        string[] arranged = ["Mark", "Position", "Means", "Action"];
        Assert.Equal(arranged, Keys(first, SwitchPage.PositionsTable));

        var again = ShowSwitches(views, headset);
        Assert.Equal(arranged, Keys(again, SwitchPage.PositionsTable));

        Press(again, tile);
        Press(again, ColumnArranger.NameOf(SwitchPage.PositionsTable, "Add"));
        Dispatcher.UIThread.RunJobs();
        Named<CheckBox>(again, ColumnArranger.NameOf(SwitchPage.PositionsTable, "Show.Page")).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["Mark", "Position", "Means", "Action", "Page"], Keys(again, SwitchPage.PositionsTable));

        Press(again, ColumnArranger.NameOf(SwitchPage.PositionsTable, "Default"));
        Assert.Equal(SwitchColumns, Keys(again, SwitchPage.PositionsTable));
    }

    [AvaloniaFact]
    public void TheMarkColumnIsAControlColumnTheBarDoesNotOffer()
    {
        var window = ShowSwitches(ViewState(), headset: false);
        var tile = ColumnArranger.NameOf(SwitchPage.PositionsTable, "Tile");

        Press(window, tile);
        Press(window, ColumnArranger.NameOf(SwitchPage.PositionsTable, "Add"));

        Assert.DoesNotContain(
            window.GetVisualDescendants().OfType<CheckBox>(),
            box => box.Name == ColumnArranger.NameOf(SwitchPage.PositionsTable, "Show.Mark"));
    }
}
