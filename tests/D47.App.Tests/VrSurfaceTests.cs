using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia.Controls;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The headset panel surface: which mode it is in, which slot it reads, and — the one that
/// matters — that going mini in the headset does not take the desktop window with it.
/// </summary>
public class VrSurfaceTests
{
    /// <summary>
    /// The bug this pins is the obvious way to build mini and the wrong one. Both surfaces bind
    /// one view model, so a mode held there would put the window into mini the moment the
    /// headset went into it — and it would look completely correct in every test that only ever
    /// built one surface.
    /// </summary>
    [AvaloniaFact]
    public void GoingMiniInTheHeadsetLeavesTheDesktopWindowAlone()
    {
        var (settings, _, _) = TestSurface.Create();
        var model = new PanelViewModel();

        var window = new PanelView { DataContext = model };
        using var headset = new VrPanelSurface(model, settings, _ => null);

        settings.Apply(VrCapability.ModeKey, "mini", SettingsCaller.Panel);
        headset.ApplyMode();

        Assert.Equal(PanelMode.Mini, headset.Mode);
        Assert.Equal(PanelMode.Full, window.Mode);

        // And they are still showing the same thing, which is the whole point of the split.
        model.Append("Fixture One, docked.");
        Assert.Contains("Fixture One", model.TranscriptText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void MiniIsASmallerImageAtASmallerWidthRatherThanTheSameOneHungNearer()
    {
        var (settings, _, _) = TestSurface.Create();

        // Full, explicitly. Mini is the shipped default now, and this test is about the
        // full panel's own settings slot rather than about which mode d47 starts in.
        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);
        using var surface = new VrPanelSurface(new PanelViewModel(), settings, _ => null);

        var full = (surface.Size, surface.Placement.WidthMetres);

        settings.Apply(VrCapability.ModeKey, "mini", SettingsCaller.Panel);
        var mini = (surface.Size, surface.Placement.WidthMetres);

        Assert.True(mini.Size.Width < full.Size.Width);
        Assert.True(mini.WidthMetres < full.WidthMetres);
    }

    [AvaloniaFact]
    public void EachModeReadsItsOwnPlacementSlot()
    {
        var (settings, _, _) = TestSurface.Create();

        // Full, explicitly. Mini is the shipped default now, and this test is about the
        // full panel's own settings slot rather than about which mode d47 starts in.
        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);
        using var surface = new VrPanelSurface(new PanelViewModel(), settings, _ => null);

        Assert.Equal(VrCapability.PanelSlot, surface.Slot);

        settings.Apply(VrCapability.ModeKey, "mini", SettingsCaller.Panel);
        Assert.Equal(VrCapability.MiniSlot, surface.Slot);

        // Changing one does not move the other.
        settings.Apply("vr.mini.size", "0.5", SettingsCaller.Panel);
        Assert.Equal(0.5f, surface.Placement.WidthMetres, 3);

        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);
        Assert.NotEqual(0.5f, surface.Placement.WidthMetres, 3);
    }

    /// <summary>
    /// A surface that has been put down reads its pose from view state and everything else from
    /// settings. Joined at the surface rather than in either store, because the choice to be
    /// world-locked is a preference and where a hand left it is not.
    /// </summary>
    [AvaloniaFact]
    public void AWorldLockedSurfaceTakesItsPoseFromTheAnchorAndItsLookFromSettings()
    {
        var (settings, _, _) = TestSurface.Create();

        // Full, explicitly. Mini is the shipped default now, and this test is about the
        // full panel's own settings slot rather than about which mode d47 starts in.
        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);

        var placed = new VrPose(new System.Numerics.Vector3(0.4f, 1.3f, -0.9f), System.Numerics.Quaternion.Identity);
        var against = new VrPose(new System.Numerics.Vector3(0, 1.6f, 0), System.Numerics.Quaternion.Identity);

        using var surface = new VrPanelSurface(new PanelViewModel(), settings, _ => (placed, against));

        settings.Apply(VrCapability.LockKey(VrCapability.PanelSlot), "world", SettingsCaller.Panel);
        settings.Apply("vr.panel.curve", "0.3", SettingsCaller.Panel);

        var placement = surface.Placement;

        Assert.Equal(SurfaceLock.WorldLocked, placement.Lock);
        Assert.Equal(0.3f, placement.Curvature, 3);
        Assert.Equal(placed.Position.X, placement.Where(against).Position.X, 4);
    }

    [AvaloniaFact]
    public void CurvatureReachingZeroIsWhatFlatMeans()
    {
        var (settings, _, _) = TestSurface.Create();

        // Full, explicitly. Mini is the shipped default now, and this test is about the
        // full panel's own settings slot rather than about which mode d47 starts in.
        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);
        using var surface = new VrPanelSurface(new PanelViewModel(), settings, _ => null);

        // The default is flat, and there is no second mode that could disagree with it.
        Assert.Equal(0f, surface.Placement.Curvature);

        settings.Apply("vr.panel.curve", "0.35", SettingsCaller.Panel);
        Assert.Equal(0.35f, surface.Placement.Curvature, 3);

        settings.Apply("vr.panel.curve", "0", SettingsCaller.Panel);
        Assert.Equal(0f, surface.Placement.Curvature);
    }

    /// <summary>
    /// Scale changes how large everything on the panel is drawn; mini changes how much of it
    /// there is. The distinction is the one the checklist draws, so it is worth an assertion
    /// that they are not the same lever.
    /// </summary>
    [AvaloniaFact]
    public void ScaleAndMiniAreDifferentLevers()
    {
        var (settings, _, _) = TestSurface.Create();

        // Full, explicitly. Mini is the shipped default now, and this test is about the
        // full panel's own settings slot rather than about which mode d47 starts in.
        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);
        using var surface = new VrPanelSurface(new PanelViewModel(), settings, _ => null);

        var before = surface.Size;

        settings.Apply("vr.panel.scale", "150", SettingsCaller.Panel);
        surface.Configure();

        // Scaling does not change how many pixels the surface is: it changes what fits in them.
        Assert.Equal(before, surface.Size);
        Assert.Equal(150, surface.Placement.ZoomPercent);
    }

    [AvaloniaFact]
    public void ThePlacementRowsExistForBothSurfacesAndNoneOfThemIsProtected()
    {
        var (settings, _, _) = TestSurface.Create();

        foreach (var slot in new[] { VrCapability.PanelSlot, VrCapability.MiniSlot })
        {
            foreach (var name in new[] { "lock", "distance", "size", "curve", "scale" })
            {
                var row = settings.Find($"vr.{slot}.{name}");

                Assert.NotNull(row);

                // Placement is cosmetic. Nothing here reaches anything safety-critical, so
                // there is no reason the model should not be able to move a panel.
                Assert.False(row.Protected);
            }
        }

        // Opacity is not in that list because it is not per-surface: one knob answers for both
        // panels, which is why "set the opacity" can no longer land on the one you cannot see.
        var opacity = settings.Find(VrCapability.OpacityKey);

        Assert.NotNull(opacity);
        Assert.False(opacity.Protected);
        Assert.Null(settings.Find($"vr.{VrCapability.PanelSlot}.opacity"));
        Assert.Null(settings.Find($"vr.{VrCapability.MiniSlot}.opacity"));
    }

    [AvaloniaFact]
    public void MiniOpensSmallerAndNearerThanTheFullPanel()
    {
        var defaults = D47Settings.Defaults.Vr;

        Assert.True(defaults.Mini.Width < defaults.Panel.Width);
        Assert.True(defaults.Mini.Distance < defaults.Panel.Distance);
    }

    [AvaloniaFact]
    public void TheCaptionSurfaceIsNotReachableFromThePlacementRows()
    {
        var (settings, _, _) = TestSurface.Create();

        // Captions are their own overlay precisely so Overlay Positioning cannot touch them.
        Assert.Null(settings.Find("vr.captions.lock"));
        Assert.Null(settings.Find("vr.captions.distance"));
        Assert.Null(settings.Find("vr.captions.curve"));
    }

    [AvaloniaFact]
    public void MiniHidesTheHeaderTheBannersAndTheAskBoxAndKeepsTheTranscript()
    {
        var model = new PanelViewModel();
        model.Append("Fixture Anchorage, 12.4 ly.");

        var view = new PanelView { DataContext = model, Mode = PanelMode.Mini };

        foreach (var name in new[] { "Header", "Banners", "AskRow" })
        {
            Assert.False(Named(view, name).IsVisible);
        }

        Assert.True(Named(view, "TranscriptScroller").IsVisible);
        Assert.True(Named(view, "TurnLine").IsVisible);
    }

    /// <summary>
    /// The headset card, captured for a human to look at. It is the largest section the
    /// settings surface has grown - a switch, a mode, twelve placement rows across two
    /// surfaces and four caption rows - and whether that reads as a panel or as a wall is a
    /// question only eyes answer.
    /// </summary>
    [AvaloniaFact]
    public void TheHeadsetSettingsCardRendersToACapture()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new Theming.ThemeManager(
                Avalonia.Application.Current!,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Theming.ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        var scroller = host.View.GetVisualDescendants().OfType<ScrollViewer>().First();
        // Inside the scroller, so this is the card's heading and not the nav item of the same
        // name in the sidebar — which sits at the top and would scroll nowhere.
        var card = scroller.GetVisualDescendants()
            .OfType<TextBlock>()
            .First(text => text.Text == "Headset");

        // Scrolled by offset rather than by BringIntoView, which does nothing before the
        // surface has settled and leaves the capture showing whatever was at the top.
        scroller.Offset = scroller.Offset.WithY(Math.Max(0, TopOf(card, (Avalonia.Visual)scroller.Content!) - 24));

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var frame = host.Window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        frame.Save(
            Path.Combine(TestSurface.CaptureDirectory, "settings-headset.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        host.Close();
    }

    /// <summary>
    /// How far down the scrolled content a control sits. Summed up the visual ancestry because
    /// Bounds is relative to a parent, and the alternative — BringIntoView — does nothing
    /// before the surface has settled and leaves the capture showing whatever was at the top.
    /// </summary>
    private static double TopOf(Avalonia.Visual control, Avalonia.Visual within)
    {
        var top = 0.0;

        for (var at = control; at is not null && at != within; at = at.GetVisualParent())
        {
            top += at.Bounds.Y;
        }

        return top;
    }

    private static Control Named(Control view, string name) =>
        view.GetLogicalDescendants().OfType<Control>().Single(control => control.Name == name);
}
