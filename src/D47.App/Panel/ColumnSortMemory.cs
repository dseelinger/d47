using D47.App.Theming;
using D47.Core.Configuration;

namespace D47.App.Panel;

/// <summary>Remembers the sort of each table, by table id; the headset's sorts are stored apart from the window's.</summary>
public sealed class ColumnSortMemory(ViewStateStore store, bool headset = false)
{
    /// <summary>The field key and direction stored for a table; null when it is in its default order.</summary>
    public (string Key, SortDirection Direction)? Remembered(string table) =>
        store.Load().ColumnSorts.TryGetValue(ColumnArrangement.IdFor(table, headset), out var sort)
            ? (sort.Key, sort.Descending ? SortDirection.Descending : SortDirection.Ascending)
            : null;

    /// <summary>The <c>order_by</c> name stored with a table's sort; null where the sort is of the rows shown or none is stored.</summary>
    public string? RememberedOrderBy(string table) =>
        store.Load().ColumnSorts.TryGetValue(ColumnArrangement.IdFor(table, headset), out var sort) ? sort.OrderBy : null;

    /// <summary>Stores a table's sort, or forgets it when <paramref name="sort"/> is null; <paramref name="orderBy"/> is the service's name for the field.</summary>
    public void Remember(string table, (string Key, SortDirection Direction)? sort, string? orderBy = null)
    {
        var id = ColumnArrangement.IdFor(table, headset);
        var state = store.Load();

        store.Save(sort is { } chosen
            ? state.WithSort(id, new StoredSort(chosen.Key, chosen.Direction == SortDirection.Descending, orderBy))
            : state.WithoutSort(id));
    }
}
