using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using D47.App.Panel;
using D47.Core.Configuration;

namespace D47.App.Theming;

/// <summary>
/// A table drawn from its fields in the order its stored arrangement names. Control columns stay at the edge
/// they are declared at. The sort is kept by field key, so a moved column keeps it and a column left out drops it.
/// Every header and row built between two calls of <see cref="Refresh"/> shares one <see cref="Layout"/>.
/// While <see cref="Arranging"/>, a header press chooses a column instead of sorting, and every edit is stored as it is made.
/// </summary>
public sealed class ArrangedTable<TRow>
{
    private readonly string _headName;
    private readonly IReadOnlyList<TableField<TRow>> _fields;
    private readonly IReadOnlyList<TableField<TRow>> _arrangeable;
    private readonly ColumnArrangementMemory? _arrangement;
    private readonly ColumnWidthMemory? _widths;
    private readonly double _spacing;
    private readonly string? _sortedByDefault;
    private IReadOnlyList<string>? _unstored;
    private (string Key, SortDirection Direction)? _sort;
    private IReadOnlyList<TableField<TRow>> _drawn = [];
    private IReadOnlyList<string> _owners = [];
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
        Id = id;
        _headName = headName ?? id + "Head";
        _fields = fields;
        _arrangeable = [.. fields.Where(field => field.Label is not null)];
        _arrangement = arrangement;
        _widths = widths;
        _spacing = spacing;
        _sortedByDefault = sortedByDefault;

        Refresh();
    }

    public string Id { get; }

    /// <summary>The fields drawn, in order: leading control columns, the arranged fields, trailing control columns.</summary>
    public IReadOnlyList<TableField<TRow>> Shown { get; private set; }

    public TableLayout Layout { get; private set; }

    /// <summary>Raised after the sort, the arrangement or the arranging state changes.</summary>
    public event Action? Changed;

    /// <summary>The table has more than one column to arrange.</summary>
    public bool CanArrange => _arrangeable.Count > 1;

    /// <summary>The COLUMNS bar is open: headers choose rather than sort.</summary>
    public bool Arranging { get; private set; }

    /// <summary>The key of the column chosen in the bar; null when none is.</summary>
    public string? Chosen { get; private set; }

    /// <summary>The ADD A COLUMN panel is open.</summary>
    public bool Adding { get; private set; }

    /// <summary>The chosen column's label; null when none is chosen.</summary>
    public string? ChosenLabel => _arrangeable.FirstOrDefault(f => f.Key == Chosen)?.Label;

    /// <summary>The table shows its default columns.</summary>
    public bool IsDefault => ColumnArrangement.IsDefault(Columns, Stored);

    /// <summary>The chosen column can move one place to that side.</summary>
    public bool CanMove(bool left)
    {
        var at = ShownKeys.IndexOf(Chosen ?? string.Empty);

        return at >= 0 && (left ? at > 0 : at < ShownKeys.Count - 1);
    }

    /// <summary>The chosen column can be removed: it is not required and not the last shown.</summary>
    public bool CanRemove => Chosen is { } key && CanHide(key);

    /// <summary>
    /// Every column the table can show, for the ADD A COLUMN panel: the shown ones in table order, then the rest A to Z.
    /// A column that cannot be removed is <c>Locked</c>.
    /// </summary>
    public IReadOnlyList<(string Key, string Label, bool Shown, bool Locked)> Choices
    {
        get
        {
            var shown = ShownKeys;
            var byKey = _arrangeable.ToDictionary(f => f.Key, StringComparer.Ordinal);

            return
            [
                .. shown.Select(key => (key, byKey[key].Label!, true, !CanHide(key))),
                .. _arrangeable
                    .Where(f => !shown.Contains(f.Key))
                    .OrderBy(f => f.Label, StringComparer.CurrentCultureIgnoreCase)
                    .Select(f => (f.Key, f.Label!, false, false)),
            ];
        }
    }

    private IReadOnlyList<ArrangedColumn> Columns =>
        [.. _arrangeable.Select(f => new ArrangedColumn(f.Key, f.ByDefault, f.Required))];

    private IReadOnlyList<string>? Stored => _arrangement is { } memory ? memory.Remembered(Id) : _unstored;

    private List<string> ShownKeys => [.. ColumnArrangement.Shown(Columns, Stored)];

    /// <summary>Re-reads the arrangement and starts a new layout.</summary>
    [MemberNotNull(nameof(Shown), nameof(Layout), nameof(_head))]
    public void Refresh()
    {
        Shown = Arranged();
        (_drawn, _owners) = Drawn(Shown);
        Layout = new TableLayout(Id, [.. _drawn.Select(field => field.Column)], _widths, _spacing);
        _head = NewHead();
    }

    public TableGrid Header() =>
        Layout.Header([.. _drawn.Select((field, column) =>
            Arranging && field.Label is not null ? ChooseCell(field, _owners[column])
            : field.Label is null && field.Sort is null ? new Border()
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

    /// <summary>The control name of the choose cell for the drawn field <paramref name="key"/>.</summary>
    public string ChooseName(string key) => $"{_headName}.Choose.{key}";

    /// <summary>Opens the COLUMNS bar, or closes it with nothing chosen and the panel shut.</summary>
    public void ToggleArranging()
    {
        Arranging = !Arranging;
        Chosen = null;
        Adding = false;
        Changed?.Invoke();
    }

    /// <summary>Closes the COLUMNS bar if it is open.</summary>
    public void StopArranging()
    {
        if (Arranging)
        {
            ToggleArranging();
        }
    }

    /// <summary>Chooses the column, or clears the choice when it is already chosen.</summary>
    public void Choose(string key)
    {
        Chosen = Chosen == key ? null : key;
        Changed?.Invoke();
    }

    public void ToggleAdding()
    {
        Adding = !Adding;
        Changed?.Invoke();
    }

    /// <summary>Moves the chosen column one place to that side.</summary>
    public void Move(bool left)
    {
        if (Chosen is { } key && CanMove(left))
        {
            Store(ColumnArrangement.Moved(Columns, Stored, key, left));
        }
    }

    /// <summary>Removes the chosen column and clears the choice.</summary>
    public void Remove()
    {
        if (Chosen is { } key)
        {
            Hide(key);
        }
    }

    /// <summary>Shows the column at the right and chooses it.</summary>
    public void Show(string key)
    {
        Chosen = key;
        Store(ColumnArrangement.Added(Columns, Stored, key));
    }

    /// <summary>Removes the column unless it is required or the last shown; clears the choice if it was chosen.</summary>
    public void Hide(string key)
    {
        if (!CanHide(key))
        {
            return;
        }

        if (Chosen == key)
        {
            Chosen = null;
        }

        Store(ColumnArrangement.Removed(Columns, Stored, key));
    }

    /// <summary>Forgets the arrangement, so the table shows its default columns; dragged widths are kept.</summary>
    public void Default()
    {
        if (_arrangement is { } memory)
        {
            memory.Forget(Id);
        }
        else
        {
            _unstored = null;
        }

        Chosen = null;
        Refresh();
        Changed?.Invoke();
    }

    private bool CanHide(string key)
    {
        var shown = ShownKeys;

        return shown.Contains(key) && shown.Count > 1 && !_arrangeable.Any(field => field.Key == key && field.Required);
    }

    private void Store(IReadOnlyList<string> keys)
    {
        if (_arrangement is { } memory)
        {
            memory.Remember(Id, keys);
        }
        else
        {
            _unstored = keys;
        }

        Refresh();
        Changed?.Invoke();
    }

    private Button ChooseCell(TableField<TRow> field, string owner)
    {
        const double Inset = 6;

        // Neighbouring cells are a tile gap apart, whatever the column spacing.
        var bleed = (_spacing - Gaps.Tile) / 2;
        var chosen = owner == Chosen;
        var ink = chosen ? ThemeManager.KnockKey : ThemeManager.GreyKey;
        var label = SortHeader<TRow>.Line(field.Label!, TypeScale.Meta, FontWeight.Medium, ink);
        label.HorizontalAlignment = field.Alignment;

        Control content = label;

        if ((field.Second ?? field.Sort?.Second) is { } second)
        {
            var under = SortHeader<TRow>.Line(second, TypeScale.MetaSmall, FontWeight.Normal, ink);
            under.HorizontalAlignment = field.Alignment;
            content = new StackPanel { Children = { label, under } };
        }

        var button = new Button
        {
            Name = ChooseName(field.Key),
            Theme = Application.Current?.FindResource("D47.SortHeader") as ControlTheme,
            Content = content,
            MinHeight = TypeScale.MinimumTarget,
            Margin = new Thickness(-bleed, 0),
            Padding = new Thickness(Inset, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = field.Alignment,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        ThemeInk.Bind(button, Button.BackgroundProperty, chosen ? ThemeManager.AKey : ThemeManager.TileKey);
        Avalonia.Automation.AutomationProperties.SetName(button, $"Choose {field.Label}");
        button.Click += (_, _) => Choose(owner);

        return button;
    }

    private IReadOnlyList<TableField<TRow>> Arranged()
    {
        var byKey = _arrangeable.ToDictionary(field => field.Key, StringComparer.Ordinal);
        var keys = ColumnArrangement.Shown(Columns, Stored);

        var first = _arrangeable.Count == 0 ? _fields.Count : _fields.ToList().IndexOf(_arrangeable[0]);

        return
        [
            .. _fields.Take(first).Where(field => field.Label is null),
            .. keys.Select(key => byKey[key]),
            .. _fields.Skip(first).Where(field => field.Label is null),
        ];
    }

    private static (List<TableField<TRow>> Drawn, List<string> Owners) Drawn(IReadOnlyList<TableField<TRow>> shown) =>
        ([.. shown.SelectMany(field => field.More.Prepend(field))],
         [.. shown.SelectMany(field => field.More.Prepend(field).Select(_ => field.Key))]);

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
