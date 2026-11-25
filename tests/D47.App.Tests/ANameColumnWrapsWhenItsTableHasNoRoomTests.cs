using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>When a table's columns need more width than it has, only its name columns give way, by wrapping.</summary>
public class ANameColumnWrapsWhenItsTableHasNoRoomTests
{
    private const string LongName = "Modified Consumer Firmware";

    private static TextBlock Text(string text, bool wrap = false) =>
        new() { Text = text, FontSize = 14, TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap };

    private static double Natural(Control control)
    {
        control.Measure(Size.Infinity);
        return control.DesiredSize.Width;
    }

    private static double LongestWord() => LongName.Split(' ').Max(word => Natural(Text(word)));

    private static double[] Edges(TableGrid grid) => [.. grid.ColumnDefinitions.Select(definition => definition.ActualWidth)];

    [AvaloniaFact]
    public void OnlyTheNameColumnNarrowsAndItsNameWrapsOntoMoreLines()
    {
        TableColumn[] columns = [new("name", ColumnFit.Fill), new("count"), new("ledger")];
        var layout = new TableLayout("test.noroom", columns);
        var name = Text(LongName, wrap: true);
        var count = Text("1,234 / 5,678");
        var ledger = Text("MANUFACTURED");
        var others = Natural(Text("1,234 / 5,678")) + Natural(Text("MANUFACTURED"));
        var word = LongestWord();
        var oneLine = Natural(Text(LongName));

        var header = layout.Header([Text("N"), Text("C"), Text("L")]);
        var row = layout.Row([name, count, ledger]);
        var window = new Window { Content = new StackPanel { Children = { header, row } }, Width = others + word + 30, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var edges = Edges(header);

        Assert.Equal(edges, Edges(row));
        Assert.True(edges[0] < oneLine, $"the name column is {edges[0]} wide; the name on one line is {oneLine}");
        Assert.True(edges[0] >= word - 0.5, $"the name column is {edges[0]} wide; its longest word is {word}");
        Assert.True(edges[1] >= Natural(Text("1,234 / 5,678")) - 0.5);
        Assert.True(edges[2] >= Natural(Text("MANUFACTURED")) - 0.5);
        Assert.True(name.DesiredSize.Height > count.DesiredSize.Height * 1.5, $"the name is {name.DesiredSize.Height} tall; one line is {count.DesiredSize.Height}");
        window.Close();
    }

    [AvaloniaFact]
    public void AColumnThatHoldsNoNamesNeverNarrowsBelowItsWidestCell()
    {
        TableColumn[] columns = [new("name", ColumnFit.Fill), new("figure")];
        var layout = new TableLayout("test.figure", columns);
        var header = layout.Header([Text("N"), Text("F")]);
        var row = layout.Row([Text(LongName, wrap: true), Text("123,456,789 CR")]);
        var window = new Window { Content = new StackPanel { Children = { header, row } }, Width = 60, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(layout.MinimumOf(1) >= Natural(Text("123,456,789 CR")) - 0.5);
        Assert.True(Edges(header)[1] >= Natural(Text("123,456,789 CR")) - 0.5);
        Assert.True(layout.Shortfall > 0);
        window.Close();
    }

    [AvaloniaFact]
    public void ANameBesideAGlyphNarrowsToItsLongestWordAndTheGlyphKeepsItsSlot()
    {
        var glyph = new Border { Width = 20, Height = 20 };
        Grid.SetColumn(glyph, 1);
        var cell = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 6,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { Text(LongName, wrap: true), glyph },
        };
        cell.Measure(Size.Infinity);

        Assert.Equal(Math.Ceiling(LongestWord()) + 6 + 20, NarrowestWidth.Of(cell), 0.5);
    }

    [AvaloniaFact]
    public void TextThatCannotWrapDoesNotNarrow()
    {
        var across = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Text(LongName, wrap: true), Text("+2") } };
        across.Measure(Size.Infinity);
        var trimmed = new TextBlock { Text = LongName, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis };
        trimmed.Measure(Size.Infinity);

        Assert.Equal(across.DesiredSize.Width, NarrowestWidth.Of(across), 0.5);
        Assert.Equal(trimmed.DesiredSize.Width, NarrowestWidth.Of(trimmed), 0.5);
    }

    [AvaloniaFact]
    public void TheHeadsetLeavesOutAColumnWhoseCellsDoNotFit()
    {
        const string Wide = "12345678901234567890123456789012345678901234567890";
        IReadOnlyList<TableField<string>> fields =
        [
            new("Name", new TableColumn("Name", ColumnFit.Fill), row => Text(row, wrap: true)) { Label = "Name", Filters = [], Sort = SortColumn<string>.Text("Name", row => row), Required = true },
            new("A", new TableColumn("A"), _ => Text(Wide)) { Label = "A", Filters = [], Sort = SortColumn<string>.Text("A", row => row) },
            new("B", new TableColumn("B"), _ => Text(Wide)) { Label = "B", Filters = [], Sort = SortColumn<string>.Text("B", row => row) },
        ];
        var views = new ViewStateStore(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);
        var table = new ArrangedTable<string>("test.headset", fields, new ColumnArrangementMemory(views, headset: true), null);
        var host = new Border();
        var changed = 0;
        table.Changed += () => changed++;

        void Draw()
        {
            table.Refresh();
            host.Child = new StackPanel { Children = { table.Header(), table.Row(LongName) } };
        }

        Draw();
        var window = new Window { Content = host, Width = Natural(Text(Wide)) + 120, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, table.ShownFields.Count);
        Assert.True(changed > 0, "the table did not ask to be drawn again");

        Draw();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Name", "A"], table.ShownFields.Select(field => field.Key));
        Assert.True(table.Layout.Shortfall <= 0.5, $"the drawn columns are {table.Layout.Shortfall} short");
        window.Close();
    }
}
