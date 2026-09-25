using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Ships;

namespace D47.App.Panel;

/// <summary>
/// The Power page's chart: every module stacked on the cumulative axis, the output lines across it, and
/// the selected priority opened up in a column beside it with a leader curve per module (#469).
/// </summary>
/// <remarks>
/// Geometry is the design's, in its own units: heights are measured up from the chart's bottom edge and
/// flipped when drawn.
/// </remarks>
public sealed class PowerChart : Control
{
    public const double ChartWidth = 705;

    public const double ChartHeight = 500;

    private const double StackLeft = 205;
    private const double StackRight = 295;
    private const double DrillLeft = 425;
    private const double DrillRight = 705;
    private const double LabelRight = 196;
    private const double Column = 480;
    private const double Floor = 10;
    private const double LeastBar = 24;
    private const double DragStart = 4;

    // This and the 1px every slice leaves at its top put priorities 4px apart.
    private const double PriorityGap = 3;
    private const double EmptySlot = 20;
    private const double LeastLabel = 16;

    private PowerPriorities? _power;
    private int _selected = PowerPriorities.Lowest;
    private double _deployed;

    private string? _hover;
    private string? _pressed;
    private Point _pressedAt;
    private string? _dragging;
    private (string Slot, bool After)? _drop;
    private int? _move;

    public PowerChart()
    {
        Width = ChartWidth;
        Height = ChartHeight;
        Cursor = new Cursor(StandardCursorType.Arrow);

        ResourcesChanged += (_, _) => InvalidateVisual();
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>Raised when a slice or a P-label is pressed, with its priority.</summary>
    public event Action<int>? Selected;

    /// <summary>Raised when a bar is dropped on another: the dragged slot, the target slot, and whether it lands above it.</summary>
    public event Action<string, string, bool>? Reordered;

    /// <summary>Raised when a bar is dropped on another priority's block in the stack: the slot and that priority.</summary>
    public event Action<string, int>? Moved;

    /// <summary>Draws <paramref name="power"/> with <paramref name="selected"/> opened up.</summary>
    /// <param name="deployed">The deployed draw, which sets the scale in both modes.</param>
    public void Show(PowerPriorities power, int selected, double deployed)
    {
        _power = power;
        _selected = Math.Clamp(selected, 1, PowerPriorities.Lowest);
        _deployed = deployed;
        _drop = null;
        _move = null;
        _dragging = null;
        _pressed = null;

        InvalidateVisual();
    }

    /// <summary>The slot under the pointer in the drill-in column, or null.</summary>
    internal string? Hovered => _hover;

    private sealed record Slice(ModuleSpan Span, int Priority, double Bottom, double Top);

    private sealed record Bar(ModuleSpan Span, double Bottom, double Height)
    {
        public double Middle => Bottom + (Height / 2);
    }

    /// <summary>One priority's block of the stack, linear in megawatts from its own bottom edge.</summary>
    private sealed record Block(PriorityBand Band, double Bottom, double Top, double Scale)
    {
        public bool IsEmpty => Band.Total <= 0;

        public double Middle => (Bottom + Top) / 2;

        public double At(double megawatts) => Bottom + Math.Round((megawatts - Band.Start) * Scale);
    }

    private sealed record Layout(
        IReadOnlyList<Block> Blocks,
        IReadOnlyList<Slice> Slices,
        PriorityDrill Drill,
        IReadOnlyList<Bar> Bars,
        double Top)
    {
        public Block Selected => Blocks[Drill.Band.Priority - 1];

        /// <summary>Where <paramref name="megawatts"/> falls on the stack: in the first block that reaches it, or above them all.</summary>
        public double At(double megawatts)
        {
            foreach (var block in Blocks.Where(block => !block.IsEmpty))
            {
                if (megawatts <= block.Band.Cumulative + 1e-9)
                {
                    return block.At(megawatts);
                }
            }

            var last = Blocks[^1];

            return last.Top + Math.Round((megawatts - last.Band.Cumulative) * last.Scale);
        }
    }

    private Layout? Lay()
    {
        if (_power is not { } power)
        {
            return null;
        }

        var reach = Math.Max(power.Output, _deployed) * 1.06;
        var empties = power.Bands.Count(band => band.Total <= 0);
        var room = Math.Max(0, Column - (PriorityGap * (PowerPriorities.Lowest - 1)) - (EmptySlot * empties));
        var scale = reach > 0 ? room / reach : 0;

        var blocks = new List<Block>();
        var bottom = Floor;

        foreach (var band in power.Bands)
        {
            var height = band.Total <= 0 ? EmptySlot : Math.Round(band.Total * scale);

            blocks.Add(new Block(band, bottom, bottom + height, scale));
            bottom += height + PriorityGap;
        }

        var slices = blocks
            .Where(block => !block.IsEmpty)
            .SelectMany(block => power.Drill(block.Band.Priority).Spans
                .Select(span => new Slice(span, block.Band.Priority, block.At(span.Start), block.At(span.End))))
            .ToList();

        var drill = power.Drill(_selected);
        var heights = Heights([.. drill.Spans.Select(span => span.Module.Megawatts)], Column, LeastBar);
        var bars = new List<Bar>();
        var y = Floor;

        for (var index = 0; index < drill.Spans.Count; index++)
        {
            bars.Add(new Bar(drill.Spans[index], y, heights[index]));
            y += heights[index];
        }

        return new Layout(blocks, slices, drill, bars, y);
    }

    /// <summary>
    /// Bar heights in proportion to <paramref name="megawatts"/>, filling <paramref name="column"/>. A bar
    /// that would come out shorter than <paramref name="least"/> is raised to it, and the others share
    /// what is left in proportion.
    /// </summary>
    internal static double[] Heights(IReadOnlyList<double> megawatts, double column, double least)
    {
        var heights = new double[megawatts.Count];
        var raised = new bool[megawatts.Count];

        while (true)
        {
            var free = Math.Max(0, column - (least * raised.Count(up => up)));
            var shared = megawatts.Where((_, index) => !raised[index]).ToList();
            var total = shared.Sum();
            var changed = false;

            for (var index = 0; index < megawatts.Count; index++)
            {
                if (raised[index])
                {
                    heights[index] = least;
                    continue;
                }

                heights[index] = total > 0 ? megawatts[index] / total * free : free / shared.Count;

                if (heights[index] < least)
                {
                    raised[index] = true;
                    changed = true;
                }
            }

            if (!changed)
            {
                return heights;
            }
        }
    }

    private static double Flip(double fromBottom) => ChartHeight - fromBottom;

    /// <summary>Where <paramref name="megawatts"/> falls in the drill-in column, or null outside it.</summary>
    private static double? Inside(Layout layout, double megawatts) =>
        layout.Bars.FirstOrDefault(bar =>
                megawatts >= bar.Span.Start - 1e-9 && megawatts <= bar.Span.End + 1e-9) is { } bar
            ? bar.Bottom + ((megawatts - bar.Span.Start) / bar.Span.Module.Megawatts * bar.Height)
            : null;

    public override void Render(DrawingContext context)
    {
        if (Lay() is not { } layout || _power is not { } power)
        {
            return;
        }

        var bands = power.Bands;

        // The stack.
        foreach (var slice in layout.Slices)
        {
            var height = Math.Max(1, slice.Top - slice.Bottom - 1);
            var powered = bands[slice.Priority - 1].PoweredAt[0];
            var key = slice.Span.Module.Slot == _hover
                ? ThemeManager.WhiteKey
                : powered ? ThemeManager.AKey : ThemeManager.RedKey;

            using (context.PushOpacity(slice.Priority == _selected ? 1 : 0.45))
            {
                context.DrawRectangle(
                    Brush(key), null, new Rect(StackLeft, Flip(slice.Bottom + height), StackRight - StackLeft, height));
            }
        }

        foreach (var block in layout.Blocks.Where(block => block.IsEmpty))
        {
            var height = block.Top - block.Bottom - 1;

            using (context.PushOpacity(block.Band.Priority == _selected ? 1 : 0.45))
            {
                context.DrawRectangle(
                    null,
                    new Pen(Brush(ThemeManager.GreyKey), 1, new DashStyle([4, 3], 0)),
                    new Rect(StackLeft + 0.5, Flip(block.Bottom + height) + 0.5, StackRight - StackLeft - 1, height - 1));
            }
        }

        foreach (var (label, at) in Labels(layout))
        {
            context.DrawText(label, at);
        }

        if (_move is { } move)
        {
            var block = layout.Blocks[move - 1];
            var height = block.Top - block.Bottom - 1;

            context.DrawRectangle(
                null,
                new Pen(Brush(ThemeManager.CyanKey), 2),
                new Rect(StackLeft - 2, Flip(block.Bottom + height) - 2, StackRight - StackLeft + 4, height + 4));
        }

        // The output lines, each labelled just above itself.
        for (var index = 0; index < power.Lines.Count; index++)
        {
            var line = power.Lines[index];
            var full = index == 0;
            var y = layout.At(line.Megawatts);

            using (context.PushOpacity(full ? 1 : 0.6))
            {
                context.DrawRectangle(
                    Brush(ThemeManager.WhiteKey), null, new Rect(0, Flip(y) - (full ? 2 : 1), StackRight, full ? 2 : 1));
            }

            var meta = Text($"{Figure(line.Megawatts)} MW · {line.Powered}", Fonts.MonoFamily, 12, ThemeManager.WhiteKey);
            var name = Text(
                LevelName(index), Fonts.ChromeFamily, 12, full ? ThemeManager.WhiteKey : ThemeManager.GreyKey);

            var metaTop = Flip(y + 4) - meta.Height;

            context.DrawText(meta, new Point(LabelRight - meta.Width, metaTop));
            context.DrawText(name, new Point(LabelRight - name.Width, metaTop - 1 - name.Height));
        }

        Leaders(context, layout);

        var drill = layout.Drill;

        var head = Text($"{Figure(drill.Band.Cumulative)} MW", Fonts.MonoFamily, 11, ThemeManager.GreyKey);
        context.DrawText(head, new Point(DrillRight - head.Width, Flip(layout.Top + 4) - head.Height));

        var foot = Text($"{Figure(drill.Band.Start)} MW", Fonts.MonoFamily, 11, ThemeManager.GreyKey);
        context.DrawText(foot, new Point(DrillRight - foot.Width, ChartHeight + 8 - foot.Height));

        if (layout.Bars.Count == 0)
        {
            var slab = new Rect(DrillLeft, Floor, DrillRight - DrillLeft, ChartHeight - (2 * Floor));

            context.DrawRectangle(Brush(ThemeManager.SlabKey), null, slab);

            var empty = Text("Nothing draws power in this priority.", Fonts.ProseFamily, 14, ThemeManager.GreyKey);

            context.DrawText(empty, slab.Center - new Point(empty.Width / 2, empty.Height / 2));
        }

        foreach (var bar in layout.Bars)
        {
            DrawBar(context, bar, power.Output);
        }

        if (_drop is { } drop && layout.Bars.FirstOrDefault(bar => bar.Span.Module.Slot == drop.Slot) is { } target)
        {
            var bottom = Math.Round(drop.After ? target.Bottom + target.Height - 2 : target.Bottom - 1);

            context.DrawRectangle(Brush(ThemeManager.CyanKey), null, new Rect(DrillLeft - 6, Flip(bottom + 3), 292, 3));
        }

        foreach (var crossing in drill.Crossings)
        {
            if (Inside(layout, crossing.Line.Megawatts) is not { } at)
            {
                continue;
            }

            var full = crossing.Line.Level >= 1;
            var y = Math.Round(at);

            using (context.PushOpacity(full ? 1 : 0.7))
            {
                context.DrawRectangle(
                    Brush(ThemeManager.WhiteKey), null, new Rect(DrillLeft, Flip(y) - (full ? 2 : 1), 290, full ? 2 : 1));
            }
        }
    }

    /// <summary>The funnel, a curve per module, and a dashed curve per output line inside the priority.</summary>
    private void Leaders(DrawingContext context, Layout layout)
    {
        var block = layout.Selected;
        var top = block.Top;
        var bottom = block.Bottom;
        const double middle = (StackRight + DrillLeft) / 2;

        var funnel = new StreamGeometry();

        using (var draw = funnel.Open())
        {
            draw.BeginFigure(new Point(StackRight, Flip(top)), isFilled: true);
            draw.CubicBezierTo(
                new Point(middle, Flip(top)), new Point(middle, Flip(layout.Top)), new Point(DrillLeft, Flip(layout.Top)));
            draw.LineTo(new Point(DrillLeft, Flip(Floor)));
            draw.CubicBezierTo(
                new Point(middle, Flip(Floor)), new Point(middle, Flip(bottom)), new Point(StackRight, Flip(bottom)));
            draw.EndFigure(isClosed: true);
        }

        using (context.PushOpacity(0.35))
        {
            context.DrawGeometry(Brush(ThemeManager.TileKey), null, funnel);
        }

        foreach (var bar in layout.Bars)
        {
            var slot = bar.Span.Module.Slot;
            var from = (block.At(bar.Span.Start) + block.At(bar.Span.End)) / 2;
            var on = slot == _hover;

            using (context.PushOpacity(on ? 1 : 0.7))
            {
                context.DrawGeometry(
                    null,
                    new Pen(Brush(on ? ThemeManager.WhiteKey : ThemeManager.AKey), on ? 2 : 1),
                    Curve(from, bar.Middle - 1));
            }
        }

        foreach (var crossing in layout.Drill.Crossings)
        {
            if (Inside(layout, crossing.Line.Megawatts) is not { } at)
            {
                continue;
            }

            context.DrawGeometry(
                null,
                new Pen(Brush(ThemeManager.WhiteKey), 1, new DashStyle([4, 3], 0)),
                Curve(block.At(crossing.Line.Megawatts), Math.Round(at)));
        }
    }

    private static StreamGeometry Curve(double from, double to)
    {
        const double middle = (StackRight + DrillLeft) / 2;
        var curve = new StreamGeometry();

        using var draw = curve.Open();

        draw.BeginFigure(new Point(StackRight, Flip(from)), isFilled: false);
        draw.CubicBezierTo(new Point(middle, Flip(from)), new Point(middle, Flip(to)), new Point(DrillLeft, Flip(to)));
        draw.EndFigure(isClosed: false);

        return curve;
    }

    private void DrawBar(DrawingContext context, Bar bar, double output)
    {
        var module = bar.Span.Module;
        var height = Math.Round(bar.Height - 2);
        var bottom = Math.Round(bar.Bottom);
        var box = new Rect(DrillLeft, Flip(bottom + height), DrillRight - DrillLeft, height);
        var fits = bar.Span.End <= output + 1e-9;

        using var fade = context.PushOpacity(module.Slot == _dragging ? 0.4 : 1);

        context.DrawRectangle(Brush(fits ? ThemeManager.AKey : ThemeManager.RedKey), null, box);

        var megawatts = Text(Figure(module.Megawatts), Fonts.MonoFamily, 12, ThemeManager.KnockKey);
        var tag = module.IsHardpoint ? Text("HARDPOINT", Fonts.ChromeFamily, 10, ThemeManager.BrownKey) : null;
        var room = box.Width - 20 - megawatts.Width - 8 - (tag is null ? 0 : tag.Width + 8);

        var name = Text(
            module.Name.ToUpperInvariant(), Fonts.ChromeFamily, 12, ThemeManager.KnockKey, FontWeight.SemiBold);

        name.MaxTextWidth = Math.Max(1, room);
        name.MaxLineCount = 1;
        name.Trimming = TextTrimming.CharacterEllipsis;

        var middle = box.Y + (box.Height / 2);

        using (context.PushClip(box))
        {
            context.DrawText(name, new Point(box.X + 10, middle - (name.Height / 2)));

            if (tag is not null)
            {
                context.DrawText(tag, new Point(box.X + 10 + name.Width + 8, middle - (tag.Height / 2)));
            }

            context.DrawText(megawatts, new Point(box.Right - 10 - megawatts.Width, middle - (megawatts.Height / 2)));
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var point = e.GetPosition(this);

        if (_pressed is { } pressed)
        {
            if (_dragging is null && Distance(point, _pressedAt) >= DragStart)
            {
                _dragging = pressed;
            }

            if (_dragging is not null)
            {
                _drop = BarAt(point) is { } target && target.Span.Module.Slot != _dragging
                    ? (target.Span.Module.Slot, point.Y < Flip(target.Middle))
                    : null;

                _move = PriorityAt(point) is { } priority && priority != _selected ? priority : null;

                InvalidateVisual();
                return;
            }
        }

        var hover = BarAt(point)?.Span.Module.Slot;

        if (hover != _hover)
        {
            _hover = hover;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        if (_hover is not null && _dragging is null)
        {
            _hover = null;
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var point = e.GetPosition(this);

        if (BarAt(point) is { } bar)
        {
            _pressed = bar.Span.Module.Slot;
            _pressedAt = point;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (PriorityAt(point) is { } priority)
        {
            e.Handled = true;
            Selected?.Invoke(priority);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        var dragged = _dragging;
        var drop = _drop;
        var move = _move;

        _pressed = null;
        _dragging = null;
        _drop = null;
        _move = null;
        e.Pointer.Capture(null);

        if (dragged is not null && drop is { } target)
        {
            Reordered?.Invoke(dragged, target.Slot, target.After);
        }
        else if (dragged is not null && move is { } priority)
        {
            Moved?.Invoke(dragged, priority);
        }

        InvalidateVisual();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        _pressed = null;
        _dragging = null;
        _drop = null;
        _move = null;
        InvalidateVisual();
    }

    /// <summary>The drill-in bar under <paramref name="point"/>, or null.</summary>
    private Bar? BarAt(Point point)
    {
        if (point.X < DrillLeft || point.X > DrillRight || Lay() is not { } layout)
        {
            return null;
        }

        var up = Flip(point.Y);

        return layout.Bars.FirstOrDefault(bar => up >= bar.Bottom && up < bar.Bottom + bar.Height);
    }

    /// <summary>The priority whose block of the stack, or the gap above it, is under <paramref name="point"/>, or null.</summary>
    internal int? PriorityAt(Point point)
    {
        if (point.X < StackLeft || point.X > StackRight || Lay() is not { } layout)
        {
            return null;
        }

        var up = Flip(point.Y);

        return layout.Blocks
            .FirstOrDefault(block => up >= block.Bottom && up < block.Top + PriorityGap)?.Band.Priority;
    }

    /// <summary>The middle of a priority's block of the stack, in this control's coordinates.</summary>
    internal Point? CentreOf(int priority) =>
        Lay() is { } layout
            ? new Point((StackLeft + StackRight) / 2, Flip(layout.Blocks[priority - 1].Middle))
            : null;

    /// <summary>A priority's block of the stack, in this control's coordinates.</summary>
    internal Rect? BlockOf(int priority) =>
        Lay()?.Blocks[priority - 1] is { } block
            ? new Rect(StackLeft, Flip(block.Top), StackRight - StackLeft, block.Top - block.Bottom)
            : null;

    /// <summary>A module's slice of the stack, in this control's coordinates.</summary>
    internal Rect? SliceOf(string slot) =>
        Lay()?.Slices.FirstOrDefault(slice => slice.Span.Module.Slot == slot) is { } slice
            ? new Rect(StackLeft, Flip(slice.Top), StackRight - StackLeft, slice.Top - slice.Bottom)
            : null;

    /// <summary>The height of an output line across the stack, in this control's coordinates.</summary>
    internal double? LineAt(int index) =>
        Lay() is { } layout && _power is { } power ? Flip(layout.At(power.Lines[index].Megawatts)) : null;

    /// <summary>Each priority's P-label and where it is drawn, in this control's coordinates.</summary>
    internal IReadOnlyList<(int Priority, Rect At, bool Inside)> PriorityLabels() =>
        Lay() is { } layout
            ? [.. Labels(layout).Select((label, index) => (
                index + 1, new Rect(label.At, new Size(label.Text.Width, label.Text.Height)), label.At.X < StackRight))]
            : [];

    /// <summary>
    /// A P-label per priority: centred in its block when the block holds it, otherwise just right of the
    /// stack and raised clear of the label below.
    /// </summary>
    private List<(FormattedText Text, Point At)> Labels(Layout layout)
    {
        var labels = new List<(FormattedText, Point)>();
        var clear = double.NegativeInfinity;

        foreach (var block in layout.Blocks)
        {
            var inside = block.Top - block.Bottom - 1 >= LeastLabel;
            var key = !inside || block.IsEmpty ? ThemeManager.GreyKey : ThemeManager.KnockKey;
            var label = Text($"P{block.Band.Priority}", Fonts.ChromeFamily, 13, key, FontWeight.SemiBold);

            if (inside)
            {
                labels.Add((label, new Point(
                    (StackLeft + StackRight - label.Width) / 2, Flip(block.Middle) - (label.Height / 2))));
                continue;
            }

            var bottom = Math.Max(block.Middle - (label.Height / 2), clear);

            clear = bottom + label.Height;
            labels.Add((label, new Point(StackRight + 6, Flip(bottom + label.Height))));
        }

        return labels;
    }

    /// <summary>The middle of a drill-in bar, in this control's coordinates.</summary>
    internal Point? CentreOfBar(string slot) =>
        Lay()?.Bars.FirstOrDefault(bar => bar.Span.Module.Slot == slot) is { } bar
            ? new Point((DrillLeft + DrillRight) / 2, Flip(bar.Middle))
            : null;

    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    internal static string LevelName(int index) => index switch
    {
        0 => "FULL OUTPUT",
        1 => "DESTROYED 50%",
        2 => "MALFUNCTIONING 40%",
        _ => "BOTH 20%",
    };

    internal static string Figure(double megawatts) => megawatts.ToString("0.00", CultureInfo.InvariantCulture);

    private FormattedText Text(
        string text, string family, double size, string key, FontWeight weight = FontWeight.Normal) =>
        new(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily(family), FontStyle.Normal, weight),
            size,
            Brush(key));

    /// <summary>A D47 resource, resolved against the running theme so the chart follows all four.</summary>
    private IBrush Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var found) && found is IBrush brush
            ? brush
            : Brushes.Transparent;
}
