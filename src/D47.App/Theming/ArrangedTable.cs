using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Configuration;

namespace D47.App.Theming;

/// <summary>
/// A table drawn from its fields in the order its stored arrangement names. Control columns stay at the edge
/// they are declared at. The sort is kept by field key, so a moved column keeps it and a column left out drops it. It is also
/// stored with the arrangement memory, apart for the headset, unless the rows are sorted elsewhere.
/// Every header and row built between two calls of <see cref="Refresh"/> shares one <see cref="Layout"/>.
/// While <see cref="Arranging"/>, a header press chooses a column instead of sorting, and every edit is stored as it is made.
/// In the headset each column's minimum is its header's width, and the arranged columns that do not fit the table's width
/// at their minimums are left out from the right.
/// </summary>
public sealed class ArrangedTable<TRow>
{
    private const double ChooseInset = 6;

    /// <summary>Text as wide as a filtered header's counts, measured into a filterable column's minimum.</summary>
    private const string MarkRoom = " 00/00";

    private readonly string _headName;
    private readonly IReadOnlyList<TableField<TRow>> _fields;
    private readonly IReadOnlyList<TableField<TRow>> _arrangeable;
    private readonly ColumnArrangementMemory? _arrangement;
    private readonly ColumnWidthMemory? _widths;
    private readonly double _spacing;
    private readonly string? _sortedByDefault;
    private readonly bool _remembers;
    private IReadOnlyList<string>? _unstored;
    private (string Key, SortDirection Direction)? _sort;
    private IReadOnlyList<TableField<TRow>> _drawn = [];
    private IReadOnlyList<string> _owners = [];
    private SortHeader<TRow> _head;
    private readonly Dictionary<string, double> _floors = new(StringComparer.Ordinal);
    private double? _room;
    private readonly RowFilter<TRow> _filter;

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

        RequireSorts(id, fields);

        _filter = new RowFilter<TRow>(fields
            .SelectMany(field => field.More.Prepend(field).Select(part => (Owner: field, Part: part)))
            .Where(pair => pair.Part.Filters is { Count: > 0 })
            .Select(pair => (pair.Part.Key, pair.Owner.Label ?? pair.Part.Label ?? pair.Part.Key, pair.Part.Filters!)));

        _remembers = _arrangeable.All(field => field.Sort is { IsRemote: false });

        if (_remembers)
        {
            _sort = _arrangement?.Sorts.Remembered(id);
        }

        Refresh();
    }

    public string Id { get; }

    /// <summary>The fields drawn, in order: leading control columns, the arranged fields, trailing control columns.</summary>
    public IReadOnlyList<TableField<TRow>> ShownFields { get; private set; }

    public TableLayout Layout { get; private set; }

    /// <summary>Raised after the sort, the arrangement or the arranging state changes.</summary>
    public event Action? Changed;

    /// <summary>Raised when a header press changes the sort, before <see cref="Changed"/>; not raised by <see cref="ShowSort"/>.</summary>
    public event Action? Sorted;

    /// <summary>Raised when a tick changes which rows the filter shows, before <see cref="Changed"/>.</summary>
    public event Action? FilterChanged;

    /// <summary>The key of the sorted field and its direction; null in the table's default order.</summary>
    public (string Key, SortDirection Direction)? SortedBy => _sort;

    /// <summary>The table is drawn in the headset, which does not scroll sideways.</summary>
    public bool Headset => _arrangement?.Headset == true;

    /// <summary>The table has more than one column to arrange.</summary>
    public bool CanArrange => _arrangeable.Count > 1;

    /// <summary>The COLUMNS bar is open: headers choose rather than sort.</summary>
    public bool Arranging { get; private set; }

    /// <summary>The key of the column chosen in the bar; null when none is.</summary>
    public string? Chosen { get; private set; }

    /// <summary>The filter bar is open. Opening it closes the COLUMNS bar and the reverse.</summary>
    public bool Filtering { get; private set; }

    /// <summary>The key of the field whose values are listed; null when none is.</summary>
    public string? FilterChosen { get; private set; }

    /// <summary>Some field declares a value list.</summary>
    public bool CanFilter => _filter.Any;

    /// <summary>The ADD A COLUMN panel is open.</summary>
    public bool Adding { get; private set; }

    /// <summary>The chosen column's label; null when none is chosen.</summary>
    public string? ChosenLabel => _arrangeable.FirstOrDefault(f => f.Key == Chosen) is { } chosen ? chosen.Name ?? chosen.Label : null;

    /// <summary>The table shows its default columns.</summary>
    public bool IsDefault => ColumnArrangement.IsDefault(Columns, Stored);

    /// <summary>The chosen column can move one place to that side.</summary>
    public bool CanMove(bool left)
    {
        var drawn = ShownFields.Where(field => field.Label is not null).Select(field => field.Key).ToList();
        var at = drawn.IndexOf(Chosen ?? string.Empty);

        return at >= 0 && (left ? at > 0 : at < drawn.Count - 1);
    }

    /// <summary>The column is shown, or there is room to draw it after the drawn columns.</summary>
    public bool CanAdd(string key) =>
        !Headset
        || _room is not { } room
        || ShownKeys.Contains(key)
        || _arrangeable.FirstOrDefault(field => field.Key == key) is not { } field
        || Need([.. ShownFields, field]) <= room;

    /// <summary>In the headset, no column that is not shown has room to be drawn.</summary>
    public bool NoRoom
    {
        get
        {
            if (!Headset || _room is null)
            {
                return false;
            }

            var hidden = Choices.Where(choice => !choice.Shown).ToList();

            return hidden.Count > 0 && hidden.All(choice => !CanAdd(choice.Key));
        }
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
                .. shown.Select(key => (key, byKey[key].Name ?? byKey[key].Label!, true, !CanHide(key))),
                .. _arrangeable
                    .Where(f => !shown.Contains(f.Key))
                    .OrderBy(f => f.Name ?? f.Label, StringComparer.CurrentCultureIgnoreCase)
                    .Select(f => (f.Key, f.Name ?? f.Label!, false, false)),
            ];
        }
    }

    private IReadOnlyList<ArrangedColumn> Columns =>
        [.. _arrangeable.Select(f => new ArrangedColumn(f.Key, f.ByDefault, f.Required))];

    private IReadOnlyList<string>? Stored => _arrangement is { } memory ? memory.Remembered(Id) : _unstored;

    private List<string> ShownKeys => [.. ColumnArrangement.Shown(Columns, Stored)];

    /// <summary>Re-reads the arrangement and starts a new layout.</summary>
    [MemberNotNull(nameof(ShownFields), nameof(Layout), nameof(_head))]
    public void Refresh()
    {
        ShownFields = Fitted(Arranged());
        (_drawn, _owners) = Drawn(ShownFields);

        var layout = new TableLayout(Id, [.. _drawn.Select(ColumnOf)], _widths, _spacing);
        layout.Sized += width => Resized(layout, width);
        Layout = layout;
        _head = NewHead();
    }

    /// <summary>
    /// The header: sorting cells, or choose cells while either bar is open. While <see cref="Filtering"/>, a field with
    /// no value list draws a disabled cell. A filtered field's label is in A with its shown and listed counts.
    /// </summary>
    public TableGrid Header() =>
        Layout.Header([.. _drawn.Select((field, column) =>
            Arranging && field.Label is not null ? ChooseCell(field, _owners[column])
            : Filtering && (field.Label is not null || field.Sort is not null) ? FilterCell(field)
            : field.Label is null && field.Sort is null ? new Border()
            : _head.Cell(column, field.Alignment, Mark(field.Key), field.Label))]);

    public TableGrid Row(TRow row) => Layout.Row([.. _drawn.Select(field => field.Cell(row))]);

    /// <summary><paramref name="rows"/>, given in the default order, sorted by the chosen sort.</summary>
    public IReadOnlyList<TRow> Sort(IReadOnlyList<TRow> rows) => _head.Sort(rows);

    /// <summary>The rows no unticked value hides, in the order given.</summary>
    public IReadOnlyList<TRow> Filter(IReadOnlyList<TRow> rows) => _filter.Filter(rows);

    /// <summary>The rows the filter lets through, sorted by the chosen sort.</summary>
    public IReadOnlyList<TRow> Shown(IReadOnlyList<TRow> rows) => Sort(Filter(rows));

    /// <summary>How many of <paramref name="rows"/> the filter hides.</summary>
    public int Hidden(IReadOnlyList<TRow> rows) => rows.Count - Filter(rows).Count;

    /// <summary>
    /// Records the values the rows carry and how many rows carry each. A value listed once keeps its place; a new one goes
    /// at the end of its block, ticked.
    /// </summary>
    public void Observe(IReadOnlyList<TRow> rows) => _filter.Observe(rows);

    /// <summary>Every block of the declared value lists, in table order.</summary>
    public IReadOnlyList<FilterBlock> FilterBlocks => _filter.Blocks;

    /// <summary>The labels of the fields with at least one unticked value.</summary>
    public IReadOnlyList<string> FilteredLabels => _filter.FilteredLabels;

    /// <summary>A block of the field lists more than 20 values.</summary>
    public bool Many(string key) => _filter.Many(key);

    /// <summary>The field declares a value list.</summary>
    public bool HasValues(string key) => _filter.Has(key);

    /// <summary>
    /// The values ticked over the values listed in the field's blocks that have an unticked value; null when none has.
    /// </summary>
    public (int Shown, int Listed)? Mark(string key)
    {
        var filtered = FilterBlocks.Where(block => block.FieldKey == key && block.Filtered).ToList();

        return filtered.Count == 0
            ? null
            : (filtered.Sum(block => block.Values.Count(value => value.Ticked)), filtered.Sum(block => block.Values.Count));
    }

    /// <summary>
    /// Opens the values of a field with more than 20 in a block on a page of their own; while set,
    /// <see cref="ChooseFilter"/> calls it for such a field instead of choosing it.
    /// </summary>
    public Action<string>? PickMany { get; set; }

    /// <summary>The control name of the filter bar's header cell for the drawn field <paramref name="key"/>.</summary>
    public string FilterName(string key) => $"{_headName}.Filter.{key}";

    /// <summary>Ticks every value of every block, keeping each value's place in its block.</summary>
    public void TickEveryValue() => Filtered(_filter.TickEvery());

    /// <summary>Sets every value of the field's blocks to <paramref name="ticked"/>'s answer, raising one change.</summary>
    public void SetTicked(string key, Func<int, string, bool> ticked)
    {
        var changed = false;

        foreach (var block in FilterBlocks.Where(block => block.FieldKey == key))
        {
            foreach (var value in block.Values)
            {
                changed |= _filter.Set(key, block.Index, value.Value, ticked(block.Index, value.Value));
            }
        }

        Filtered(changed);
    }

    public void Tick(string key, int block, string value) => Filtered(_filter.Set(key, block, value, true));

    public void Untick(string key, int block, string value) => Filtered(_filter.Set(key, block, value, false));

    public void TickAll(string key, int block) => Filtered(_filter.SetAll(key, block, true));

    public void UntickAll(string key, int block) => Filtered(_filter.SetAll(key, block, false));

    /// <summary>
    /// Ticks every value of every block and forgets the values seen, for a new answer. Raises no change: the caller is
    /// drawing the new answer.
    /// </summary>
    public void ClearFilters() => _filter.Clear();

    /// <summary>Opens the filter bar, or closes it with nothing chosen. Opening it closes the COLUMNS bar.</summary>
    public void ToggleFiltering()
    {
        Filtering = !Filtering;
        FilterChosen = null;

        if (Filtering)
        {
            Arranging = false;
            Chosen = null;
            Adding = false;
        }

        Changed?.Invoke();
    }

    /// <summary>Chooses the field whose values are listed, or clears the choice when it is already chosen.</summary>
    public void ChooseFilter(string key)
    {
        if (FilterChosen != key && Many(key) && PickMany is { } pick)
        {
            pick(key);

            return;
        }

        FilterChosen = FilterChosen == key ? null : key;
        Changed?.Invoke();
    }

    private void Filtered(bool changed)
    {
        if (changed)
        {
            FilterChanged?.Invoke();
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Shows a sort held elsewhere, such as a service's, without raising any event. Returns false, and shows no sort,
    /// when the field is not drawn or does not sort.
    /// </summary>
    public bool ShowSort(string? key, SortDirection direction)
    {
        if (SortedAt(key) is not { } column)
        {
            _sort = null;
            _head.Show(null);

            return false;
        }

        _sort = (key!, direction);
        _head.Show(new SortOrder(column, direction));

        return true;
    }

    /// <summary>The control name of the choose cell for the drawn field <paramref name="key"/>.</summary>
    public string ChooseName(string key) => $"{_headName}.Choose.{key}";

    /// <summary>Opens the COLUMNS bar, or closes it with nothing chosen and the panel shut.</summary>
    public void ToggleArranging()
    {
        Arranging = !Arranging;
        Chosen = null;
        Adding = false;

        if (Arranging)
        {
            Filtering = false;
            FilterChosen = null;
        }

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

    /// <summary>Closes the COLUMNS and FILTER bars and forgets the filter, for a page opened again; the columns and sort stay.</summary>
    public void Reopen()
    {
        StopArranging();
        ClearFilters();

        if (Filtering)
        {
            ToggleFiltering();
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

    /// <summary>Shows the column at the right and chooses it, when there is room for it.</summary>
    public void Show(string key)
    {
        if (!CanAdd(key))
        {
            return;
        }

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

        if (NoRoom)
        {
            Adding = false;
        }

        Changed?.Invoke();
    }

    private Button ChooseCell(TableField<TRow> field, string owner)
    {
        var bleed = Bleed;
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
            Padding = new Thickness(ChooseInset, 0),
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

    private Button FilterCell(TableField<TRow> field)
    {
        var name = field.Label ?? field.Sort!.Label;
        var values = HasValues(field.Key);
        var chosen = values && FilterChosen == field.Key;
        var mark = values ? Mark(field.Key) : null;
        var ink = !values ? ThemeManager.Grey2Key : chosen ? ThemeManager.KnockKey : mark is null ? ThemeManager.GreyKey : ThemeManager.AKey;
        var label = SortHeader<TRow>.Line(name, TypeScale.Meta, FontWeight.Medium, ink);
        label.HorizontalAlignment = field.Alignment;

        if (mark is { } counts)
        {
            SortHeader<TRow>.AddMark(label, counts);
        }

        Control content = label;

        if ((field.Second ?? field.Sort?.Second) is { } second)
        {
            var under = SortHeader<TRow>.Line(
                second, TypeScale.MetaSmall, FontWeight.Normal,
                !values ? ThemeManager.Grey2Key : chosen ? ThemeManager.KnockKey : ThemeManager.GreyKey);
            under.HorizontalAlignment = field.Alignment;
            content = new StackPanel { Children = { label, under } };
        }

        var bleed = Bleed;
        var button = new Button
        {
            Name = FilterName(field.Key),
            Theme = Application.Current?.FindResource("D47.SortHeader") as ControlTheme,
            Content = content,
            MinHeight = TypeScale.MinimumTarget,
            Margin = new Thickness(-bleed, 0),
            Padding = new Thickness(ChooseInset, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = field.Alignment,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEnabled = values,
        };

        if (values)
        {
            ThemeInk.Bind(button, Button.BackgroundProperty, chosen ? ThemeManager.AKey : ThemeManager.TileKey);
            button.Click += (_, _) => ChooseFilter(field.Key);
        }

        var shown = mark is { } marked ? SortHeader<TRow>.MarkWords(marked) : null;

        Avalonia.Automation.AutomationProperties.SetName(
            button,
            !values ? $"{name}: not filtered"
            : chosen ? shown is null ? $"{name}, chosen" : $"{name}, chosen, {shown}"
            : shown is null ? $"Filter by {name}" : $"{name}, filtered: {shown}");

        return button;
    }

    /// <summary>Neighbouring choose cells are a tile gap apart, whatever the column spacing.</summary>
    private double Bleed => (_spacing - Gaps.Tile) / 2;

    /// <summary>The field's column; in the headset its minimum is its header's width, the same with the bar open or closed.</summary>
    private TableColumn ColumnOf(TableField<TRow> field) =>
        Headset && field.Column.Fit != ColumnFit.Fixed && field.Column.Min is null
            ? field.Column with { Min = Floor(field) }
            : field.Column;

    /// <summary>The narrowest the field's column goes: its set width or minimum, else the wider of its header cells, sorting and choosing.</summary>
    private double Floor(TableField<TRow> field)
    {
        if (field.Column.Fit == ColumnFit.Fixed)
        {
            return field.Column.Fixed;
        }

        if (field.Column.Min is { } min)
        {
            return min;
        }

        if (_floors.TryGetValue(field.Key, out var known))
        {
            return known;
        }

        var second = field.Second ?? field.Sort?.Second;
        var under = second is null ? 0 : Measure(second, TypeScale.MetaSmall, FontWeight.Normal);
        var width = 0.0;

        if (field.Sort is { } sort)
        {
            width = Math.Max(Measure(sort.Label + (sort.Sortable ? " ▲" : string.Empty) + (HasValues(field.Key) ? MarkRoom : string.Empty), TypeScale.Meta, FontWeight.Medium), under);
        }

        if (field.Label is { } label)
        {
            var text = Math.Max(Measure(label + (HasValues(field.Key) ? MarkRoom : string.Empty), TypeScale.Meta, FontWeight.Medium), under);
            width = Math.Max(width, Math.Max(text, text + (2 * ChooseInset) - (2 * Bleed)));
        }

        return _floors[field.Key] = Math.Ceiling(width);
    }

    private static double Measure(string text, double size, FontWeight weight)
    {
        var line = SortHeader<TRow>.Line(text, size, weight, ThemeManager.GreyKey);
        line.Measure(Size.Infinity);

        return line.DesiredSize.Width;
    }

    /// <summary>The width the fields' columns need at their minimums, with the spacing between them.</summary>
    private double Need(IReadOnlyList<TableField<TRow>> fields)
    {
        var columns = Drawn(fields).Drawn;

        return columns.Sum(Floor) + (_spacing * Math.Max(0, columns.Count - 1));
    }

    /// <summary>In the headset, the fields less the arranged ones at the right that do not fit; one arranged field always stays.</summary>
    private List<TableField<TRow>> Fitted(IReadOnlyList<TableField<TRow>> fields)
    {
        var fitted = fields.ToList();

        if (!Headset || _room is not { } room)
        {
            return fitted;
        }

        while (fitted.Count(field => field.Label is not null) > 1 && Need(fitted) > room)
        {
            fitted.RemoveAt(fitted.FindLastIndex(field => field.Label is not null));
        }

        return fitted;
    }

    /// <summary>In the headset, takes the table's new width, and redraws when that changes the columns drawn or the bar is open.</summary>
    private void Resized(TableLayout layout, double width)
    {
        if (!Headset || layout != Layout || (_room is { } room && Math.Abs(room - width) <= 0.5))
        {
            return;
        }

        _room = width;

        if (Arranging || Fitted(Arranged()).Count != ShownFields.Count)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (layout == Layout)
                {
                    Changed?.Invoke();
                }
            });
        }
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
            Remember();
            Sorted?.Invoke();
            Changed?.Invoke();
        };

        return head;
    }

    private void Remember()
    {
        if (_remembers)
        {
            _arrangement?.Sorts.Remember(Id, _sort);
        }
    }

    private static void RequireSorts(string table, IReadOnlyList<TableField<TRow>> fields)
    {
        foreach (var field in fields.SelectMany(field => field.More.Prepend(field)))
        {
            if (field.Label is not null && field.Sort is not { Sortable: true })
            {
                throw new ArgumentException($"Table {table}: the labelled field {field.Key} has no sort that presses.", nameof(fields));
            }
        }
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
