using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using D47.App.Panel;
using D47.Core.Configuration;

namespace D47.App.Theming;

/// <summary>
/// A table drawn from its fields in the order its stored arrangement names. Control columns stay at the edge
/// they are declared at. The sort is kept by field key, so a moved column keeps it and a column left out drops it.
/// Every header and row built between two calls of <see cref="Refresh"/> shares one <see cref="Layout"/>.
/// </summary>
public sealed class ArrangedTable<TRow>
{
    private readonly string _id;
    private readonly string _headName;
    private readonly IReadOnlyList<TableField<TRow>> _fields;
    private readonly ColumnArrangementMemory? _arrangement;
    private readonly ColumnWidthMemory? _widths;
    private readonly double _spacing;
    private readonly string? _sortedByDefault;
    private (string Key, SortDirection Direction)? _sort;
    private IReadOnlyList<TableField<TRow>> _drawn = [];
    private SortHeader<TRow> _head;

    public ArrangedTable(
        string id,
        IReadOnlyList<TableField<TRow>> fields,
        ColumnArrangementMemory? arrangement,
        ColumnWidthMemory? widths,
        double spacing = 0,
        string? sortedByDefault = null,
        string? headName = null)
    {
        _id = id;
        _headName = headName ?? id + "Head";
        _fields = fields;
        _arrangement = arrangement;
        _widths = widths;
        _spacing = spacing;
        _sortedByDefault = sortedByDefault;

        Refresh();
    }

    /// <summary>The fields drawn, in order: leading control columns, the arranged fields, trailing control columns.</summary>
    public IReadOnlyList<TableField<TRow>> Shown { get; private set; }

    public TableLayout Layout { get; private set; }

    /// <summary>Raised after the sort changes.</summary>
    public event Action? Changed;

    /// <summary>Re-reads the arrangement and starts a new layout.</summary>
    [MemberNotNull(nameof(Shown), nameof(Layout), nameof(_head))]
    public void Refresh()
    {
        Shown = Arranged();
        _drawn = Drawn(Shown);
        Layout = new TableLayout(_id, [.. _drawn.Select(field => field.Column)], _widths, _spacing);
        _head = NewHead();
    }

    public TableGrid Header() =>
        Layout.Header([.. _drawn.Select((field, column) => field.Label is null && field.Sort is null
            ? new Border()
            : _head.Cell(column, field.Alignment))]);

    public TableGrid Row(TRow row) => Layout.Row([.. _drawn.Select(field => field.Cell(row))]);

    /// <summary><paramref name="rows"/>, given in the default order, sorted by the chosen sort.</summary>
    public IReadOnlyList<TRow> Sort(IReadOnlyList<TRow> rows) => _head.Sort(rows);

    /// <summary>Returns the table to its default order.</summary>
    public void Reset()
    {
        if (_sort is null)
        {
            return;
        }

        _sort = null;
        _head.Show(null);
        Changed?.Invoke();
    }

    private IReadOnlyList<TableField<TRow>> Arranged()
    {
        var arranged = _fields.Where(field => field.Label is not null).ToList();
        var byKey = arranged.ToDictionary(field => field.Key, StringComparer.Ordinal);
        var keys = ColumnArrangement.Shown(
            [.. arranged.Select(field => new ArrangedColumn(field.Key, field.ByDefault, field.Required))],
            _arrangement?.Remembered(_id));

        var first = arranged.Count == 0 ? _fields.Count : _fields.ToList().IndexOf(arranged[0]);

        return
        [
            .. _fields.Take(first).Where(field => field.Label is null),
            .. keys.Select(key => byKey[key]),
            .. _fields.Skip(first).Where(field => field.Label is null),
        ];
    }

    private static List<TableField<TRow>> Drawn(IReadOnlyList<TableField<TRow>> shown) =>
        [.. shown.SelectMany(field => field.More.Prepend(field))];

    private SortHeader<TRow> NewHead()
    {
        var byDefault = SortedAt(_sortedByDefault) is { } at ? new SortOrder(at, _drawn[at].Sort!.First) : (SortOrder?)null;
        var drawn = _drawn;
        var head = new SortHeader<TRow>(_headName, [.. drawn.Select(HeaderColumn)], byDefault);

        if (_sort is { } sort && SortedAt(sort.Key) is { } column)
        {
            head.Show(new SortOrder(column, sort.Direction));
        }
        else
        {
            _sort = null;
        }

        head.Changed += () =>
        {
            _sort = head.Order is { } order ? (drawn[order.Column].Key, order.Direction) : null;
            Changed?.Invoke();
        };

        return head;
    }

    private int? SortedAt(string? key)
    {
        if (key is null)
        {
            return null;
        }

        for (var i = 0; i < _drawn.Count; i++)
        {
            if (_drawn[i].Key == key && _drawn[i].Sort is not null)
            {
                return i;
            }
        }

        return null;
    }

    private static SortColumn<TRow> HeaderColumn(TableField<TRow> field)
    {
        var column = field.Sort ?? SortColumn<TRow>.Plain(field.Label ?? string.Empty);

        return field.Second is { } second ? column with { Second = second } : column;
    }
}
