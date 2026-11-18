using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// The fields of one kind of search table and how each header sorts. A field the service orders by is registered with the
/// <c>order_by</c> name it sends; every other field sorts the rows already fetched.
/// </summary>
internal sealed class SearchTableFields(GalaxySearchKind kind)
{
    private readonly Dictionary<string, string> _sortNames = new(StringComparer.Ordinal);

    public void Clear() => _sortNames.Clear();

    /// <summary>The field's header asks the service to order the search.</summary>
    public bool IsRemote(string key) => _sortNames.ContainsKey(key);

    /// <summary>The <c>order_by</c> name the field's header sends; null where it sorts the rows fetched.</summary>
    public string? OrderBy(string key) => _sortNames.GetValueOrDefault(key);

    /// <summary>
    /// A field; <paramref name="sort"/> is the <c>order_by</c> name its header asks the service for. Without one,
    /// <paramref name="local"/> sorts the rows fetched.
    /// </summary>
    public TableField<object> Field(
        string key,
        TableColumn column,
        string label,
        Func<object, Control> cell,
        string? sort = null,
        string? second = null,
        bool right = false,
        bool byDefault = true,
        Func<string, SortColumn<object>>? local = null)
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
            return local is null ? field : field with { Sort = local(label) };
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

/// <summary>The sorts of the rows a table has fetched, over one value of each row.</summary>
internal static class LocalSort
{
    public static Func<string, SortColumn<object>> Text<TRow>(Func<TRow, string?> key) =>
        label => SortColumn<object>.Text(label, row => key((TRow)row));

    public static Func<string, SortColumn<object>> Figure<TRow>(Func<TRow, double?> key, SortDirection first = SortDirection.Descending) =>
        label => SortColumn<object>.Figure<double>(label, row => key((TRow)row), first);

    public static Func<string, SortColumn<object>> Flag<TRow>(Func<TRow, bool?> key) =>
        Figure<TRow>(row => key(row) is { } flag ? (flag ? 1 : 0) : null);

    public static Func<string, SortColumn<object>> When<TRow>(Func<TRow, DateTimeOffset?> key) =>
        Figure<TRow>(row => key(row)?.ToUnixTimeSeconds());
}
