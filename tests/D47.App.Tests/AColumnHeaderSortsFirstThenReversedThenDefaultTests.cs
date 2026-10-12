using D47.App.Theming;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A sortable column header (#1112): a press sorts in the column's first direction, a second reverses,
/// a third returns to the default order; missing values go last and ties keep the default order.
/// </summary>
public class AColumnHeaderSortsFirstThenReversedThenDefaultTests
{
    private sealed record Row(string Name, long? Value, double? Distance);

    private static readonly IReadOnlyList<SortColumn<Row>> Columns =
    [
        SortColumn<Row>.Text("Name", row => row.Name),
        SortColumn<Row>.Figure("Value", row => row.Value, SortDirection.Descending),
        SortColumn<Row>.Figure("Distance", row => row.Distance, SortDirection.Ascending) with { OneWay = true },
        SortColumn<Row>.Plain("Notes"),
    ];

    private const int Name = 0;
    private const int Value = 1;
    private const int Distance = 2;
    private const int Notes = 3;

    /// <summary>The default order: as listed here.</summary>
    private static readonly IReadOnlyList<Row> Rows =
    [
        new("Cobra", 300, 12.5),
        new("Asp", null, 3),
        new("Viper", 100, null),
        new("Anaconda", 300, 40),
        new("Eagle", 50, 3),
    ];

    private static string[] Names(IReadOnlyList<Row> rows) => [.. rows.Select(row => row.Name)];

    private static SortOrder? Press(SortOrder? current, int column) => ColumnSort.Next(Columns, current, column);

    [Fact]
    public void PressingAColumnSortsFirstThenReversedThenReturnsToDefault()
    {
        var first = Press(null, Value);
        Assert.Equal(new SortOrder(Value, SortDirection.Descending), first);

        var reversed = Press(first, Value);
        Assert.Equal(new SortOrder(Value, SortDirection.Ascending), reversed);

        Assert.Null(Press(reversed, Value));
    }

    [Fact]
    public void PressingAnotherColumnSortsItInItsFirstDirection()
    {
        var reversed = new SortOrder(Value, SortDirection.Ascending);

        Assert.Equal(new SortOrder(Name, SortDirection.Ascending), Press(reversed, Name));
    }

    [Fact]
    public void AOneWayColumnReturnsToDefaultOnItsSecondPress()
    {
        var first = Press(null, Distance);
        Assert.Equal(new SortOrder(Distance, SortDirection.Ascending), first);

        Assert.Null(Press(first, Distance));
    }

    [Fact]
    public void AColumnWithNoSortKeyChangesNothing()
    {
        var sorted = new SortOrder(Value, SortDirection.Descending);

        Assert.Equal(sorted, Press(sorted, Notes));
        Assert.Null(Press(null, Notes));
    }

    [Fact]
    public void NoSortKeepsTheDefaultOrder()
    {
        Assert.Equal(Names(Rows), Names(ColumnSort.Sorted(Rows, Columns, null)));
    }

    [Fact]
    public void AMissingValueGoesLastInBothDirections()
    {
        Assert.Equal(
            ["Cobra", "Anaconda", "Viper", "Eagle", "Asp"],
            Names(ColumnSort.Sorted(Rows, Columns, new SortOrder(Value, SortDirection.Descending))));

        Assert.Equal(
            ["Eagle", "Viper", "Cobra", "Anaconda", "Asp"],
            Names(ColumnSort.Sorted(Rows, Columns, new SortOrder(Value, SortDirection.Ascending))));
    }

    [Fact]
    public void TiesKeepTheDefaultOrderInBothDirections()
    {
        // Cobra and Anaconda tie on Value; Asp and Eagle tie on Distance.
        Assert.Equal(
            ["Cobra", "Anaconda"],
            Names(ColumnSort.Sorted(Rows, Columns, new SortOrder(Value, SortDirection.Descending))).Take(2));

        Assert.Equal(
            ["Asp", "Eagle", "Cobra", "Anaconda", "Viper"],
            Names(ColumnSort.Sorted(Rows, Columns, new SortOrder(Distance, SortDirection.Ascending))));

        Assert.Equal(
            ["Anaconda", "Cobra", "Asp", "Eagle", "Viper"],
            Names(ColumnSort.Sorted(Rows, Columns, new SortOrder(Distance, SortDirection.Descending))));
    }

    [Fact]
    public void TextSortsAToZIgnoringCase()
    {
        Row[] rows = [new("viper", 1, 1), new("Asp", 1, 1), new("cobra", 1, 1), new(" ", 1, 1)];

        Assert.Equal(
            ["Asp", "cobra", "viper", " "],
            Names(ColumnSort.Sorted(rows, Columns, new SortOrder(Name, SortDirection.Ascending))));

        Assert.Equal(
            ["viper", "cobra", "Asp", " "],
            Names(ColumnSort.Sorted(rows, Columns, new SortOrder(Name, SortDirection.Descending))));
    }
}
