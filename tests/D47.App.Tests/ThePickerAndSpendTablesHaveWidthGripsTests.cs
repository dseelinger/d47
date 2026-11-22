using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using D47.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The hull picker's table and both Spend breakdown tables have width grips on the desktop and none in the headset (#2574).</summary>
[Trait("Category", "Integration")]
public sealed class ThePickerAndSpendTablesHaveWidthGripsTests
{
    private const string PickId = "loadout.intend";

    private static readonly DateTimeOffset Noon = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    private static ViewStateStore Views() =>
        new(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static TableGrid Header(Visual root, string id) =>
        root.GetVisualDescendants().OfType<TableGrid>().First(grid => grid.Layout.Id == id && grid.IsHeader);

    private static double[] Widths(TableGrid grid) => [.. grid.ColumnDefinitions.Select(definition => definition.ActualWidth)];

    private static void Drag(Window window, ColumnGrip grip, double by)
    {
        var start = grip.TranslatePoint(new Point(grip.Bounds.Width / 2, grip.Bounds.Height / 2), window)!.Value;

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Point(by, 0));
        Dispatcher.UIThread.RunJobs();
        window.MouseUp(start + new Point(by, 0), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static (PanelView Panel, Window Window) AskForAHull(ViewStateStore views, bool headset = false)
    {
        var paths = new AppPaths(TestSurface.MemoryFolder("d47-picker-grips"));
        var checklists = TestSurface.Checklists(paths.Data);
        var ships = TestSurface.ShipPlans(checklists, paths.Data, () => null);
        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableLoadout(ships, checklists, () => null, null);
        panel.EnableColumnArrangements(new ColumnArrangementMemory(views, headset));
        panel.EnableResizableColumns(new ColumnWidthMemory(views));

        var look = AppLook.Put();
        var window = new Window { Content = panel, Width = 1600, Height = 1000 };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        panel.Nav.Show(PanelTabNames.AssetsRoot);
        Dispatcher.UIThread.RunJobs();

        panel.GetVisualDescendants().OfType<Button>().First(button => button.Content as string == "Plan a ship you do not own")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }

    [AvaloniaFact]
    public void AWidthDraggedOnThePickerTableIsThereWhenThePickerOpensAgain()
    {
        var views = Views();
        var (panel, window) = AskForAHull(views);
        var header = Header(panel, PickId);
        var before = Widths(header);

        Drag(window, header.Children.OfType<ColumnGrip>().First(), 40);

        var dragged = Widths(Header(panel, PickId));

        Assert.NotEqual(before, dragged);
        window.Close();

        var (again, second) = AskForAHull(views);

        Assert.Equal(dragged, Widths(Header(again, PickId)));
        second.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetPickerTableHasNoGrips()
    {
        var (panel, window) = AskForAHull(Views(), headset: true);

        Assert.Empty(Header(panel, PickId).Children.OfType<ColumnGrip>());
        window.Close();
    }

    private static (PanelView Panel, Window Window) OpenSpend(bool headset = false)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableResizableColumns(new ColumnWidthMemory(Views()));

        var window = new Window { Content = panel, Width = 1600, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        _ = panel.Open(new SpendBreakdownPage(SpendBreakdownOpensFromSpendTests.Charged("d47-spend-grips", Noon), TimeZoneInfo.Utc));
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }

    private static void ShowEveryCharge(PanelView panel)
    {
        var options = panel.GetVisualDescendants().OfType<Segment>().First(segment => segment.Name == "SpendSection")
            .GetVisualDescendants().OfType<RadioButton>().ToList();
        options[SpendBreakdownModel.EveryChargeSection].IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    private static void AGripMovesOnlyTheColumnsBesideIt(PanelView panel, Window window, string id)
    {
        var header = Header(panel, id);
        var grip = header.Children.OfType<ColumnGrip>().First();
        var before = Widths(header);

        Drag(window, grip, 30);

        var after = Widths(Header(panel, id));
        var changed = Enumerable.Range(0, before.Length).Where(i => Math.Abs(before[i] - after[i]) > 0.5).ToList();

        Assert.Equal(2, changed.Count);
        Assert.InRange(after[changed[0]] - before[changed[0]], 29, 31);
        Assert.InRange(before[changed[1]] - after[changed[1]], 29, 31);
        Assert.Contains(grip.Name, new[] { TableLayout.GripName(id, header.Layout.Columns[changed[0]].Key) });
    }

    [AvaloniaFact]
    public void AGripOnTheByPurposeTableMovesWidthBetweenTheColumnsBesideIt()
    {
        var (panel, window) = OpenSpend();

        AGripMovesOnlyTheColumnsBesideIt(panel, window, SpendBreakdownPage.TableId);
        window.Close();
    }

    [AvaloniaFact]
    public void AGripOnTheEveryChargeTableMovesWidthBetweenTheColumnsBesideIt()
    {
        var (panel, window) = OpenSpend();

        ShowEveryCharge(panel);
        AGripMovesOnlyTheColumnsBesideIt(panel, window, SpendBreakdownPage.ChargeTableId);
        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetSpendTablesHaveNoGrips()
    {
        var (panel, window) = OpenSpend(headset: true);

        Assert.Empty(Header(panel, SpendBreakdownPage.TableId).Children.OfType<ColumnGrip>());

        ShowEveryCharge(panel);

        Assert.Empty(Header(panel, SpendBreakdownPage.ChargeTableId).Children.OfType<ColumnGrip>());
        window.Close();
    }
}
