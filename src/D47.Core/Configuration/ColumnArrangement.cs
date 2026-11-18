namespace D47.Core.Configuration;

/// <summary>A column a table can show now; <paramref name="Required"/> columns are never removed.</summary>
public readonly record struct ArrangedColumn(string Key, bool ShownByDefault = true, bool Required = false);

/// <summary>Which columns a table shows and in what order, as pure functions over column keys.</summary>
public static class ColumnArrangement
{
    public const string HeadsetSuffix = "@headset";

    public static string IdFor(string table, bool headset) => headset ? table + HeadsetSuffix : table;

    /// <summary>The keys to draw: the stored keys the table can show, then missing required columns; the default columns when nothing results.</summary>
    public static IReadOnlyList<string> Shown(IReadOnlyList<ArrangedColumn> columns, IReadOnlyList<string>? stored)
    {
        if (stored is not { Count: > 0 })
        {
            return Defaults(columns);
        }

        var known = columns.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        var shown = stored.Where(known.Contains).Distinct(StringComparer.Ordinal).ToList();

        for (var i = 0; i < columns.Count; i++)
        {
            if (!columns[i].Required || shown.Contains(columns[i].Key))
            {
                continue;
            }

            var at = 0;
            for (var before = i - 1; before >= 0; before--)
            {
                var index = shown.IndexOf(columns[before].Key);
                if (index >= 0)
                {
                    at = index + 1;
                    break;
                }
            }

            shown.Insert(at, columns[i].Key);
        }

        return shown.Count == 0 ? Defaults(columns) : shown;
    }

    /// <summary>The key changes places with the nearest shown column on that side; unchanged at the edge.</summary>
    public static IReadOnlyList<string> Moved(
        IReadOnlyList<ArrangedColumn> columns, IReadOnlyList<string>? stored, string key, bool left)
    {
        var shown = Shown(columns, stored).ToList();
        var from = shown.IndexOf(key);
        var to = left ? from - 1 : from + 1;

        if (from < 0 || to < 0 || to >= shown.Count)
        {
            return Edited(shown, stored, columns);
        }

        (shown[from], shown[to]) = (shown[to], shown[from]);

        return Edited(shown, stored, columns);
    }

    /// <summary>The key goes last; unchanged when the table cannot show it or already does.</summary>
    public static IReadOnlyList<string> Added(
        IReadOnlyList<ArrangedColumn> columns, IReadOnlyList<string>? stored, string key)
    {
        var shown = Shown(columns, stored).ToList();

        if (columns.Any(c => c.Key == key) && !shown.Contains(key))
        {
            shown.Add(key);
        }

        return Edited(shown, stored, columns);
    }

    /// <summary>Unchanged for a required column, the last shown column, or a column not shown.</summary>
    public static IReadOnlyList<string> Removed(
        IReadOnlyList<ArrangedColumn> columns, IReadOnlyList<string>? stored, string key)
    {
        var shown = Shown(columns, stored).ToList();
        var required = columns.Any(c => c.Key == key && c.Required);

        if (!required && shown.Count > 1)
        {
            shown.Remove(key);
        }

        return Edited(shown, stored, columns);
    }

    public static bool IsDefault(IReadOnlyList<ArrangedColumn> columns, IReadOnlyList<string>? stored) =>
        Shown(columns, stored).SequenceEqual(Defaults(columns), StringComparer.Ordinal);

    private static List<string> Defaults(IReadOnlyList<ArrangedColumn> columns) =>
        columns.Where(c => c.ShownByDefault || c.Required).Select(c => c.Key).ToList();

    // Stored keys the table cannot show now stay stored, after the shown ones.
    private static List<string> Edited(List<string> shown, IReadOnlyList<string>? stored, IReadOnlyList<ArrangedColumn> columns)
    {
        if (stored is null)
        {
            return shown;
        }

        var known = columns.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        shown.AddRange(stored.Where(k => !known.Contains(k)).Distinct(StringComparer.Ordinal));

        return shown;
    }
}
