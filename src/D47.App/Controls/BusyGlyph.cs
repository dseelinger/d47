using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace D47.App.Controls;

/// <summary>
/// A small spinning arc, for the moment between asking d47 for something and getting it.
/// <para>
/// Its own control rather than a spinner built into the one place that first needed it: a
/// picker that has to gather devices is not the last thing here that will take long enough to
/// need saying so, and the second copy is where two of these start looking different from each
/// other.
/// </para>
/// <para>
/// Drawn rather than templated, and animated by the animation system rather than a timer. A
/// timer per instance is a thing to remember to stop; the animation is cancelled when the
/// control leaves the visual tree, which happens whether or not anybody remembered.
/// </para>
/// </summary>
public sealed class BusyGlyph : Control
{
    /// <summary>The arc's colour. Set by the caller, so this follows the theme it sits in.</summary>
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<BusyGlyph, IBrush?>(nameof(Stroke));

    /// <summary>
    /// How much of the ring is drawn, in degrees. Three quarters: a full ring cannot be seen to
    /// rotate, and much less than half reads as a fleck rather than as motion.
    /// </summary>
    private const double SweepDegrees = 270;

    /// <summary>
    /// Where the arc starts, in degrees. Animated rather than spinning the control with a
    /// render transform: the transform animator wants a visual and throws a cast exception when
    /// handed the transform itself, and an angle this control already needs in order to draw is
    /// one indirection fewer than a matrix rotating what it drew.
    /// </summary>
    private static readonly StyledProperty<double> AngleProperty =
        AvaloniaProperty.Register<BusyGlyph, double>(nameof(Angle));

    private CancellationTokenSource? _spinning;

    static BusyGlyph() => AffectsRender<BusyGlyph>(AngleProperty, StrokeProperty);

    public BusyGlyph()
    {
        Width = 14;
        Height = 14;
    }

    private double Angle => GetValue(AngleProperty);

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _spinning = new CancellationTokenSource();

        var spin = new Animation
        {
            Duration = TimeSpan.FromSeconds(1),
            IterationCount = IterationCount.Infinite,

            // Linear, because a spinner that eases is a spinner that looks like it is
            // struggling rather than working.
            Easing = new LinearEasing(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(RotateTransform.AngleProperty, 0d) },
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(RotateTransform.AngleProperty, 360d) },
                },
            },
        };

        _ = spin.RunAsync(this, _spinning.Token);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _spinning?.Cancel();
        _spinning?.Dispose();
        _spinning = null;
    }

    public override void Render(DrawingContext context)
    {
        if (Stroke is not { } brush)
        {
            return;
        }

        var thickness = 2d;
        var radius = (Math.Min(Bounds.Width, Bounds.Height) - thickness) / 2;

        if (radius <= 0)
        {
            return;
        }

        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);

        static Point On(Point centre, double radius, double degrees)
        {
            var radians = degrees * Math.PI / 180;
            return centre + new Point(radius * Math.Sin(radians), -radius * Math.Cos(radians));
        }

        var start = On(centre, radius, Angle);
        var end = On(centre, radius, Angle + SweepDegrees);

        var arc = new StreamGeometry();

        using (var draw = arc.Open())
        {
            draw.BeginFigure(start, isFilled: false);
            draw.ArcTo(
                end,
                new Size(radius, radius),
                rotationAngle: 0,
                isLargeArc: SweepDegrees > 180,
                SweepDirection.Clockwise);
            draw.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, new Pen(brush, thickness, lineCap: PenLineCap.Round), arc);
    }
}
