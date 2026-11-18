using Avalonia;
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
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.App.Tests.UiWait;

namespace D47.App.Tests;

/// <summary>Spare width is shared across every column and a grip moves width between its two columns (#2573).</summary>
[Trait("Category", "Integration")]
public sealed class SpareTableWidthIsSharedAcrossEveryColumnTests
{
    private static readonly IReadOnlyList<FactionStanding> Factions =
    [
        new("Amicale des Compagnons de l'Espace", "Democracy", "Federation", 0.4, ["Boom"], []),
        new("LPV Corporation", "Corporate", "Independent", 0.3, [], ["Bust"]),
        new("HIP 112494 Purple Legal Org", "Dictatorship", "Empire", 0.2, ["Civil unrest"], []),
        new("HIP 112494 Gold Council", "Cooperative", "Alliance", 0.1, [], []),
    ];

    private static ViewStateStore Views() =>
        new(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static StarSystemProfile WithFactions() =>
        TheSystemPageListsItsStationsTests.Ltt7786Profile() with { Factions = Factions };

    private static (PanelView Panel, Window Window) Open(ViewStateStore views, int width, string section, StarSystemProfile? profile = null)
    {
        var shown = profile ?? TheSystemPageListsItsStationsTests.Ltt7786Profile();
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableSearch();
        var widths = new ColumnWidthMemory(views);
        panel.EnableResizableColumns(widths);
        panel.EnableColumnArrangements(new ColumnArrangementMemory(views));
        panel.EnableStarSystem(new StarSystemSurface(
            new TheSystemPageListsItsStationsTests.Systems(shown),
            () => new JournalLocation("LTT 7786", null, false, null) { SystemAddress = 633608311522 })
        {
            ColumnWidths = widths,
        });

        var window = new Window { Content = panel, Width = width, Height = 1180 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        Until(() => panel.GetVisualDescendants().OfType<TextBlock>().Any(block => block.IsEffectivelyVisible && block.Text == "LAST REPORTED"));

        if (section != "Overview")
        {
            TheSystemPageListsItsStationsTests.Section(panel, section);
        }

        return (panel, window);
    }

    private static TableGrid Header(Visual root, string id) =>
        root.GetVisualDescendants().OfType<TableGrid>().First(grid => grid.Layout.Id == id && grid.IsHeader);

    private static double[] Widths(TableGrid grid) => [.. grid.ColumnDefinitions.Select(definition => definition.ActualWidth)];

    private static double[] Lefts(TableGrid grid)
    {
        var lefts = new double[grid.ColumnDefinitions.Count];

        for (var i = 1; i < lefts.Length; i++)
        {
            lefts[i] = lefts[i - 1] + grid.ColumnDefinitions[i - 1].ActualWidth;
        }

        return lefts;
    }

    private static ColumnGrip Grip(TableGrid header, string table, string key) =>
        header.Children.OfType<ColumnGrip>().Single(grip => grip.Name == TableLayout.GripName(table, key));

    private static Point Centre(Control control, Window window) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    private static void Drag(Window window, ColumnGrip grip, double by)
    {
        var start = Centre(grip, window);

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Point(by, 0));
        Dispatcher.UIThread.RunJobs();
        window.MouseUp(start + new Point(by, 0), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Visual root, string name)
    {
        var button = root.GetVisualDescendants().OfType<Button>().First(control => control.Name == name);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheMinorFactionsTableLeavesNoWiderGapAfterTheNamesThanBetweenItsFigures()
    {
        var (panel, window) = Open(Views(), 1900, "Overview", WithFactions());
        var header = Header(panel, StarSystemPage.FactionsTable);
        var lefts = Lefts(header);
        var widest = panel.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => Factions.Any(faction => faction.Name == block.Text))
            .MaxBy(block => block.Bounds.Width)!;
        var textRight = widest.TranslatePoint(new Point(widest.Bounds.Width, 0), header)!.Value.X;
        var nameToGovernment = lefts[1] - textRight;
        var governmentToAllegiance = lefts[2] - lefts[1];

        Assert.True(
            nameToGovernment <= governmentToAllegiance,
            $"{nameToGovernment} after the names, {governmentToAllegiance} between Government and Allegiance.");
        window.Close();
    }

    [AvaloniaFact]
    public void DraggingTheGovernmentGripMovesWidthBetweenGovernmentAndAllegianceAlone()
    {
        var (panel, window) = Open(Views(), 1900, "Overview", WithFactions());
        var header = Header(panel, StarSystemPage.FactionsTable);
        var grip = Grip(header, StarSystemPage.FactionsTable, "Government");
        var widthsBefore = Widths(header);
        var leftsBefore = Lefts(header);
        var gripBefore = Centre(grip, window).X;

        Drag(window, grip, 40);

        var widths = Widths(header);
        var lefts = Lefts(header);

        Assert.Equal(widthsBefore[1] + 40, widths[1], 1);
        Assert.Equal(widthsBefore[2] - 40, widths[2], 1);
        Assert.Equal(gripBefore + 40, Centre(grip, window).X, 1);

        foreach (var column in new[] { 0, 3, 4, 5 })
        {
            Assert.Equal(widthsBefore[column], widths[column], 1);
        }

        foreach (var column in new[] { 0, 1, 3, 4, 5 })
        {
            Assert.Equal(leftsBefore[column], lefts[column], 1);
        }

        window.Close();
    }

    [AvaloniaFact]
    public void ARestartedPageKeepsTheDraggedProportion()
    {
        var views = Views();
        var (first, firstWindow) = Open(views, 1900, "Overview", WithFactions());
        var header = Header(first, StarSystemPage.FactionsTable);

        Drag(firstWindow, Grip(header, StarSystemPage.FactionsTable, "Government"), 40);

        var dragged = Widths(header);
        firstWindow.Close();

        var (again, againWindow) = Open(views, 1900, "Overview", WithFactions());

        Assert.Equal(dragged, Widths(Header(again, StarSystemPage.FactionsTable)), new Within(1));
        againWindow.Close();
    }

    [AvaloniaFact]
    public void AColumnAddedToTheStationsTableCanBeWidenedFromItsLeftBoundary()
    {
        var (panel, window) = Open(Views(), 1900, "Stations");
        var id = StarSystemPage.StationsTable;

        Press(panel, ColumnArranger.NameOf(id, "Tile"));
        Press(panel, ColumnArranger.NameOf(id, "Add"));
        panel.GetVisualDescendants().OfType<CheckBox>().First(box => box.Name == ColumnArranger.NameOf(id, "Show.Market")).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Press(panel, ColumnArranger.NameOf(id, "Done"));

        var header = Header(panel, id);
        var keys = header.Layout.Columns.Select(column => column.Key).ToList();
        var added = keys.IndexOf("Market");

        Assert.True(added > 0, string.Join(", ", keys));

        var before = Widths(header)[added];

        Drag(window, Grip(header, id, keys[added - 1]), -30);

        Assert.True(Widths(header)[added] > before + 29, $"{before} became {Widths(header)[added]}.");
        window.Close();
    }

    private sealed class Within(double tolerance) : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) <= tolerance;

        public int GetHashCode(double value) => 0;
    }
}
