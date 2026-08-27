using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The rule between two panes becomes something the mouse can take hold of (list.md Phase 55).
/// <para>
/// Driven through the drawn panel rather than through the memory class, because the two claims
/// worth making are both about what is on screen: that a handle exists where a mouse can reach it,
/// and that it does <em>not</em> exist on the surfaces the ask rules out.
/// </para>
/// </summary>
public sealed class ThePanesMoveWhereYouWantThemTests
{
    private static PanelView Laid(PanelView panel, double width, double height = 700)
    {
        var window = new Window { Content = panel, Width = width, Height = height };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    private static PanelView Furnished(double width)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.Furnish(
            PanelTab.Loadout,
            crumb => new TextBlock { Text = crumb.Word },
            new NavCrumb("fleet", "Ships"));

        return Laid(panel, width);
    }

    private static PaneWidthMemory Memory(out AppPaths paths)
    {
        paths = new AppPaths(TempFolders.Create("d47-pane-widths"));
        return new PaneWidthMemory(new ViewStateStore(paths, NullLogger<ViewStateStore>.Instance));
    }

    private static DrillView Strip(PanelView panel) =>
        panel.GetVisualDescendants().OfType<DrillView>().Single();

    private static Grid Grid(DrillView strip) =>
        strip.GetVisualDescendants().OfType<Grid>().First();

    private static void Drill(PanelView panel)
    {
        panel.Tab = PanelTab.Loadout;
        panel.Nav.GoTo(
            new NavCrumb("fleet", "Ships"),
            new NavCrumb("ship:12", "Corsair"));

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// <b>The safety property, and the reason this phase is the window's alone.</b> The same
    /// <c>PanelView</c> is instantiated on three surfaces, and the headset drives it through a
    /// geometric hit test — so a handle that existed there would be draggable by the ray, which is
    /// the one outcome the ask rules out. A surface that was never furnished with a mouse has no
    /// handle to find.
    /// </summary>
    [AvaloniaFact]
    public void ASurfaceWithNoMouseHasNoHandles()
    {
        var panel = Furnished(900);
        Drill(panel);

        Assert.Empty(Grid(Strip(panel)).Children.OfType<GridSplitter>());
    }

    /// <summary>And the same surface, furnished, grows one handle per rule — never one on the
    /// outside edge, because there is nothing on the far side of it to resize.</summary>
    [AvaloniaFact]
    public void AFurnishedSurfaceGrowsOneHandlePerRule()
    {
        var panel = Furnished(900);
        panel.EnableDraggablePanes(Memory(out _));

        Drill(panel);

        var strip = Strip(panel);

        Assert.Equal(2, strip.Panes);
        Assert.Single(Grid(strip).Children.OfType<GridSplitter>());
    }

    /// <summary>
    /// The reflow still decides how many panes there are, and the handles follow it rather than
    /// competing with it: three panes is two rules, and two rules is two handles.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(520, 1, 0)]
    [InlineData(900, 2, 1)]
    [InlineData(1500, 3, 2)]
    public void ThereIsAHandleBetweenEveryPairAndNowhereElse(double width, int panes, int handles)
    {
        var panel = Furnished(width);
        panel.EnableDraggablePanes(Memory(out _));

        panel.Tab = PanelTab.Loadout;
        panel.Nav.GoTo(
            new NavCrumb("fleet", "Ships"),
            new NavCrumb("ship:12", "Corsair"),
            new NavCrumb("slot:3", "Weapon 3"));

        Dispatcher.UIThread.RunJobs();

        var strip = Strip(panel);

        Assert.Equal(panes, strip.Panes);
        Assert.Equal(handles, Grid(strip).Children.OfType<GridSplitter>().Count());
    }

    /// <summary>
    /// <b>The trap the phase names.</b> <c>Draw</c> clears the column definitions on every
    /// navigation and re-adds one star per pane, so a width the Commander dragged is discarded the
    /// moment they open a ship — unless the remembered shares are re-applied on every draw. Here
    /// the split is written down first, and then a navigation is made to try to lose it.
    /// </summary>
    [AvaloniaFact]
    public void ADragSurvivesTheNavigationThatRedrawsTheStrip()
    {
        var memory = Memory(out _);
        memory.Remember(2, [0.25, 0.75]);

        var panel = Furnished(900);
        panel.EnableDraggablePanes(memory);

        Drill(panel);

        // A second navigation, which is what empties and rebuilds the columns.
        panel.Nav.Drill(new NavCrumb("slot:3", "Weapon 3"));
        Dispatcher.UIThread.RunJobs();

        var columns = Grid(Strip(panel)).ColumnDefinitions;

        Assert.Equal(2, columns.Count);
        Assert.Equal(0.25, columns[0].Width.Value, 3);
        Assert.Equal(0.75, columns[1].Width.Value, 3);
    }

    /// <summary>
    /// A two-pane split and a three-pane split are different arrangements, and the reflow moves
    /// between them on its own as the window is dragged. One remembered list would have widening
    /// the window silently restate the two-pane choice as a three-pane one.
    /// </summary>
    [AvaloniaFact]
    public void APaneCountKeepsItsOwnSplit()
    {
        var memory = Memory(out _);
        memory.Remember(2, [0.25, 0.75]);

        Assert.NotNull(memory.Remembered(2));
        Assert.Null(memory.Remembered(3));
    }

    /// <summary>
    /// <c>MinimumPaneWidth</c> is the clamp on the drag as well as on the reflow, and it has to be
    /// the same number: otherwise a Commander can drag a pane down to a sliver that
    /// <c>ArrangeOverride</c> still believes is 380 wide.
    /// </summary>
    [AvaloniaFact]
    public void EveryPaneCarriesTheReflowsOwnFloor()
    {
        var panel = Furnished(1500);
        panel.EnableDraggablePanes(Memory(out _));

        Drill(panel);

        var columns = Grid(Strip(panel)).ColumnDefinitions;

        Assert.All(columns, column => Assert.Equal(DrillView.MinimumPaneWidth, column.MinWidth));
    }

    /// <summary>And the surface with no mouse buys no layout constraint for a control it never
    /// draws.</summary>
    [AvaloniaFact]
    public void ASurfaceWithNoMouseCarriesNoFloorEither()
    {
        var panel = Furnished(1500);
        Drill(panel);

        var columns = Grid(Strip(panel)).ColumnDefinitions;

        Assert.All(columns, column => Assert.Equal(0, column.MinWidth));
    }

    /// <summary>
    /// The record is read back through validation, because the file it comes from is one a
    /// Commander can edit. A wrong-length, zero, negative or non-finite share means equal panes
    /// rather than a layout given a number it cannot use.
    /// </summary>
    [Theory]
    [InlineData(new[] { 0.5 })]                 // too few for the pane count asked for
    [InlineData(new[] { 0.3, 0.3, 0.4 })]       // too many
    [InlineData(new[] { 0.0, 1.0 })]            // a pane that could never be dragged back
    [InlineData(new[] { -0.5, 1.5 })]
    [InlineData(new[] { double.NaN, 1.0 })]
    [InlineData(new[] { double.PositiveInfinity, 1.0 })]
    public void AShareThatCannotBeUsedMeansEqualPanes(double[] stored)
    {
        var state = new ViewState().With(2, stored);

        Assert.Null(state.SharesFor(2));
    }

    /// <summary>And a usable one is handed back as written, so the caller may normalise it.</summary>
    [Fact]
    public void AUsableShareIsHandedBack()
    {
        var state = new ViewState().With(2, [1, 3]);

        Assert.Equal([1, 3], state.SharesFor(2));
    }
}
