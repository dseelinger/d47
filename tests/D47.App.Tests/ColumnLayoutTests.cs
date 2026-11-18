using D47.App.Theming;
using Xunit;

namespace D47.App.Tests;

/// <summary>The rules ColumnLayout.Widths follows (#1612).</summary>
public class ColumnLayoutTests
{
    private static readonly IReadOnlyDictionary<string, double> NoneChosen = new Dictionary<string, double>();

    private static IReadOnlyList<double> Widths(
        IReadOnlyList<TableColumn> columns,
        double[] natural,
        double[] minimum,
        double available,
        IReadOnlyDictionary<string, double>? shares = null) =>
        ColumnLayout.Widths(columns, natural, minimum, shares ?? NoneChosen, available);

    [Fact]
    public void ColumnsThatFitTakeTheirNaturalWidth()
    {
        TableColumn[] columns = [new("a"), new("b"), new("c", ColumnFit.Fixed) { Fixed = 40 }];

        Assert.Equal([100, 50, 40], Widths(columns, [100, 50, 0], [10, 10, 0], 190));
    }

    [Fact]
    public void LeftOverSpaceIsSharedEvenlyWhateverTheNaturalWidths()
    {
        TableColumn[] columns = [new("a"), new("b", ColumnFit.Fill), new("c", ColumnFit.Fill)];

        Assert.Equal([200, 200, 300], Widths(columns, [100, 100, 200], [10, 10, 10], 700));
    }

    [Fact]
    public void SixColumnsWithNothingSizedEachGainAnEqualShareOfTheSpareWidth()
    {
        TableColumn[] columns =
            [new("a", ColumnFit.Fill), new("b"), new("c"), new("d"), new("e"), new("f")];
        double[] natural = [300, 80, 60, 90, 40, 70];
        var widths = Widths(columns, natural, [10, 10, 10, 10, 10, 10], natural.Sum() + 600);

        for (var i = 0; i < 6; i++)
        {
            Assert.Equal(natural[i] + 100, widths[i], 6);
        }
    }

    [Fact]
    public void FixedColumnsReceiveNoSpareWidth()
    {
        TableColumn[] columns = [new("a"), new("b", ColumnFit.Fixed) { Fixed = 40 }, new("c")];

        Assert.Equal([150, 40, 110], Widths(columns, [100, 0, 60], [10, 0, 10], 300));
    }

    [Fact]
    public void ColumnsWithAStoredShareKeepItAndReceiveNoSpareWidth()
    {
        TableColumn[] columns = [new("a"), new("b"), new("c"), new("d"), new("e")];
        var shares = new Dictionary<string, double> { ["b"] = 0.25, ["c"] = 0.15 };

        // b is 250 and c is 150; a, d and e share the other 600 less their 300 of natural width.
        var widths = Widths(columns, [100, 90, 90, 100, 100], [10, 10, 10, 10, 10], 1000, shares);

        Assert.Equal([200, 250, 150, 200, 200], widths);
    }

    [Fact]
    public void AShareKeepsItsProportionWhenTheTableIsWider()
    {
        TableColumn[] columns = [new("a"), new("b")];
        var shares = new Dictionary<string, double> { ["a"] = 0.25, ["b"] = 0.75 };

        Assert.Equal([300, 900], Widths(columns, [50, 50], [10, 10], 1200, shares));
        Assert.Equal([400, 1200], Widths(columns, [50, 50], [10, 10], 1600, shares));
    }

    [Fact]
    public void WhenEveryColumnIsSizedTheSpaceStaysEmpty()
    {
        TableColumn[] columns = [new("a"), new("b", ColumnFit.Fill)];
        var shares = new Dictionary<string, double> { ["a"] = 0.16, ["b"] = 0.18 };

        Assert.Equal([80, 90], Widths(columns, [10, 10], [10, 10], 500, shares));
    }

    [Fact]
    public void OverflowIsTakenInProportionToSlackFromUnsizedColumnsFirst()
    {
        TableColumn[] columns = [new("a"), new("b"), new("c")];
        var shares = new Dictionary<string, double> { ["c"] = 0.5 };

        // c is 100; a has 90 of slack and b has 30; 40 must go.
        var widths = Widths(columns, [100, 40, 100], [10, 10, 10], 200, shares);

        Assert.Equal([70, 30, 100], widths);
    }

    [Fact]
    public void SizedColumnsShrinkOnlyOnceTheOthersAreAtTheirMinimum()
    {
        TableColumn[] columns = [new("a"), new("b")];
        var shares = new Dictionary<string, double> { ["b"] = 1.0 };

        var widths = Widths(columns, [100, 50], [20, 20], 120, shares);

        Assert.Equal([20, 100], widths);
    }

    [Fact]
    public void NothingGoesBelowItsMinimumAndFixedColumnsNeverChange()
    {
        TableColumn[] columns = [new("a"), new("b", ColumnFit.Fixed) { Fixed = 50 }, new("c")];

        var widths = Widths(columns, [100, 0, 100], [30, 0, 40], 10);

        Assert.Equal([30, 50, 40], widths);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0)]
    [InlineData(-5)]
    public void AShareThatIsNotFiniteAndPositiveIsIgnored(double bad)
    {
        TableColumn[] columns = [new("a"), new("b", ColumnFit.Fill)];
        var shares = new Dictionary<string, double> { ["a"] = bad };

        Assert.Equal([250, 250], Widths(columns, [100, 100], [10, 10], 500, shares));
    }
}
