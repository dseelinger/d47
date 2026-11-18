using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using Xunit;

namespace D47.App.Tests;

/// <summary>A TableLayout fits columns to content and lets the desktop window drag the boundaries (#1612).</summary>
[Trait("Category", "Integration")]
public class ATableFitsItsColumnsToItsContentTests
{
    private const string Table = "test.table";

    private static readonly TableColumn[] Columns =
    [
        new("slot", ColumnFit.Fixed) { Fixed = 30 },
        new("current"),
        new("plan"),
        new("notes", ColumnFit.Fill),
    ];

    private static TextBlock Text(string text) => new() { Text = text, TextWrapping = Avalonia.Media.TextWrapping.NoWrap };

    private static (Window Window, StackPanel Rows, TableGrid Header, List<TableGrid> Data) Draw(TableLayout layout)
    {
        var header = layout.Header([Text("#"), Text("CURRENT"), Text("PLAN"), Text("NOTES")]);
        var data = new List<TableGrid>
        {
            layout.Row([Text("1"), Text("empty"), Text("Frame shift drive 5A"), Text("a fairly long note that must not be trimmed")]),
            layout.Row([Text("2"), Text("empty"), Text("Shield 3A"), Text("short")]),
        };

        var rows = new StackPanel { Children = { header } };

        foreach (var row in data)
        {
            rows.Children.Add(row);
        }

        var window = new Window { Content = rows, Width = 1000, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, rows, header, data);
    }

    private static double[] Edges(TableGrid grid) => [.. grid.ColumnDefinitions.Select(definition => definition.ActualWidth)];

    private static Point Centre(Control control, Window window) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    [AvaloniaFact]
    public void HeaderAndRowsShareColumnEdgesAndTheContentColumnIsNoWiderThanItsContent()
    {
        var (_, _, header, data) = Draw(new TableLayout(Table, Columns));

        foreach (var row in data)
        {
            Assert.Equal(Edges(header), Edges(row));
        }

        var current = data[0].Children.OfType<TextBlock>().Single(text => text.Text == "empty");
        var label = header.Children.OfType<TextBlock>().Single(text => text.Text == "CURRENT");

        Assert.True(Edges(header)[1] <= Math.Max(current.Bounds.Width, label.Bounds.Width) + 0.5);

        var notes = data[0].Children.OfType<TextBlock>().Last();

        Assert.True(Edges(header)[3] >= notes.DesiredSize.Width);
    }

    [AvaloniaFact]
    public void ATableWithNoMemoryHasNoGrips()
    {
        var (_, _, header, _) = Draw(new TableLayout(Table, Columns));

        Assert.Empty(header.Children.OfType<ColumnGrip>());
    }

    [AvaloniaFact]
    public void DraggingAGripMovesWidthBetweenItsTwoColumnsAndReleasingRemembersBothShares()
    {
        var (settings, store, paths) = TestSurface.Create();
        var memory = new ColumnWidthMemory(store);
        var layout = new TableLayout(Table, Columns, memory);
        var (window, _, header, data) = Draw(layout);
        var grip = header.Children.OfType<ColumnGrip>().Single(g => g.Name == TableLayout.GripName(Table, "plan"));
        var start = Centre(grip, window);
        var before = Edges(header);

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Point(40, 0));
        Dispatcher.UIThread.RunJobs();

        var during = Edges(header);

        Assert.Equal(before[2] + 40, during[2], 1);
        Assert.Equal(before[3] - 40, during[3], 1);
        Assert.Equal(before[1], during[1], 1);
        Assert.Equal(during, Edges(data[1]));
        Assert.Empty(memory.Remembered(Table));

        window.MouseUp(start + new Point(40, 0), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        var total = during.Sum();
        var shares = memory.Remembered(Table);

        Assert.Equal(during[2] / total, shares["plan"], 3);
        Assert.Equal(during[3] / total, shares["notes"], 3);
        Assert.Equal(2, shares.Count);

        var (_, _, again, _) = Draw(new TableLayout(Table, Columns, new ColumnWidthMemory(store)));

        Assert.Equal(during[2], Edges(again)[2], 1);
        Assert.Equal(during[3], Edges(again)[3], 1);
        GC.KeepAlive(settings);
        GC.KeepAlive(paths);
    }

    [AvaloniaFact]
    public void ADraggedColumnKeepsItsProportionWhenTheTableIsWiderAndAfterARestart()
    {
        TableColumn[] two = [new("left"), new("right")];
        var (settings, store, paths) = TestSurface.Create();

        double[] Drawn(double width, ColumnWidthMemory memory, Func<Window, TableGrid, bool> drag)
        {
            var layout = new TableLayout(Table, two, memory);
            var header = layout.Header([Text("LEFT"), Text("RIGHT")]);
            var row = layout.Row([Text("a"), Text("b")]);
            var window = new Window { Content = new StackPanel { Children = { header, row } }, Width = width, Height = 200 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            if (drag(window, header))
            {
                Dispatcher.UIThread.RunJobs();
            }

            return Edges(header);
        }

        var first = Drawn(1200, new ColumnWidthMemory(store), (window, header) =>
        {
            var grip = header.Children.OfType<ColumnGrip>().Single();
            var start = Centre(grip, window);
            var want = 300 - Edges(header)[0];

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(want, 0));
            window.MouseUp(start + new Point(want, 0), MouseButton.Left);

            return true;
        });

        Assert.InRange(first[0], 299, 301);

        var wider = Drawn(1600, new ColumnWidthMemory(store), (_, _) => false);

        Assert.InRange(wider[0], 399, 401);
        Assert.InRange(wider[1], 1199, 1201);
        GC.KeepAlive(settings);
        GC.KeepAlive(paths);
    }

    private static Avalonia.Media.Color Ink(string key) =>
        ((Avalonia.Media.ISolidColorBrush)Application.Current!.Resources[key]!).Color;

    private static Border Line(ColumnGrip grip) => (Border)grip.Child!;

    private static Avalonia.Media.Color Drawn(ColumnGrip grip) => ((Avalonia.Media.ISolidColorBrush)Line(grip).Background!).Color;

    [AvaloniaFact]
    public void AGripDrawsALine2LineAtRestAndAnALineWhileHoveredOrDragged()
    {
        using var look = AppLook.Put();

        var (_, store, _) = TestSurface.Create();
        var (window, _, header, _) = Draw(new TableLayout(Table, Columns, new ColumnWidthMemory(store)));
        var grip = header.Children.OfType<ColumnGrip>().Single(g => g.Name == TableLayout.GripName(Table, "plan"));
        var point = Centre(grip, window);

        Assert.Equal(12, grip.Bounds.Width, 1);
        Assert.Equal(1, Line(grip).Bounds.Width, 1);
        Assert.Equal(6, Line(grip).Bounds.Top, 1);
        Assert.Equal(grip.Bounds.Height - 6, Line(grip).Bounds.Bottom, 1);
        Assert.Equal(Ink(ThemeManager.Line2Key), Drawn(grip));

        window.MouseMove(point);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, Line(grip).Bounds.Width, 1);
        Assert.Equal(Ink(ThemeManager.AKey), Drawn(grip));

        window.MouseDown(point, MouseButton.Left);
        window.MouseMove(point + new Point(0, 400));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Ink(ThemeManager.AKey), Drawn(grip));

        window.MouseUp(point + new Point(0, 400), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, Line(grip).Bounds.Width, 1);
        Assert.Equal(Ink(ThemeManager.Line2Key), Drawn(grip));
    }

    [AvaloniaFact]
    public void DoubleClickingAGripReturnsBothColumnsToTheEvenSpread()
    {
        var (_, evenStore, _) = TestSurface.Create();
        var (_, _, even, _) = Draw(new TableLayout(Table, Columns, new ColumnWidthMemory(evenStore)));
        var (_, store, _) = TestSurface.Create();
        var memory = new ColumnWidthMemory(store);
        memory.Remember(Table, "plan", 0.3);
        memory.Remember(Table, "notes", 0.4);
        var (window, _, header, _) = Draw(new TableLayout(Table, Columns, memory));
        var grip = header.Children.OfType<ColumnGrip>().Single(g => g.Name == TableLayout.GripName(Table, "plan"));
        var point = Centre(grip, window);

        Assert.Equal(0.3 * Edges(header).Sum(), Edges(header)[2], 1);
        Assert.Equal(0.4 * Edges(header).Sum(), Edges(header)[3], 1);

        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(memory.Remembered(Table));
        Assert.Equal(Edges(even), Edges(header).Select(width => Math.Round(width, 1)).ToArray(), new Tolerance());
    }

    [AvaloniaFact]
    public void AFixedColumnHasNoGripAndNeitherDoesTheLastColumn()
    {
        var (_, store, _) = TestSurface.Create();
        var (_, _, header, _) = Draw(new TableLayout(Table, Columns, new ColumnWidthMemory(store)));

        Assert.Equal(["current", "plan"], header.Children.OfType<ColumnGrip>().Select(g => g.Name!.Split('.')[^2]).Order());
    }

    private sealed class Tolerance : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) < 0.6;

        public int GetHashCode(double value) => 0;
    }
}
