using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Releasing the leftmost rule at the strip's left edge hides the leftmost pane.</summary>
[Trait("Category", "Integration")]
public sealed class TheLeftmostPaneCanBeDraggedAwayTests
{
    private static ViewStateStore Store() =>
        new(new AppPaths(TempFolders.Create("d47-pane-hide")), new DiskFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static PanelView Furnished(PaneWidthMemory memory, double width)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.Furnish(
            PanelTab.Assets,
            crumb => new TextBlock { Text = crumb.Word },
            new NavCrumb("fleet", "Ships"));

        var window = new Window { Content = panel, Width = width, Height = 700 };
        window.Show();
        panel.EnableDraggablePanes(memory);

        panel.Tab = PanelTab.Assets;
        panel.Nav.GoTo(
            new NavCrumb("fleet", "Ships"),
            new NavCrumb("ship:12", "Corsair"),
            new NavCrumb("slot:3", "Weapon 3"));
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    private static DrillView Strip(PanelView panel) =>
        panel.GetVisualDescendants().OfType<DrillView>().Single();

    private static Grid Grid(DrillView strip) =>
        strip.GetVisualDescendants().OfType<Grid>().First();

    private static string Words(DrillView strip) =>
        string.Join(
            ",",
            Grid(strip).Children.OfType<Border>().Select(host => host.Child).OfType<TextBlock>().Select(text => text.Text));

    [AvaloniaFact]
    public void ReleasedNearTheLeftEdgeTheLeftmostPaneIsHidden()
    {
        var panel = Furnished(new PaneWidthMemory(Store()), 1500);
        var strip = Strip(panel);

        Assert.Equal("Ships,Corsair,Weapon 3", Words(strip));

        strip.ReleaseLeftmostRule(100);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Corsair,Weapon 3", Words(strip));
        Assert.Equal(2, Grid(strip).ColumnDefinitions.Count);
        Assert.Single(Grid(strip).Children.OfType<GridSplitter>());
    }

    [AvaloniaFact]
    public void ReleasedFurtherRightThePaneStays()
    {
        var panel = Furnished(new PaneWidthMemory(Store()), 1500);
        var strip = Strip(panel);

        strip.ReleaseLeftmostRule(190);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Ships,Corsair,Weapon 3", Words(strip));
    }

    [AvaloniaFact]
    public void TheRemainingPanesKeepTheirSharesRelativeToEachOther()
    {
        var memory = new PaneWidthMemory(Store());
        memory.Remember(3, [0.2, 0.2, 0.6]);

        var panel = Furnished(memory, 1500);
        var strip = Strip(panel);

        strip.ReleaseLeftmostRule(0);
        Dispatcher.UIThread.RunJobs();

        var columns = Grid(strip).ColumnDefinitions;

        Assert.Equal(0.2, columns[0].Width.Value, 3);
        Assert.Equal(0.6, columns[1].Width.Value, 3);
        Assert.All(columns, column => Assert.Equal(DrillView.MinimumPaneWidth, column.MinWidth));
    }

    [AvaloniaFact]
    public void AHandleAtTheLeftEdgeBringsThePaneBack()
    {
        var memory = new PaneWidthMemory(Store());
        var panel = Furnished(memory, 1500);
        var strip = Strip(panel);

        strip.ReleaseLeftmostRule(0);
        Dispatcher.UIThread.RunJobs();

        var edge = Grid(strip).Children.OfType<Avalonia.Controls.Panel>().Single();
        Assert.Equal(0, Avalonia.Controls.Grid.GetColumn(edge));
        Assert.NotNull(edge.Cursor);

        strip.ReleaseEdgeHandle(100);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Corsair,Weapon 3", Words(strip));

        strip.ReleaseEdgeHandle(200);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Ships,Corsair,Weapon 3", Words(strip));
        Assert.Equal(3, Grid(strip).ColumnDefinitions.Count);
        Assert.False(memory.LeftmostHidden);
    }

    [AvaloniaFact]
    public void TheHiddenStateSurvivesNavigationAndARestart()
    {
        var store = Store();
        var panel = Furnished(new PaneWidthMemory(store), 1500);
        var strip = Strip(panel);

        strip.ReleaseLeftmostRule(0);
        Dispatcher.UIThread.RunJobs();

        panel.Nav.Drill(new NavCrumb("mod:1", "Beam"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Weapon 3,Beam", Words(strip));

        var restarted = Furnished(new PaneWidthMemory(store), 1500);

        Assert.Equal("Corsair,Weapon 3", Words(Strip(restarted)));
    }

    [AvaloniaFact]
    public void ALoneLevelDrawsWhetherOrNotAPaneIsHidden()
    {
        var memory = new PaneWidthMemory(Store());
        memory.RememberLeftmostHidden(true);

        var panel = Furnished(memory, 500);
        var strip = Strip(panel);

        Assert.Equal("Weapon 3", Words(strip));
        Assert.Empty(Grid(strip).Children.OfType<Avalonia.Controls.Panel>());
    }
}
