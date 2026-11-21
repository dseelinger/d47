using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using D47.App.Theming;
using Xunit;

namespace D47.App.Tests;

/// <summary>An arranged table hides the rows whose values are all unticked, and keeps the sort of the rest (#2630).</summary>
public class ARowFilterHidesRowsByUntickedValuesTests
{
    private sealed record Place(string Name, string[] Economy, string[] Security);

    private static readonly Place[] Places =
    [
        new("Delta", ["Industrial"], ["High"]),
        new("Alpha", ["Industrial"], ["Low"]),
        new("Charlie", ["Extraction", "Refinery"], ["High"]),
        new("Bravo", ["Agriculture"], []),
        new("Echo", [], ["High"]),
    ];

    private static ArrangedTable<Place> Table(bool economyOnly = false)
    {
        var economy = new ValueList<Place>("Economy", place => place.Economy, "No economy");
        var security = new ValueList<Place>("Security", place => place.Security, "Unknown");

        return new ArrangedTable<Place>(
            "RowFilterTest",
            [
                new TableField<Place>("Name", new TableColumn("Name"), _ => new Border())
                {
                    Label = "Name",
                    Sort = SortColumn<Place>.Text("Name", place => place.Name),
                },
                new TableField<Place>("Economy", new TableColumn("Economy"), _ => new Border())
                {
                    Label = "Economy",
                    Sort = SortColumn<Place>.Text("Economy", place => place.Economy.FirstOrDefault()),
                    Filters = economyOnly ? [economy] : [economy, security],
                },
            ],
            null,
            null);
    }

    private static string[] Names(IEnumerable<Place> rows) => [.. rows.Select(place => place.Name)];

    private static IReadOnlyList<FilterValue> Values(ArrangedTable<Place> table, int block) => table.FilterBlocks[block].Values;

    [AvaloniaFact]
    public void UntickingAValueHidesExactlyTheRowsWithThatValue()
    {
        using var look = AppLook.Put();
        var table = Table();
        table.Observe(Places);

        table.Untick("Economy", 0, "Industrial");

        Assert.Equal(["Charlie", "Bravo", "Echo"], Names(table.Filter(Places)));
        Assert.Equal(2, table.Hidden(Places));
        Assert.Equal(["Economy"], table.FilteredLabels);
    }

    [AvaloniaFact]
    public void ARowOfAListCellHidesOnlyWhenNoneOfItsItemsIsTicked()
    {
        using var look = AppLook.Put();
        var table = Table();
        table.Observe(Places);

        table.Untick("Economy", 0, "Extraction");
        Assert.Contains("Charlie", Names(table.Filter(Places)));

        table.Untick("Economy", 0, "Refinery");
        Assert.DoesNotContain("Charlie", Names(table.Filter(Places)));

        table.Tick("Economy", 0, "Extraction");
        Assert.Contains("Charlie", Names(table.Filter(Places)));
    }

    [AvaloniaFact]
    public void TheNoValueEntryHidesAndShowsTheRowsWithNoValue()
    {
        using var look = AppLook.Put();
        var table = Table();
        table.Observe(Places);

        Assert.Equal("No economy", Values(table, 0)[^1].Value);
        Assert.Equal(1, Values(table, 0)[^1].Count);

        table.Untick("Economy", 0, "No economy");
        Assert.DoesNotContain("Echo", Names(table.Filter(Places)));
        Assert.Equal(4, table.Filter(Places).Count);

        table.Tick("Economy", 0, "No economy");
        Assert.Contains("Echo", Names(table.Filter(Places)));
    }

    [AvaloniaFact]
    public void TwoBlocksOnOneFieldBothApply()
    {
        using var look = AppLook.Put();
        var table = Table();
        table.Observe(Places);

        table.Untick("Economy", 0, "Industrial");
        table.Untick("Economy", 1, "High");

        Assert.Equal(["Bravo"], Names(table.Filter(Places)));
    }

    [AvaloniaFact]
    public void ValuesAreListedMostRowsFirstWithTheNoValueEntryLast()
    {
        using var look = AppLook.Put();
        var table = Table();
        table.Observe(Places);

        Assert.Equal(["Industrial", "Extraction", "Refinery", "Agriculture", "No economy"], Values(table, 0).Select(v => v.Value));
        Assert.Equal(2, Values(table, 0)[0].Count);
        Assert.Equal(["High", "Low", "Unknown"], Values(table, 1).Select(v => v.Value));
    }

    [AvaloniaFact]
    public void AValueSeenByALaterObserveIsAddedAtTheEndTickedAndItsRowsShow()
    {
        using var look = AppLook.Put();
        var table = Table(economyOnly: true);
        table.Observe(Places);
        table.UntickAll("Economy", 0);

        Place[] more = [.. Places, new Place("Foxtrot", ["Tourism"], []), new Place("Golf", ["Tourism"], [])];
        table.Observe(more);

        var values = Values(table, 0);
        Assert.Equal("Tourism", values[^2].Value);
        Assert.Equal("No economy", values[^1].Value);
        Assert.True(values[^2].Ticked);
        Assert.Equal(2, values[^2].Count);
        Assert.Equal(["Foxtrot", "Golf"], Names(table.Filter(more)));
        Assert.Equal("Industrial", values[0].Value);
    }

    [AvaloniaFact]
    public void ClearingTheFiltersShowsEveryRowAndForgetsTheValuesSeen()
    {
        using var look = AppLook.Put();
        var table = Table();
        table.Observe(Places);
        table.Untick("Economy", 0, "Industrial");
        table.UntickAll("Economy", 1);

        table.ClearFilters();

        Assert.Equal(Names(Places), Names(table.Filter(Places)));
        Assert.Empty(table.FilteredLabels);
        Assert.Empty(Values(table, 0));
    }

    [AvaloniaFact]
    public void ShownKeepsTheSortOrderOfTheRowsThatRemain()
    {
        using var look = AppLook.Put();
        var table = Table();
        table.Observe(Places);
        table.Untick("Economy", 0, "Agriculture");

        Assert.True(table.ShowSort("Name", SortDirection.Ascending));

        Assert.Equal(["Alpha", "Charlie", "Delta", "Echo"], Names(table.Shown(Places)));

        table.ShowSort("Name", SortDirection.Descending);

        Assert.Equal(["Echo", "Delta", "Charlie", "Alpha"], Names(table.Shown(Places)));
    }

    [AvaloniaFact]
    public void RefreshKeepsTheFilter()
    {
        using var look = AppLook.Put();
        var table = Table();
        table.Observe(Places);
        table.Untick("Economy", 0, "Industrial");

        table.Refresh();

        Assert.Equal(2, table.Hidden(Places));
    }

    [AvaloniaFact]
    public void ManyIsTrueWhenABlockListsMoreThanTwentyValues()
    {
        using var look = AppLook.Put();
        var table = Table(economyOnly: true);
        var few = Enumerable.Range(0, 20).Select(i => new Place($"P{i}", [$"E{i}"], [])).ToArray();

        table.Observe(few);
        Assert.False(table.Many("Economy"));

        table.Observe([.. few, new Place("Extra", ["E20"], [])]);
        Assert.True(table.Many("Economy"));
    }

    [AvaloniaFact]
    public void OpeningTheFilterClosesTheColumnsBarAndTheReverse()
    {
        using var look = AppLook.Put();
        var table = Table();

        table.ToggleArranging();
        Assert.True(table.Arranging);

        table.ToggleFiltering();
        Assert.True(table.Filtering);
        Assert.False(table.Arranging);

        table.ChooseFilter("Economy");
        Assert.Equal("Economy", table.FilterChosen);

        table.ToggleArranging();
        Assert.True(table.Arranging);
        Assert.False(table.Filtering);
        Assert.Null(table.FilterChosen);
    }

    [AvaloniaFact]
    public void ATableWithNoDeclaredFiltersFiltersNothing()
    {
        using var look = AppLook.Put();
        var table = new ArrangedTable<Place>(
            "NoFilters",
            [new TableField<Place>("Name", new TableColumn("Name"), _ => new Border()) { Label = "Name", Sort = SortColumn<Place>.Text("Name", place => place.Name) }],
            null,
            null);

        table.Observe(Places);

        Assert.False(table.CanFilter);
        Assert.Equal(Names(Places), Names(table.Shown(Places)));
    }
}
