using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using D47.App.Controls;
using D47.App.Theming;

namespace D47.App.Panel;

/// <summary>Fields and frames shared by the Credits and Codex tables.</summary>
internal static class LedgerTables
{
    public const double RowInset = 14;

    /// <summary>A control column: not arranged, drawn at the edge it is declared at. Its header is <paramref name="head"/>'s, or empty.</summary>
    public static TableField<TRow> Control<TRow>(TableColumn column, Func<TRow, Control> cell, SortColumn<TRow>? head = null, bool right = false) =>
        new(column.Key, column, row => Centred(cell(row)))
        {
            Sort = head,
            Alignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };

    /// <summary>A field the Commander can add; it is not shown until they do.</summary>
    public static TableField<TRow> Added<TRow>(string key, TableColumn column, Func<TRow, Control> cell, SortColumn<TRow> sort) =>
        new(key, column, row => Centred(cell(row))) { Label = sort.Label, Sort = sort, Filters = [], ByDefault = false };

    /// <summary>The table's header, as tall as a press target and inset to the rows' padding.</summary>
    public static TableGrid Head<TRow>(ArrangedTable<TRow> columns, Thickness margin)
    {
        var header = columns.Header();
        header.MinHeight = TypeScale.MinimumTarget;
        header.Margin = margin;

        return header;
    }

    /// <summary>
    /// The COLUMNS tile on a line of its own, then its bar when open. Given the table's <paramref name="rows"/>, before
    /// the filter, the line also carries the filter's head line and FILTER, and the filter bar shows when open.
    /// </summary>
    public static StackPanel Arranger<TRow>(
        ArrangedTable<TRow> columns, IReadOnlyList<TRow>? rows = null, string? sortedBy = null, PanelPrompts? prompts = null)
    {
        var top = new StackPanel { Spacing = Gaps.Tile };

        if (ColumnArranger.Line(columns, rows, sortedBy) is { } tile)
        {
            top.Children.Add(tile);
        }

        if (rows is not null && ColumnFilter.Bar(columns, rows, prompts: prompts) is { } filter)
        {
            top.Children.Add(filter);
        }

        if (ColumnArranger.Bar(columns) is { } bar)
        {
            top.Children.Add(bar);
        }

        return top;
    }

    /// <summary>On the desktop a table wider than the page scrolls sideways, header and rows together.</summary>
    public static Control Sideways<TRow>(ArrangedTable<TRow> columns, Control table) =>
        columns.Headset ? table : new TableScroll(columns.Layout, table);

    private static Control Centred(Control cell)
    {
        cell.VerticalAlignment = VerticalAlignment.Center;

        return cell;
    }
}

/// <summary>
/// The lines above a table, its header, and its scrolled list of rows. The list, its scroller and the header are
/// built again when the table's layout changes; a redraw that leaves the layout and the items alone leaves the list
/// and its scroll position alone too.
/// </summary>
internal sealed class ArrangedList(bool headset, string listName, Func<object, Control?> row, Action? replaced = null)
{
    private TableLayout? _built;
    private Grid? _grid;
    private ScrollViewer? _scroll;
    private ItemsControl? _list;
    private TableGrid? _header;
    private Avalonia.Controls.Panel? _headParent;
    private double _barWidth;
    private IReadOnlyList<object> _shown = [];

    /// <summary>The page's own lines; the page clears and fills it on every draw.</summary>
    public StackPanel Top { get; } = new();

    public Border View { get; } = new();

    /// <summary>Makes the next <see cref="Show"/> draw its rows again even when the items are the ones shown.</summary>
    public void Forget() => _shown = [];

    /// <summary>Shows <paramref name="items"/> under <see cref="Top"/>; with none, shows the list empty and no header.</summary>
    public void Show<TRow>(ArrangedTable<TRow> columns, IReadOnlyList<object> items, Thickness margin)
    {
        if (!ReferenceEquals(_built, columns.Layout))
        {
            Build(columns, margin);
        }

        var any = items.Count > 0;

        if (_headParent is { } parent && _header is { } header && parent.Children.Contains(header) != any)
        {
            if (any)
            {
                parent.Children.Add(header);
            }
            else
            {
                parent.Children.Remove(header);
            }
        }

        _grid!.Children[1].IsVisible = any;
        _scroll!.IsVisible = any && !ColumnFilter.HidesRows(columns);

        if (any && _shown.SequenceEqual(items))
        {
            return;
        }

        _shown = items;
        replaced?.Invoke();
        _list!.ItemsSource = items;
    }

    private void Build<TRow>(ArrangedTable<TRow> columns, Thickness margin)
    {
        if (Top.Parent is Avalonia.Controls.Panel old)
        {
            old.Children.Remove(Top);
        }

        var shell = new StackPanel { Children = { Top } };
        var header = LedgerTables.Head(columns, margin);

        var list = _list = new ItemsControl
        {
            Name = listName,
            ItemsPanel = new FuncTemplate<Avalonia.Controls.Panel?>(() => new VirtualizingStackPanel()),
            ItemTemplate = new FuncDataTemplate<object>((item, _) => item is null ? null : row(item)),
        };

        _grid = LoadoutPages.HeaderAndList(shell, header, list, headset ? null : columns.Layout, out var scroll);
        _scroll = scroll;
        _header = header;
        _headParent = header.Parent as Avalonia.Controls.Panel;
        _shown = [];
        _built = columns.Layout;

        if (headset)
        {
            Follow(header, scroll, margin);
        }

        View.Child = _grid;
    }

    /// <summary>In the headset the header is kept as wide as the rows, which sit beside the scroller's bar; the bar always shows, so its width never changes the rows' width.</summary>
    private void Follow(TableGrid header, ScrollViewer rows, Thickness margin)
    {
        rows.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;

        void Apply()
        {
            if (rows.Bounds.Width > 0)
            {
                _barWidth = rows.Bounds.Width - rows.Viewport.Width;
            }

            header.Margin = new Thickness(margin.Left, margin.Top, margin.Right + _barWidth, margin.Bottom);
        }

        Apply();
        rows.ScrollChanged += (_, _) => Apply();
    }
}
