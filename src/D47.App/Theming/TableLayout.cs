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
/// gets a grip after each column that is not fixed and has another such column to its right; dragging it
/// moves width between those two columns.
/// </summary>
public sealed class TableLayout
{
    private const double GripWidth = 12;

    private readonly List<WeakReference<TableGrid>> _grids = [];
    private readonly ColumnWidthMemory? _memory;
    private readonly double[] _natural;
    private readonly double[] _headerWidth;
    private readonly double[] _narrowest;
    private readonly Dictionary<string, double> _chosen;
    private readonly double[] _widths;
    private double _available;
    private double? _sized;

    public TableLayout(string id, IReadOnlyList<TableColumn> columns, ColumnWidthMemory? memory = null, double spacing = 0)
    {
        Id = id;
        Columns = columns;
        Spacing = spacing;
        _memory = memory;
        _natural = new double[columns.Count];
        _headerWidth = new double[columns.Count];
        _narrowest = new double[columns.Count];
        _widths = new double[columns.Count];
        _chosen = memory is null ? [] : new Dictionary<string, double>(memory.Remembered(id), StringComparer.Ordinal);
    }

    public string Id { get; }

    public IReadOnlyList<TableColumn> Columns { get; }

    public double Spacing { get; }

    /// <summary>The widths currently applied to the columns.</summary>
    public IReadOnlyList<double> Widths => _widths;

    /// <summary>Raised when a header or row is measured at a finite width other than the last one.</summary>
    internal event Action<double>? Sized;

    /// <summary>Raised when a newly measured header or row raises a column's natural or narrowest width.</summary>
    internal event Action? Widened;

    /// <summary>How much wider than the last measured width the columns' minimums and the spacing need.</summary>
    internal double Shortfall =>
        Math.Max(0, Enumerable.Range(0, Columns.Count).Sum(MinimumOf) + (Spacing * Math.Max(0, Columns.Count - 1)) - _available);

    /// <summary>The control name of the grip after column <paramref name="key"/> of table <paramref name="id"/>.</summary>
    public static string GripName(string id, string key) => $"{id}.{key}.Grip";

    /// <summary>Builds the header row from ready-made cells, one per column.</summary>
    public TableGrid Header(IReadOnlyList<Control> cells) => Build(cells, header: true);

    /// <summary>Builds a data row from cells, one per column.</summary>
    public TableGrid Row(IReadOnlyList<Control> cells) => Build(cells, header: false);

    /// <summary>
    /// The narrowest column <paramref name="column"/> goes. A <see cref="ColumnFit.Fill"/> column, which holds names, wraps
    /// down to its longest word; every other column keeps its widest cell. Neither goes below its minimum or header label.
    /// </summary>
    internal double MinimumOf(int column) =>
        Columns[column] switch
        {
            { Fit: ColumnFit.Fixed } fixedColumn => fixedColumn.Fixed,
            { Fit: ColumnFit.Fill, Whole: false } name => Math.Max(name.Min ?? _headerWidth[column], _narrowest[column]),
            var other => Math.Max(other.Min ?? _headerWidth[column], _natural[column]),
        };

    internal void Compute(double available)
    {
        var spacing = Spacing * Math.Max(0, Columns.Count - 1);

        if (!double.IsFinite(available))
        {
            available = Enumerable.Range(0, Columns.Count).Sum(NaturalOf) + spacing;
        }
        else if (_sized is not { } sized || Math.Abs(sized - available) > 0.5)
        {
            _sized = available;
            Sized?.Invoke(available);
        }

        _available = available;

        var minimum = Enumerable.Range(0, Columns.Count).Select(MinimumOf).ToList();
        var widths = ColumnLayout.Widths(Columns, _natural, minimum, _chosen, Math.Max(0, available - spacing));

        for (var i = 0; i < widths.Count; i++)
        {
            _widths[i] = widths[i];
        }
    }

    internal void Report(TableGrid grid, double[] natural, double[] narrowest, bool header)
    {
        var grew = false;

        for (var i = 0; i < natural.Length; i++)
        {
            if (narrowest[i] > _narrowest[i])
            {
                _narrowest[i] = narrowest[i];
                grew = true;
            }

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
            Widened?.Invoke();
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
                if (Columns[i].Fit != ColumnFit.Fixed && PartnerOf(i) is var partner and >= 0)
                {
                    AddGrip(grid, i, partner);
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

    /// <summary>The next column to the right that is not fixed; -1 when there is none.</summary>
    private int PartnerOf(int column)
    {
        for (var next = column + 1; next < Columns.Count; next++)
        {
            if (Columns[next].Fit != ColumnFit.Fixed)
            {
                return next;
            }
        }

        return -1;
    }

    private void AddGrip(TableGrid header, int column, int partner)
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
        var startLeft = 0.0;
        var startRight = 0.0;
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
                foreach (var key in new[] { Columns[column].Key, Columns[partner].Key })
                {
                    _chosen.Remove(key);
                    _memory!.Forget(Id, key);
                }

                Reflow(header);
                return;
            }

            dragging = true;
            grip.Dragging = true;
            startX = e.GetPosition(header).X;
            startLeft = _widths[column];
            startRight = _widths[partner];
            e.Pointer.Capture(grip);
        };

        grip.PointerMoved += (_, e) =>
        {
            if (!dragging)
            {
                return;
            }

            e.Handled = true;

            var room = _available - (Spacing * Math.Max(0, Columns.Count - 1));

            if (room <= 0)
            {
                return;
            }

            var delta = Math.Max(
                MinimumOf(column) - startLeft,
                Math.Min(e.GetPosition(header).X - startX, startRight - MinimumOf(partner)));

            _chosen[Columns[column].Key] = (startLeft + delta) / room;
            _chosen[Columns[partner].Key] = (startRight - delta) / room;
            Reflow(header);
        };

        grip.PointerReleased += (_, e) =>
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            grip.Dragging = false;
            grip.Hovered = new Rect(grip.Bounds.Size).Contains(e.GetPosition(grip));
            e.Pointer.Capture(null);

            foreach (var key in new[] { Columns[column].Key, Columns[partner].Key })
            {
                if (_chosen.TryGetValue(key, out var share))
                {
                    _memory!.Remember(Id, key, share);
                }
            }
        };

        grip.PointerEntered += (_, _) => grip.Hovered = true;
        grip.PointerExited += (_, _) => grip.Hovered = dragging;

        header.Children.Add(grip);
    }

    private void Reflow(TableGrid header)
    {
        header.InvalidateMeasure();
        Invalidate();
    }
}

/// <summary>The drag handle on a header boundary; not a cell. Draws a vertical line: 1px Line2 at rest, 2px A while hovered or dragged.</summary>
public sealed class ColumnGrip : Border
{
    private const double LineInset = 6;

    private readonly Border _line = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, LineInset),
        IsHitTestVisible = false,
    };

    private IDisposable? _ink;
    private bool _hovered;
    private bool _dragging;

    public ColumnGrip()
    {
        Child = _line;
        Show();
    }

    internal bool Hovered
    {
        set
        {
            _hovered = value;
            Show();
        }
    }

    internal bool Dragging
    {
        set
        {
            _dragging = value;
            Show();
        }
    }

    private void Show()
    {
        var active = _hovered || _dragging;

        _line.Width = active ? 2 : 1;
        _ink?.Dispose();
        _ink = ThemeInk.Bind(_line, Border.BackgroundProperty, active ? ThemeManager.AKey : ThemeManager.Line2Key);
    }
}

/// <summary>A header or row of a <see cref="TableLayout"/>: a <see cref="Grid"/> whose column widths come from the table.</summary>
public sealed class TableGrid : Grid
{
    /// <summary>
    /// A natural width a cell supplies when its measured width is not what it needs. Such a cell fits itself to the width
    /// it is given, so in a <see cref="ColumnFit.Fill"/> column it sets no narrowest width.
    /// </summary>
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
        var narrowest = new double[Layout.Columns.Count];

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

            if (!IsHeader && Layout.Columns[column].Fit == ColumnFit.Fill)
            {
                narrowest[column] = Math.Max(narrowest[column], double.IsNaN(supplied) ? NarrowestWidth.Of(child) : 0);
            }
        }

        Layout.Report(this, natural, narrowest, IsHeader);
        Layout.Compute(availableSize.Width);

        for (var i = 0; i < natural.Length; i++)
        {
            ColumnDefinitions[i].Width = new GridLength(Layout.Widths[i], GridUnitType.Pixel);
        }

        return base.MeasureOverride(availableSize);
    }
}
