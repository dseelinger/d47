using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Scrollbars, aimed at with a controller
/// (remediation.md, "Scrollbars in VR should be usable with a controller").
/// <para>
/// The whole of the requirement is that the aim does not have to be good. A scrollbar is a dozen
/// pixels wide and a hand at arm's length does not hold still to a dozen pixels, so what is
/// asserted here is mostly what happens when the ray is <em>near</em> one rather than on it.
/// </para>
/// </summary>
public class TheVrScrollbarsTests
{
    private static readonly PixelSize Quad = new(1024, 640);

    private static (PanelView View, OffscreenSurface Surface, ScrollBar Bar) Filled()
    {
        var model = new PanelViewModel();

        for (var line = 0; line < 200; line++)
        {
            model.Append($"Line {line} of the transcript, long enough to wrap once or twice.\n");
        }

        var view = new PanelView { DataContext = model };
        var surface = new OffscreenSurface(view, Quad);

        surface.Render(view.KeepUp);
        surface.Render(view.KeepUp);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var bar = view.GetVisualDescendants()
            .OfType<ScrollBar>()
            .First(found => found.Orientation == Avalonia.Layout.Orientation.Vertical && found.Maximum > 0);

        return (view, surface, bar);
    }

    /// <summary>Where a bar is, in the surface's own pixels.</summary>
    private static Rect Box(ScrollBar bar, Control view)
    {
        var corner = bar.TranslatePoint(new Point(0, 0), view);
        Assert.NotNull(corner);
        return new Rect(corner.Value, bar.Bounds.Size);
    }

    [AvaloniaFact]
    public void ARayOnTheBarFindsIt()
    {
        var (view, surface, bar) = Filled();
        using var _ = surface;

        var box = Box(bar, view);

        Assert.Same(bar, surface.ScrollbarNear(box.Center));
    }

    /// <summary>
    /// And a ray that misses it by more than the bar's own width still finds it, which is the
    /// point: jitter means close enough has to be enough.
    /// </summary>
    [AvaloniaFact]
    public void ARayNearTheBarFindsItToo()
    {
        var (view, surface, bar) = Filled();
        using var _ = surface;

        var box = Box(bar, view);

        Assert.Same(bar, surface.ScrollbarNear(new Point(box.Center.X - 20, box.Center.Y)));
        Assert.Same(bar, surface.ScrollbarNear(new Point(box.Center.X + 20, box.Center.Y)));
    }

    /// <summary>But not from the other side of the panel, which is not aiming at it.</summary>
    [AvaloniaFact]
    public void ARayNowhereNearItFindsNothing()
    {
        var (view, surface, bar) = Filled();
        using var _ = surface;

        var box = Box(bar, view);

        Assert.Null(surface.ScrollbarNear(new Point(box.Center.X - 300, box.Center.Y)));
    }

    /// <summary>
    /// Where along the bar the ray is <em>is</em> where in the document it goes. Absolute rather
    /// than a relative drag from wherever a thumb was caught: there is no thumb to catch, and
    /// letting go and taking hold again does not jump.
    /// </summary>
    [AvaloniaFact]
    public void PointingAtTheTopOfTheBarGoesToTheTop()
    {
        var (view, surface, bar) = Filled();
        using var _ = surface;

        var box = Box(bar, view);

        Assert.True(bar.Value > 0, "it starts at the newest line, which is the bottom");

        OffscreenSurface.Aim(bar, view, new Point(box.Center.X, box.Y));
        Assert.Equal(bar.Minimum, bar.Value, 3);

        OffscreenSurface.Aim(bar, view, new Point(box.Center.X, box.Bottom));
        Assert.Equal(bar.Maximum, bar.Value, 3);

        // And past either end is that end rather than a value off the scale.
        OffscreenSurface.Aim(bar, view, new Point(box.Center.X, box.Y - 200));
        Assert.Equal(bar.Minimum, bar.Value, 3);

        OffscreenSurface.Aim(bar, view, new Point(box.Center.X, box.Center.Y));
        Assert.InRange(bar.Value, bar.Maximum * 0.4, bar.Maximum * 0.6);
    }

    /// <summary>
    /// And it lights up when aimed at, because a control that gives no sign it has been found is
    /// one the Commander cannot tell they are pointing at.
    /// </summary>
    [AvaloniaFact]
    public void TheBarLightsUpWhenAimedAtAndGoesOutAfterwards()
    {
        var (view, surface, bar) = Filled();
        using var _ = surface;

        Assert.DoesNotContain(":pointerover", bar.Classes);

        surface.Illuminate(bar);
        Assert.Contains(":pointerover", bar.Classes);

        surface.Illuminate(null);
        Assert.DoesNotContain(":pointerover", bar.Classes);
    }

    /// <summary>
    /// The surface the headset actually drives, rather than the offscreen host underneath it:
    /// a grab near the bar takes hold, and scrolling moves the document.
    /// </summary>
    [AvaloniaFact]
    public void TheHeadsetSurfaceTakesHoldAndScrolls()
    {
        var (settings, _, _) = TestSurface.Create();
        var model = new PanelViewModel();

        for (var line = 0; line < 200; line++)
        {
            model.Append($"Line {line} of the transcript, long enough to wrap once or twice.\n");
        }

        using var panel = new VrPanelSurface(model, settings, _ => null);

        var (width, height) = panel.Size;
        var buffer = new byte[width * height * 4];

        unsafe
        {
            fixed (byte* pixels = buffer)
            {
                panel.Draw((IntPtr)pixels, width * 4);
                panel.Draw((IntPtr)pixels, width * 4);
            }
        }

        var view = (PanelView)typeof(VrPanelSurface)
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(panel)!;

        var bar = view.GetVisualDescendants()
            .OfType<ScrollBar>()
            .First(found => found.Orientation == Avalonia.Layout.Orientation.Vertical && found.Maximum > 0);

        var box = Box(bar, view);

        // Twenty pixels short of the bar, halfway down: aiming at it without hitting it, which
        // is what a hand at arm's length actually does.
        var u = (float)((box.Center.X - 20) / width);
        var v = (float)(box.Center.Y / height);

        Assert.True(panel.GrabsScroll(u, v), "a ray near the bar takes hold of it");

        var was = bar.Value;

        panel.Scroll(u, (float)(box.Y / height));
        Assert.True(bar.Value < was, "pointing higher up the bar scrolls back through the log");

        panel.ReleaseScroll();
    }
}
