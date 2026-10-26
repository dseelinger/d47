using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Panel;

namespace D47.App.Theming;

/// <summary>
/// The column model of one drawn table, shared by its header and its rows. Rows and the header are
/// <see cref="TableGrid"/>s registered with it; each reports its cells' natural widths and takes its
/// column widths from <see cref="ColumnLayout.Widths"/>. A table built with a <see cref="ColumnWidthMemory"/>
/// gets a grip on each header boundary.
/// </summary>
public sealed class TableLayout
{
    private const double GripWidth = 12;

    private readonly List<WeakReference<TableGrid>> _grids = [];
    private readonly ColumnWidthMemory? _memory;
    private readonly double[] _natural;
    private readonly double[] _headerWidth;
    private readonly Dictionary<string, double> _chosen;
    private readonly double[] _widths;
    private double _available;

    public TableLayout(string id, IReadOnlyList<TableColumn> columns, ColumnWidthMemory? memory = null, double spacing = 0)
    {
        Id = id;
        Columns = columns;
        Spacing = spacing;
        _memory = memory;
        _natural = new double[columns.Count];
        _headerWidth = new double[columns.Count];
        _widths = new double[columns.Count];
        _chosen = memory is null ? [] : new Dictionary<string, double>(memory.Remembered(id), StringComparer.Ordinal);
    }

    public string Id { get; }

    public IReadOnlyList<TableColumn> Columns { get; }

    public double Spacing { get; }

    /// <summary>The widths currently applied to the columns.</summary>
    public IReadOnlyList<double> Widths => _widths;

    /// <summary>The widths the Commander has chosen, by column key.</summary>
    public IReadOnlyDictionary<string, double> Chosen => _chosen;

    public bool Resizable => _memory is not null;

    /// <summary>The control name of the grip after column <paramref name="key"/> of table <paramref name="id"/>.</summary>
    public static string GripName(string id, string key) => $"{id}.{key}.Grip";

    /// <summary>Builds the header row from ready-made cells, one per column.</summary>
    public TableGrid Header(IReadOnlyList<Control> cells) => Build(cells, header: true);

    /// <summary>Builds the header row from <paramref name="sort"/>'s cells, left-aligned unless <paramref name="alignments"/> says otherwise.</summary>
    public TableGrid Header<TRow>(SortHeader<TRow> sort, IReadOnlyList<HorizontalAlignment>? alignments = null) =>
        Header([.. Columns.Select((_, column) => sort.Cell(column, alignments?[column] ?? HorizontalAlignment.Left))]);

    /// <summary>Builds a data row from cells, one per column.</summary>
    public TableGrid Row(IReadOnlyList<Control> cells) => Build(cells, header: false);

    internal double MinimumOf(int column) =>
        Columns[column].Fit == ColumnFit.Fixed ? Columns[column].Fixed : Columns[column].Min ?? _headerWidth[column];

    internal void Compute(double available)
    {
        var spacing = Spacing * Math.Max(0, Columns.Count - 1);

        if (!double.IsFinite(available))
        {
            available = Enumerable.Range(0, Columns.Count).Sum(NaturalOf) + spacing;
        }

        _available = available;

        var minimum = Enumerable.Range(0, Columns.Count).Select(MinimumOf).ToList();
        var widths = ColumnLayout.Widths(Columns, _natural, minimum, _chosen, Math.Max(0, available - spacing));

        for (var i = 0; i < widths.Count; i++)
        {
            _widths[i] = widths[i];
        }
    }

    internal void Report(TableGrid grid, double[] natural, bool header)
    {
        var grew = false;

        for (var i = 0; i < natural.Length; i++)
        {
            if (header && natural[i] > _headerWidth[i])
            {
                _headerWidth[i] = natural[i];
                grew = true;
            }

            if (natural[i] > _natural[i])
            {
                _natural[i] = natural[i];
                grew = true;
            }
        }

        if (grew)
        {
            Invalidate(except: grid);
        }
    }

    private double NaturalOf(int column) =>
        Columns[column].Fit == ColumnFit.Fixed ? Columns[column].Fixed : _natural[column];

    private TableGrid Build(IReadOnlyList<Control> cells, bool header)
    {
        var grid = new TableGrid(this, header) { ColumnSpacing = Spacing };

        for (var i = 0; i < Columns.Count && i < cells.Count; i++)
        {
            Grid.SetColumn(cells[i], i);
            grid.Children.Add(cells[i]);
        }

        if (header && _memory is not null)
        {
            for (var i = 0; i < Columns.Count - 1; i++)
            {
                if (Columns[i].Fit != ColumnFit.Fixed)
                {
                    AddGrip(grid, i);
                }
            }
        }

        _grids.Add(new WeakReference<TableGrid>(grid));

        return grid;
    }

    private void Invalidate(TableGrid? except = null)
    {
        _grids.RemoveAll(reference => !reference.TryGetTarget(out _));

        foreach (var reference in _grids)
        {
            if (reference.TryGetTarget(out var grid) && grid != except)
            {
                grid.InvalidateMeasure();
            }
        }
    }

    private void AddGrip(TableGrid header, int column)
    {
        var grip = new ColumnGrip
        {
            Name = GripName(Id, Columns[column].Key),
            Width = GripWidth,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, -(GripWidth / 2) - (Spacing / 2), 0),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            ZIndex = 1,
        };

        Grid.SetColumn(grip, column);

        var startX = 0.0;
        var startWidth = 0.0;
        var dragging = false;

        grip.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)
            {
                return;
            }

            e.Handled = true;

            if (e.ClickCount >= 2)
            {
                _chosen.Remove(Columns[column].Key);
                _memory!.Forget(Id, Columns[column].Key);
                Reflow(header);
                return;
            }

            dragging = true;
            startX = e.GetPosition(header).X;
            startWidth = _widths[column];
            e.Pointer.Capture(grip);
        };

        grip.PointerMoved += (_, e) =>
        {
            if (!dragging)
            {
                return;
            }

            e.Handled = true;
            _chosen[Columns[column].Key] = Clamp(column, startWidth + e.GetPosition(header).X - startX);
            Reflow(header);
        };

        grip.PointerReleased += (_, e) =>
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            e.Pointer.Capture(null);

            if (_chosen.TryGetValue(Columns[column].Key, out var width))
            {
                _memory!.Remember(Id, Columns[column].Key, width);
            }
        };

        header.Children.Add(grip);
    }

    private double Clamp(int column, double width)
    {
        var others = Enumerable.Range(0, Columns.Count).Where(c => c != column).Sum(MinimumOf);
        var room = _available - (Spacing * Math.Max(0, Columns.Count - 1)) - others;

        return Math.Max(MinimumOf(column), Math.Min(width, room));
    }

    private void Reflow(TableGrid header)
    {
        header.InvalidateMeasure();
        Invalidate();
    }
}

/// <summary>The drag handle on a header boundary; not a cell.</summary>
public sealed class ColumnGrip : Border;

/// <summary>A header or row of a <see cref="TableLayout"/>: a <see cref="Grid"/> whose column widths come from the table.</summary>
public sealed class TableGrid : Grid
{
    /// <summary>A natural width a cell supplies when its measured width is not what it needs.</summary>
    public static readonly AttachedProperty<double> NaturalWidthProperty =
        AvaloniaProperty.RegisterAttached<TableGrid, Control, double>("NaturalWidth", double.NaN);

    internal TableGrid(TableLayout layout, bool header)
    {
        Layout = layout;
        IsHeader = header;

        foreach (var _ in layout.Columns)
        {
            ColumnDefinitions.Add(new ColumnDefinition(0, GridUnitType.Pixel));
        }
    }

    public TableLayout Layout { get; }

    public bool IsHeader { get; }

    public static double GetNaturalWidth(Control control) => control.GetValue(NaturalWidthProperty);

    public static void SetNaturalWidth(Control control, double value) => control.SetValue(NaturalWidthProperty, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        var natural = new double[Layout.Columns.Count];

        foreach (var child in Children)
        {
            var column = GetColumn(child);

            if (child is ColumnGrip || GetColumnSpan(child) != 1 || column < 0 || column >= natural.Length)
            {
                continue;
            }

            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));

            var width = child.DesiredSize.Width;
            var supplied = GetNaturalWidth(child);
            natural[column] = Math.Max(natural[column], double.IsNaN(supplied) ? width : Math.Max(supplied, width));
        }

        Layout.Report(this, natural, IsHeader);
        Layout.Compute(availableSize.Width);

        for (var i = 0; i < natural.Length; i++)
        {
            ColumnDefinitions[i].Width = new GridLength(Layout.Widths[i], GridUnitType.Pixel);
        }

        return base.MeasureOverride(availableSize);
    }
}
