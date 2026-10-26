using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Fleet slot table sizes its columns to their content (#1613).</summary>
public class TheFleetSlotTableFitsItsColumnsTests
{
    private static readonly string[] Effects = ["Thermal Resistance", "Energy Efficiency", "Heat Efficiency"];

    private static LoadoutRow Row(LoadoutSide current, LoadoutSide? plan) =>
        new("k", "Slot", "Slot", null, false)
        {
            Parts = new LoadoutParts(4, "Hardpoint 1", current, plan, "empty"),
        };

    private static readonly LoadoutSide Empty = new(null, null, null, null, null, []);

    private static readonly LoadoutSide Beam = new("4A Beam Laser, gimballed", null, "Efficient Weapon", 5, null, Effects);

    private static (Window Window, TableGrid Header, List<TableGrid> Rows) Draw(
        TableLayout table, params LoadoutRow[] rows)
    {
        var header = (TableGrid)LoadoutPages.SlotHeader(table);

        // The theme is not loaded here; the row's real inset (12) already equals the header's margin.
        header.Margin = default;
        var panel = new StackPanel { Children = { header } };
        var drawn = new List<TableGrid>();

        foreach (var row in rows)
        {
            var button = (Button)LoadoutPages.SlotRow(row, () => { }, table);

            button.Padding = default;
            button.BorderThickness = default;

            drawn.Add((TableGrid)button.Content!);
            panel.Children.Add(button);
        }

        var window = new Window { Content = panel, Width = 1000, Height = 400 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, header, drawn);
    }

    private static string Said(TextBlock text) =>
        text.Inlines is { Count: > 0 } inlines
            ? string.Concat(inlines.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text))
            : text.Text ?? string.Empty;

    private static double Width(TableGrid grid, int column) => grid.ColumnDefinitions[column].ActualWidth;

    [AvaloniaFact]
    public void AnEmptyCurrentColumnIsNoWiderThanItsLabelAndThePlanKeepsItsEffects()
    {
        var (window, header, rows) = Draw(LoadoutPages.SlotLayout(), Row(Empty, Beam), Row(Empty, Beam));

        var label = header.Children.OfType<TextBlock>().Single(text => text.Text == "CURRENT");
        var empty = rows[0].Children.OfType<TextBlock>().Single(text => text.Text == "empty");

        Assert.True(Width(header, 2) <= Math.Max(label.Bounds.Width, empty.Bounds.Width + 10) + 1);

        var plan = Said(rows[0].Children.OfType<TextBlock>().Last(text => Said(text).Contains("Beam Laser", StringComparison.Ordinal)));

        Assert.Contains("Efficient Weapon G5", plan, StringComparison.Ordinal);
        Assert.Contains("Thermal Resistance", plan, StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public void HeaderAndRowsShareTheirColumnEdges()
    {
        var (window, header, rows) = Draw(LoadoutPages.SlotLayout(), Row(Beam, Beam), Row(Empty, Beam));

        foreach (var row in rows)
        {
            Assert.Equal(
                header.ColumnDefinitions.Select(definition => definition.ActualWidth),
                row.ColumnDefinitions.Select(definition => definition.ActualWidth));
        }

        window.Close();
    }

    [AvaloniaFact]
    public void ACellTrimmedToFitStillReportsItsFullWidth()
    {
        var (window, _, rows) = Draw(LoadoutPages.SlotLayout(), Row(Beam, Beam));

        var current = rows[0].Children.OfType<TextBlock>().First(text => Said(text).Contains("Beam Laser", StringComparison.Ordinal));

        Assert.True(TableGrid.GetNaturalWidth(current) >= current.DesiredSize.Width);
        Assert.True(TableGrid.GetNaturalWidth(current) > 300);

        window.Close();
    }

    [AvaloniaFact]
    public void ADraggedPlanWidthHoldsOnAnotherShip()
    {
        var store = new ViewStateStore(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);
        var memory = new ColumnWidthMemory(store);

        memory.Remember(LoadoutPages.SlotTable, "Plan", 420);

        var (window, _, rows) = Draw(LoadoutPages.SlotLayout(memory), Row(Empty, Beam));

        Assert.Equal(420, Width(rows[0], 3), 0.5);

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetTableHasNoGrips()
    {
        var (window, header, _) = Draw(LoadoutPages.SlotLayout(), Row(Empty, Beam));

        Assert.Empty(header.Children.OfType<ColumnGrip>());

        window.Close();
    }
}
