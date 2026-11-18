using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// The fields of one kind of search table and the sort the service applies to them. A field whose header sorts is
/// registered with the <c>order_by</c> name it sends; every other field has a plain label.
/// </summary>
internal sealed class SearchTableFields(GalaxySearchKind kind)
{
    private readonly Dictionary<string, string> _sortNames = new(StringComparer.Ordinal);

    public void Clear() => _sortNames.Clear();

    /// <summary>A field; <paramref name="sort"/> is the <c>order_by</c> name its header sorts on, or null for a plain label.</summary>
    public TableField<object> Field(
        string key,
        TableColumn column,
        string label,
        Func<object, Control> cell,
        string? sort = null,
        string? second = null,
        bool right = false,
        bool byDefault = true)
    {
        var field = new TableField<object>(key, column, cell)
        {
            Label = label,
            Second = second,
            Alignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            ByDefault = byDefault,
        };

        if (sort is null || SearchSorts.For(kind).FirstOrDefault(known => known.Name == sort) is not { } spec)
        {
            return field;
        }

        _sortNames[key] = sort;

        return field with
        {
            Sort = SortColumn<object>.Remote(label, spec.FirstDescending ? SortDirection.Descending : SortDirection.Ascending)
                with { OneWay = spec.OneWay },
        };
    }

    /// <summary>
    /// Shows the sort in <paramref name="arguments"/> on <paramref name="table"/>; the nearest-first column in ascending order
    /// when none is asked or the asked column is not drawn.
    /// </summary>
    public void Show(ArrangedTable<object> table, IReadOnlyDictionary<string, string> arguments)
    {
        if (SearchSorts.Read(kind, arguments) is { } asked
            && KeyOf(asked.Field.Name) is { } key
            && table.ShowSort(key, asked.Descending ? SortDirection.Descending : SortDirection.Ascending))
        {
            return;
        }

        table.ShowSort(KeyOf(SearchSorts.Distance), SortDirection.Ascending);
    }

    /// <summary><paramref name="arguments"/> with <c>order_by</c> and <c>order</c> set to the table's sort, or left out for none.</summary>
    public Dictionary<string, string> Sorted(IReadOnlyDictionary<string, string> arguments, ArrangedTable<object> table)
    {
        var sorted = new Dictionary<string, string>(arguments, StringComparer.Ordinal);
        sorted.Remove("order_by");
        sorted.Remove("order");

        if (table.SortedBy is { } sort && _sortNames.TryGetValue(sort.Key, out var name))
        {
            sorted["order_by"] = name;
            sorted["order"] = sort.Direction == SortDirection.Ascending ? "asc" : "desc";
        }

        return sorted;
    }

    /// <summary>The table's header on a rule, the form every search table's head takes.</summary>
    public static Control Head(ArrangedTable<object> table)
    {
        var grid = table.Header();
        grid.Margin = new Thickness(12, 0, 12, 6);

        var rule = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = grid };
        ThemeInk.Bind(rule, Border.BorderBrushProperty, ThemeManager.LineKey);

        return rule;
    }

    private string? KeyOf(string sortName) =>
        _sortNames.Where(pair => pair.Value == sortName).Select(pair => pair.Key).FirstOrDefault();
}
