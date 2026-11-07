using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Ships;
using static D47.App.Panel.PowerChartLayout;

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

    private const double LabelRight = 196;
    private const double DragStart = 4;

    private PowerPriorities? _power;
    private PowerChartLayout? _layout;
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
        _layout = PowerChartLayout.Of(power, _selected, deployed);
        _drop = null;
        _move = null;
        _dragging = null;
        _pressed = null;

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (_layout is not { } layout || _power is not { } power)
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
                    Brush(ThemeManager.TileKey), null, new Rect(StackLeft, Flip(block.Bottom + height), StackRight - StackLeft, height));
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
            if (layout.Inside(crossing.Line.Megawatts) is not { } at)
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
    private void Leaders(DrawingContext context, PowerChartLayout layout)
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
            if (layout.Inside(crossing.Line.Megawatts) is not { } at)
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
                _drop = _layout?.BarAt(point) is { } target && target.Span.Module.Slot != _dragging
                    ? (target.Span.Module.Slot, point.Y < Flip(target.Middle))
                    : null;

                _move = _layout?.PriorityAt(point) is { } priority && priority != _selected ? priority : null;

                InvalidateVisual();
                return;
            }
        }

        var hover = _layout?.BarAt(point)?.Span.Module.Slot;

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

        if (_layout?.BarAt(point) is { } bar)
        {
            _pressed = bar.Span.Module.Slot;
            _pressedAt = point;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (_layout?.PriorityAt(point) is { } priority)
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

    /// <summary>The P-label per priority and where it is drawn.</summary>
    private List<(FormattedText Text, Point At)> Labels(PowerChartLayout layout)
    {
        var texts = layout.Blocks
            .Select(block => Text(
                $"P{block.Band.Priority}",
                Fonts.ChromeFamily,
                13,
                !IsLabelledInside(block) || block.IsEmpty ? ThemeManager.GreyKey : ThemeManager.KnockKey,
                FontWeight.SemiBold))
            .ToList();

        return [.. layout.Labels([.. texts.Select(text => new Size(text.Width, text.Height))])
            .Select((label, index) => (texts[index], label.At))];
    }

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
