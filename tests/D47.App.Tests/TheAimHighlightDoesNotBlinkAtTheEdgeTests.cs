using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A ray held at the edge of a scrollbar does not make it blink
/// (<a href="https://github.com/dseelinger/d47/issues/29">#29</a>), reported 2026-08-24:
/// <em>"When getting near the scrollbar (or scrolling) I get the flickering effect."</em>
/// <para>
/// <b>This is not the fault #23 was.</b> A probe drove the highlight on and off for ten cycles and
/// counted the non-background pixels of every served frame: 12,807 every time, no collapse and no
/// partial frame. The submit is double-buffered and <see cref="OffscreenSurface.Layout"/> already
/// re-measures the whole tree before rasterising, so the frames were never the problem. The
/// highlight really was blinking — one radius decided both directions, and a hand at arm's length
/// wanders across it.
/// </para>
/// <para>
/// The sibling case is already covered:
/// <see cref="TheVrScrollbarsTests.RestingAimDoesNotAskForARedraw"/> holds a hand wandering
/// <em>inside</em> a bar. This one holds it wandering across the boundary, where every frame used
/// to answer a different bar — correctly, and unbearably.
/// </para>
/// </summary>
public class TheAimHighlightDoesNotBlinkAtTheEdgeTests
{
    private static (VrPanelSurface Panel, PanelView View, ScrollBar Bar) Filled()
    {
        var (settings, _, _) = TestSurface.Create();
        var model = new PanelViewModel();

        for (var line = 0; line < 200; line++)
        {
            model.Append($"Line {line} of the transcript, long enough to wrap once or twice.\n");
        }

        var panel = new VrPanelSurface(model, settings, _ => null);

        var (width, height) = panel.Size;
        var buffer = new byte[width * height * 4];

        unsafe
        {
            fixed (byte* pixels = buffer)
            {
                panel.Draw((IntPtr)pixels, width * 4);
            }
        }

        var view = (PanelView)typeof(VrPanelSurface)
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(panel)!;

        var bar = view.GetVisualDescendants()
            .OfType<ScrollBar>()
            .First(found => found.Orientation == Avalonia.Layout.Orientation.Vertical && found.Maximum > 0);

        return (panel, view, bar);
    }

    /// <summary>
    /// A hand trying to hold still just off the bar, jittering a few pixels either side of the
    /// radius that lit it. The answer must not change at all.
    /// <para>
    /// <b>A clean sweep out and back does not test this</b>, which is what putting the fault back
    /// showed: one radius crossed monotonically still changes twice, so a sweep passes either way.
    /// The blink needs the hand to wander <em>across</em> the boundary repeatedly, which is what a
    /// hand at arm's length actually does.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void AHandTryingToHoldStillAtTheEdgeDoesNotBlink()
    {
        var (panel, view, bar) = Filled();

        using (panel)
        {
            var (width, height) = panel.Size;

            var corner = bar.TranslatePoint(new Point(0, 0), view);
            Assert.NotNull(corner);

            var box = new Rect(corner.Value, bar.Bounds.Size);

            // Light it first, from the middle of the bar, so the walk starts from lit.
            panel.Aim((float)(box.Center.X / width), (float)(box.Center.Y / height));

            var offscreen = (OffscreenSurface)typeof(VrPanelSurface)
                .GetField("_offscreen", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(panel)!;

            Assert.NotNull(offscreen.ScrollbarNear(box.Center));

            var changes = 0;
            var was = true;

            // Hovering a whisker outside the radius that lit it, wandering four pixels either way,
            // forty times. Every one of these frames used to answer a different bar.
            foreach (var step in Tremor(box.Right + OffscreenSurface.AimTolerance + 1, 4, 40))
            {
                var now = offscreen.ScrollbarNear(new Point(step, box.Center.Y)) is not null;

                if (now != was)
                {
                    changes++;
                    was = now;
                }
            }

            Assert.Equal(0, changes);
        }
    }

    /// <summary>
    /// And the hysteresis is real rather than a wider single radius: the ray leaves at a distance
    /// that would not have let it arrive.
    /// </summary>
    [AvaloniaFact]
    public void ArrivingCostsLessThanLeaving()
    {
        var (panel, view, bar) = Filled();

        using (panel)
        {
            var corner = bar.TranslatePoint(new Point(0, 0), view);
            Assert.NotNull(corner);

            var box = new Rect(corner.Value, bar.Bounds.Size);

            var offscreen = (OffscreenSurface)typeof(VrPanelSurface)
                .GetField("_offscreen", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(panel)!;

            // A point between the two radii. From cold it is too far to light anything.
            var between = new Point(
                box.Right + ((OffscreenSurface.AimTolerance + OffscreenSurface.AimRelease) / 2),
                box.Center.Y);

            offscreen.Illuminate(null);
            Assert.Null(offscreen.ScrollbarNear(between));

            // Lit from the bar itself, the same point now holds it.
            offscreen.Illuminate(offscreen.ScrollbarNear(box.Center));
            Assert.NotNull(offscreen.ScrollbarNear(between));
        }
    }

    /// <summary>A hand holding still: <paramref name="about"/>, give or take, over and over.</summary>
    private static IEnumerable<double> Tremor(double about, double give, int frames)
    {
        for (var frame = 0; frame < frames; frame++)
        {
            // Deterministic rather than random, so a failure is the same failure twice.
            yield return about + (give * Math.Sin(frame * 1.1));
        }
    }
}
