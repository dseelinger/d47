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
        IReadOnlyDictionary<string, double>? chosen = null) =>
        ColumnLayout.Widths(columns, natural, minimum, chosen ?? NoneChosen, available);

    [Fact]
    public void ColumnsThatFitTakeTheirNaturalWidth()
    {
        TableColumn[] columns = [new("a"), new("b"), new("c", ColumnFit.Fixed) { Fixed = 40 }];

        Assert.Equal([100, 50, 40], Widths(columns, [100, 50, 0], [10, 10, 0], 190));
    }

    [Fact]
    public void LeftOverSpaceGoesToUnchosenFillColumnsByNaturalWidth()
    {
        TableColumn[] columns = [new("a"), new("b", ColumnFit.Fill), new("c", ColumnFit.Fill)];

        var widths = Widths(columns, [100, 100, 200], [10, 10, 10], 500);

        Assert.Equal(100, widths[0]);
        Assert.Equal(100 + (100.0 / 3), widths[1], 6);
        Assert.Equal(200 + (200.0 / 3), widths[2], 6);
    }

    [Fact]
    public void WithNoFillColumnLeftOverSpaceGoesToContentColumns()
    {
        TableColumn[] columns = [new("a"), new("b")];

        Assert.Equal([150, 50], Widths(columns, [75, 25], [10, 10], 200));
    }

    [Fact]
    public void WhenEveryColumnIsChosenTheSpaceStaysEmpty()
    {
        TableColumn[] columns = [new("a"), new("b", ColumnFit.Fill)];
        var chosen = new Dictionary<string, double> { ["a"] = 80, ["b"] = 90 };

        Assert.Equal([80, 90], Widths(columns, [10, 10], [10, 10], 500, chosen));
    }

    [Fact]
    public void AChosenFillColumnKeepsItsWidthAndTheSpaceGoesElsewhere()
    {
        TableColumn[] columns = [new("a"), new("b", ColumnFit.Fill), new("c", ColumnFit.Fill)];
        var chosen = new Dictionary<string, double> { ["b"] = 60 };

        Assert.Equal([100, 60, 340], Widths(columns, [100, 100, 200], [10, 10, 10], 500, chosen));
    }

    [Fact]
    public void OverflowIsTakenInProportionToSlackFromUnchosenColumnsFirst()
    {
        TableColumn[] columns = [new("a"), new("b"), new("c")];
        var chosen = new Dictionary<string, double> { ["c"] = 100 };

        // a has 90 of slack and b has 30; 60 must go.
        var widths = Widths(columns, [100, 40, 100], [10, 10, 10], 180, chosen);

        Assert.Equal([55, 25, 100], widths);
    }

    [Fact]
    public void ChosenColumnsShrinkOnlyOnceTheOthersAreAtTheirMinimum()
    {
        TableColumn[] columns = [new("a"), new("b")];
        var chosen = new Dictionary<string, double> { ["b"] = 200 };

        var widths = Widths(columns, [100, 50], [20, 20], 120, chosen);

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
    public void AChosenWidthThatIsNotFiniteAndPositiveIsIgnored(double bad)
    {
        TableColumn[] columns = [new("a"), new("b", ColumnFit.Fill)];
        var chosen = new Dictionary<string, double> { ["a"] = bad };

        Assert.Equal([100, 400], Widths(columns, [100, 100], [10, 10], 500, chosen));
    }
}
