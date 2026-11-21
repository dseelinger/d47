using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using D47.App.Theming;
using Xunit;

namespace D47.App.Tests;

/// <summary>A labelled table field sets Filters, to a list or to none (#2636).</summary>
public sealed class EveryTableColumnDeclaresItsValuesTests
{
    private sealed record Row(string Name);

    private static TableField<Row> Field(string key, IReadOnlyList<ValueList<Row>>? filters) =>
        new(key, new TableColumn(key), _ => new Border())
        {
            Label = key,
            Sort = SortColumn<Row>.Text(key, row => row.Name),
            Filters = filters,
        };

    [AvaloniaFact]
    public void ALabelledFieldWithNullFiltersStopsTheTableBeingBuilt()
    {
        using var look = AppLook.Put();

        var thrown = Assert.Throws<ArgumentException>(() => new ArrangedTable<Row>(
            "Test.Table", [Field("Name", []), Field("Kind", null)], null, null));

        Assert.Contains("Test.Table", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("Kind", thrown.Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void ALabelledFieldWithAnEmptyFilterListBuilds()
    {
        using var look = AppLook.Put();

        _ = new ArrangedTable<Row>("Test.Table", [Field("Name", [])], null, null);
    }
}
