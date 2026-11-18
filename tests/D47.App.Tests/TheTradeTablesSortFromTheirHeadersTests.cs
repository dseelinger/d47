using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>Best cargo and Construction needs sort from their column headers (#1116); every column does (#2575).</summary>
public sealed class TheTradeTablesSortFromTheirHeadersTests
{
    [Fact]
    public void ProfitPerTonneListsTheHighestProfitFirstFromEveryPick()
    {
        IReadOnlyList<CargoPick> picks =
        [
            new("Gold", "A", 1_000, 100, CargoLimit.Hold),
            new("Tea", "B", 200, 500, CargoLimit.Supply),
            new("Opals", "C", 3_000, 10, CargoLimit.Demand),
        ];

        var sorted = ColumnSort.Sorted(picks, RouteBestCargoPage.Headings, new SortOrder(3, SortDirection.Descending));

        Assert.Equal(["Opals", "Gold", "Tea"], sorted.Select(pick => pick.Commodity));
    }

    [Fact]
    public void TheNumberColumnSortsByTotalProfitHighestFirst()
    {
        Assert.Equal(new SortOrder(0, SortDirection.Descending), ColumnSort.Next(RouteBestCargoPage.Headings, null, 0));
    }

    [Fact]
    public void ToBuyListsTheLargestPurchaseFirstAndOrderListsTheOpenOrdersFirst()
    {
        static ConstructionNeeds Need(string name, int toBuy) =>
            new(new ConstructionResource(name, 0, 0), toBuy, 0, null, false, toBuy);

        IReadOnlyList<ConstructionNeeds> needs = [Need("Steel", 40), Need("Water", 400), Need("Fruit", 0)];

        var sorted = ColumnSort.Sorted(needs, ConstructionPage.NeedHeadings, new SortOrder(4, SortDirection.Descending));

        Assert.Equal(["Water", "Steel", "Fruit"], sorted.Select(need => need.Resource.Name));
        Assert.Equal(new SortOrder(5, SortDirection.Descending), ColumnSort.Next(ConstructionPage.NeedHeadings, null, 5));
    }
}
