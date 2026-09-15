using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The header glyph a ray uses to enter resize mode, and the bar the handles carry to zoom and leave
/// it (#190).
/// </summary>
public class ARayFindsItsOwnWayIntoResizeModeTests
{
    private static PanelView View(VrPanelSurface panel) =>
        (PanelView)typeof(VrPanelSurface)
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(panel)!;

    private static StackPanel? Bar(VrPanelSurface panel) =>
        (StackPanel?)typeof(VrPanelSurface)
            .GetField("_bar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(panel);

    private static Control Root(VrPanelSurface panel) =>
        ((OffscreenSurface)typeof(VrPanelSurface)
            .GetField("_offscreen", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(panel)!).View;

    /// <summary>One served frame, which is what gives the tree a real extent to press.</summary>
    private static void Serve(VrPanelSurface panel)
    {
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
    }

    private static bool PressAt(VrPanelSurface panel, Control root, Control target)
    {
        var (width, height) = panel.Size;
        var at = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), root);
        Assert.NotNull(at);
        return panel.Press((float)(at.Value.X / width), (float)(at.Value.Y / height));
    }

    [AvaloniaFact]
    public void TheHeaderGlyphOffersResizeOnlyWithControllersOn()
    {
        var (settings, _, _) = TestSurface.Create();
        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);
        settings.Apply(VrCapability.ControllersKey, "false", SettingsCaller.Panel);

        var entered = 0;

        var panel = new VrPanelSurface(
            new PanelViewModel(), settings, _ => null,
            enterResize: () => entered++);

        using (panel)
        {
            Serve(panel);

            var button = View(panel).GetVisualDescendants().OfType<Button>().First(b => b.Name == "ResizeButton");

            Assert.False(button.IsVisible, "the controllers are off");

            settings.Apply(VrCapability.ControllersKey, "true", SettingsCaller.Panel);
            panel.Configure();

            Assert.True(button.IsVisible, "the controllers are on and there is a way in");

            Assert.True(PressAt(panel, Root(panel), button), "the press landed on the glyph");
            Assert.Equal(1, entered);
        }
    }

    [AvaloniaFact]
    public void MiniOffersNoHeaderGlyphEvenWithControllersOn()
    {
        var (settings, _, _) = TestSurface.Create();
        settings.Apply(VrCapability.ControllersKey, "true", SettingsCaller.Panel);
        settings.Apply(VrCapability.ModeKey, "mini", SettingsCaller.Panel);

        var panel = new VrPanelSurface(
            new PanelViewModel(), settings, _ => null,
            enterResize: () => { });

        using (panel)
        {
            Serve(panel);
            panel.Configure();

            var button = View(panel).GetVisualDescendants().OfType<Button>().First(b => b.Name == "ResizeButton");

            Assert.False(button.IsVisible, "mini carries no buttons at all");
        }
    }

    [AvaloniaFact]
    public void TheBarShowsOnlyWithTheHandlesAndPressesReachTheCallbacks()
    {
        var (settings, _, _) = TestSurface.Create();
        var zoomed = new List<string>();
        var left = 0;

        var panel = new VrPanelSurface(
            new PanelViewModel(), settings, _ => null,
            stepZoom: direction => zoomed.Add(direction),
            leaveResize: () => left++);

        using (panel)
        {
            Serve(panel);

            var bar = Bar(panel);
            Assert.NotNull(bar);
            Assert.False(bar!.IsVisible, "the handles are not shown yet");

            panel.ShowHandles(true, VrHandle.None);
            Serve(panel);

            Assert.True(bar.IsVisible);

            var root = Root(panel);
            var buttons = bar.GetVisualDescendants().OfType<Button>().ToList();
            Assert.Equal(4, buttons.Count);

            foreach (var button in buttons)
            {
                Assert.True(PressAt(panel, root, button), "the press landed on the button");
            }

            Assert.Equal(["out", "reset", "in"], zoomed);
            Assert.Equal(1, left);

            panel.ShowHandles(false, VrHandle.None);
            Assert.False(bar.IsVisible, "leaving resize mode puts the bar away too");
        }
    }

    [AvaloniaFact]
    public void TheBarStillAppearsInMiniWhileTheGlyphNeverDoes()
    {
        var (settings, _, _) = TestSurface.Create();
        settings.Apply(VrCapability.ModeKey, "mini", SettingsCaller.Panel);
        settings.Apply(VrCapability.ControllersKey, "true", SettingsCaller.Panel);

        var panel = new VrPanelSurface(
            new PanelViewModel(), settings, _ => null,
            enterResize: () => { },
            stepZoom: _ => { },
            leaveResize: () => { });

        using (panel)
        {
            Serve(panel);
            panel.Configure();

            panel.ShowHandles(true, VrHandle.None);
            Serve(panel);

            var bar = Bar(panel);
            Assert.NotNull(bar);
            Assert.True(bar!.IsVisible, "the bar still offers a way to zoom and leave");

            var button = View(panel).GetVisualDescendants().OfType<Button>().First(b => b.Name == "ResizeButton");
            Assert.False(button.IsVisible, "mini never offers an entry button");
        }
    }

    /// <summary>The inset that keeps a press on the bar from ever landing in a handle band.</summary>
    [AvaloniaFact]
    public void NoPointOnTheBarIsAHandle()
    {
        var (settings, _, _) = TestSurface.Create();

        var panel = new VrPanelSurface(
            new PanelViewModel(), settings, _ => null,
            stepZoom: _ => { },
            leaveResize: () => { });

        using (panel)
        {
            panel.ShowHandles(true, VrHandle.None);
            Serve(panel);

            var bar = Bar(panel)!;
            var root = Root(panel);
            var (width, height) = panel.Size;

            var extent = new VrExtent(panel.Placement.WidthMetres, (float)width / Math.Max(1, height));

            var corner = bar.TranslatePoint(new Point(0, 0), root);
            Assert.NotNull(corner);
            var box = new Rect(corner.Value, bar.Bounds.Size);

            foreach (var point in new[] { box.TopLeft, box.TopRight, box.BottomLeft, box.BottomRight, box.Center })
            {
                var handle = VrResize.HandleAt((float)(point.X / width), (float)(point.Y / height), extent);
                Assert.Equal(VrHandle.None, handle);
            }
        }
    }
}
