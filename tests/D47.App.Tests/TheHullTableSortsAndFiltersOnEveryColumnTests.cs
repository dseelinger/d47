using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>The hull picker's table sorts by price with unpriced hulls last, and filters on every column (#1920).</summary>
public class TheHullTableSortsAndFiltersOnEveryColumnTests
{
    private static readonly Dictionary<string, long?> Prices = new()
    {
        ["Cobra"] = 300,
        ["Asp"] = 600,
        ["Prototype"] = null,
        ["Adder"] = 100,
        ["Rumour"] = null,
    };

    private static readonly IReadOnlyList<EntryColumn> Columns =
    [
        new("Ship", hull => hull),
        new("Price", hull => BgsWorkPage.Credits(Prices[hull]), hull => Prices[hull], Right: true),
    ];

    private static IReadOnlyList<string> SortedByPrice(SortDirection direction) =>
        ColumnSort.Sorted([.. Prices.Keys.Order()], PanelPrompts.PickTable.SortColumns(Columns), new SortOrder(1, direction));

    [Fact]
    public void UnpricedHullsGoLastCheapestFirst() =>
        Assert.Equal(["Adder", "Cobra", "Asp", "Prototype", "Rumour"], SortedByPrice(SortDirection.Ascending));

    [Fact]
    public void UnpricedHullsGoLastDearestFirst() =>
        Assert.Equal(["Asp", "Cobra", "Adder", "Prototype", "Rumour"], SortedByPrice(SortDirection.Descending));

    [Fact]
    public void TheFirstPressOnPriceSortsCheapestFirst()
    {
        var columns = PanelPrompts.PickTable.SortColumns(ShipsMode.HullColumns);

        var price = ShipsMode.HullColumns.ToList().FindIndex(column => column.Label == "Price");

        Assert.Equal(new SortOrder(price, SortDirection.Ascending), ColumnSort.Next(columns, null, price));
    }

    [Fact]
    public void TheFirstPressOnSpeedSortsTheFastestHullFirst()
    {
        var columns = PanelPrompts.PickTable.SortColumns(ShipsMode.HullColumns);
        var speed = ShipsMode.HullColumns.ToList().FindIndex(column => column.Label == "Speed");
        var order = ColumnSort.Next(columns, null, speed);

        Assert.Equal(SortDirection.Descending, order!.Value.Direction);

        var sorted = ColumnSort.Sorted(["Anaconda", "Sidewinder", "Eagle"], columns, order!.Value);
        var figures = sorted.Select(hull => Core.Knowledge.EliteSpecifications.Ship(hull)!.Speed).ToList();

        Assert.Equal(figures.OrderByDescending(figure => figure), figures);
        Assert.Equal("Anaconda", sorted[^1]);
    }

    [Fact]
    public void TheFilterMatchesASpeedCell()
    {
        var speed = Core.Knowledge.EliteSpecifications.Ship("Sidewinder")!.Speed;

        Assert.True(PanelPrompts.PickTable.Matches("Sidewinder", $"{speed} m/s", ShipsMode.HullColumns));
    }

    [Fact]
    public void TheFilterMatchesTheSizeColumn()
    {
        Assert.True(PanelPrompts.PickTable.Matches("Anaconda", "large", ShipsMode.HullColumns));
        Assert.False(PanelPrompts.PickTable.Matches("Sidewinder", "large", ShipsMode.HullColumns));
    }

    [Fact]
    public void SizeSortsByPadRatherThanAlphabetically()
    {
        var sorted = ColumnSort.Sorted(
            ["Anaconda", "Python", "Sidewinder"],
            PanelPrompts.PickTable.SortColumns(ShipsMode.HullColumns),
            new SortOrder(1, SortDirection.Ascending));

        Assert.Equal(["Sidewinder", "Python", "Anaconda"], sorted);
    }
}
