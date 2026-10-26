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
    public void DraggingAGripSetsTheColumnWidthAndReleasingRemembersIt()
    {
        var (settings, store, paths) = TestSurface.Create();
        var memory = new ColumnWidthMemory(store);
        var layout = new TableLayout(Table, Columns, memory);
        var (window, _, header, data) = Draw(layout);
        var grip = header.Children.OfType<ColumnGrip>().Single(g => g.Name == TableLayout.GripName(Table, "plan"));
        var start = Centre(grip, window);
        var before = Edges(header)[2];

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Point(40, 0));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(before + 40, Edges(header)[2], 1);
        Assert.Equal(Edges(header), Edges(data[1]));
        Assert.Empty(memory.Remembered(Table));

        window.MouseUp(start + new Point(40, 0), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(before + 40, memory.Remembered(Table)["plan"], 1);

        var redrawn = new TableLayout(Table, Columns, new ColumnWidthMemory(store));
        var (_, _, again, _) = Draw(redrawn);

        Assert.Equal(before + 40, Edges(again)[2], 1);
        GC.KeepAlive(settings);
        GC.KeepAlive(paths);
    }

    [AvaloniaFact]
    public void DoubleClickingAGripClearsTheChosenWidth()
    {
        var (_, store, _) = TestSurface.Create();
        var memory = new ColumnWidthMemory(store);
        memory.Remember(Table, "plan", 300);
        var (window, _, header, _) = Draw(new TableLayout(Table, Columns, memory));
        var grip = header.Children.OfType<ColumnGrip>().Single(g => g.Name == TableLayout.GripName(Table, "plan"));
        var point = Centre(grip, window);

        Assert.Equal(300, Edges(header)[2], 1);

        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(memory.Remembered(Table));
        Assert.True(Edges(header)[2] < 300);
    }
}
