using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>On the headset panel, the ray drags the rule between two panes the way it drags a scrollbar.</summary>
public sealed class TheRayDragsTheRuleBetweenPanesTests
{
    private sealed record Rig(VrPanelSurface Panel, PanelView View, ViewStateStore Store) : IDisposable
    {
        public DrillView Strip => View.GetVisualDescendants().OfType<DrillView>().Single(strip => strip.Bounds.Width > 0);

        public Grid Grid => Strip.GetVisualDescendants().OfType<Grid>().First();

        public Control Rule(int column) => Strip.RayRules.Single(rule => Avalonia.Controls.Grid.GetColumn(rule) == column);

        public string Words =>
            string.Join(
                ",",
                Grid.Children.OfType<Border>().Select(host => host.Child).OfType<Control>()
                    .Select(pane => pane is TextBlock text ? text.Text : (pane as ScrollViewer)?.Name));

        /// <summary>Where a control's vertical middle line is, in the surface's pixels.</summary>
        public Point Middle(Control control)
        {
            var at = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), View);
            Assert.NotNull(at);
            return at.Value;
        }

        public (float U, float V) Ray(Point at)
        {
            var (width, height) = Panel.Size;
            return ((float)(at.X / width), (float)(at.Y / height));
        }

        public bool Grab(Point at)
        {
            var (u, v) = Ray(at);
            var taken = Panel.GrabsScroll(u, v);
            Panel.Serve();
            return taken;
        }

        public void Drag(Point at)
        {
            var (u, v) = Ray(at);
            Panel.Scroll(u, v);
            Panel.Serve();
        }

        public void Aim(Point at)
        {
            var (u, v) = Ray(at);
            Panel.Aim(u, v);
        }

        public void Release()
        {
            Panel.ReleaseScroll();
            Panel.Serve();
        }

        public void Dispose() => Panel.Dispose();
    }

    private static Rig Headset(bool scrolling = false, ViewStateStore? store = null)
    {
        var (settings, created, _) = TestSurface.Create();
        store ??= created;

        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);

        var panel = new VrPanelSurface(
            new PanelViewModel(), settings, _ => null, paneWidths: new PaneWidthMemory(store, headset: true));

        var view = (PanelView)typeof(VrPanelSurface)
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(panel)!;

        view.Furnish(
            PanelTab.Assets,
            crumb => crumb.Key == "fleet" && scrolling ? Scrolling() : new TextBlock { Text = crumb.Word },
            new NavCrumb("fleet", "Ships"));

        view.Tab = PanelTab.Assets;
        view.Nav.GoTo(new NavCrumb("fleet", "Ships"), new NavCrumb("ship:12", "Corsair"));

        panel.Serve();
        panel.Serve();

        return new Rig(panel, view, store);
    }

    /// <summary>Loads a palette, so the lit rule has an accent to draw with.</summary>
    private static void Palette() =>
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Dark);

    /// <summary>A pane long enough to scroll, so its bar sits just left of the rule.</summary>
    private static ScrollViewer Scrolling()
    {
        var lines = new StackPanel();

        for (var line = 0; line < 200; line++)
        {
            lines.Children.Add(new TextBlock { Text = $"Ship {line}" });
        }

        return new ScrollViewer { Name = "Ships", Content = lines, VerticalScrollBarVisibility = ScrollBarVisibility.Visible };
    }

    [AvaloniaFact]
    public void TheHeadsetStripHasARuleTheRayCanTakeAndNoMouseHandle()
    {
        using var rig = Headset();

        Assert.Equal("Ships,Corsair", rig.Words);
        Assert.Equal(2, rig.Grid.ColumnDefinitions.Count);
        Assert.Single(rig.Strip.RayRules);
        Assert.Empty(rig.Grid.Children.OfType<GridSplitter>());
    }

    [AvaloniaFact]
    public void ARuleLightsWithinReachAndGoesOutPastTheRelease()
    {
        Palette();
        using var rig = Headset();
        var rule = rig.Rule(1);
        var line = rig.Middle(rule);

        Assert.Null(((Border)rule).Background);

        rig.Aim(line + new Point(OffscreenSurface.AimTolerance - 2, 0));
        Assert.Contains(":pointerover", rule.Classes);
        Assert.NotNull(((Border)rule).Background);

        // Lit, it keeps its light out to the wider berth, as a scrollbar does.
        rig.Aim(line + new Point(OffscreenSurface.AimRelease - 2, 0));
        Assert.Contains(":pointerover", rule.Classes);

        rig.Aim(line + new Point(OffscreenSurface.AimRelease + 4, 0));
        Assert.DoesNotContain(":pointerover", rule.Classes);
        Assert.Null(((Border)rule).Background);

        // And having gone out, it lights again only within the narrower reach.
        rig.Aim(line + new Point(OffscreenSurface.AimRelease - 2, 0));
        Assert.DoesNotContain(":pointerover", rule.Classes);
    }

    [AvaloniaFact]
    public void HoldingTheTriggerMovesTheRuleAndPanesStopAtTheirMinimum()
    {
        using var rig = Headset();
        var line = rig.Middle(rig.Rule(1));

        Assert.True(rig.Grab(line + new Point(10, 0)), "a ray near the rule takes hold of it");

        rig.Drag(line + new Point(-50, 0));
        rig.Drag(line + new Point(70, 0));

        // The rule keeps the distance it was taken at, rather than jumping under the ray.
        var columns = rig.Grid.ColumnDefinitions;
        Assert.True(columns[0].ActualWidth > columns[1].ActualWidth, "the left pane is wider after the rule moved right");
        Assert.Equal(line.X + 60, rig.Middle(rig.Rule(1)).X, 1);

        rig.Drag(new Point(line.X + 2000, line.Y));

        Assert.Equal(DrillView.MinimumPaneWidth, columns[1].ActualWidth, 1);

        rig.Drag(new Point(line.X - 2000, line.Y));

        Assert.Equal(DrillView.MinimumPaneWidth, columns[0].ActualWidth, 1);
    }

    [AvaloniaFact]
    public void LettingGoRemembersTheRuleForTheHeadsetAlone()
    {
        using var rig = Headset();
        var line = rig.Middle(rig.Rule(1));

        rig.Grab(line);
        rig.Drag(line + new Point(50, 0));
        rig.Release();

        var columns = rig.Grid.ColumnDefinitions;
        var share = columns[0].ActualWidth / (columns[0].ActualWidth + columns[1].ActualWidth);
        var state = rig.Store.Load();

        Assert.Equal(share, state.SharesFor(2, headset: true)![0], 2);
        Assert.Null(state.SharesFor(2));

        // Read back by a headset started again on the same store.
        using var restarted = Headset(store: rig.Store);
        var again = restarted.Grid.ColumnDefinitions;

        Assert.Equal(share, again[0].ActualWidth / (again[0].ActualWidth + again[1].ActualWidth), 2);
    }

    [AvaloniaFact]
    public void ReleasedAtTheLeftEdgeTheLeftmostPaneHidesAndTheEdgeBringsItBack()
    {
        Palette();
        using var rig = Headset();
        var line = rig.Middle(rig.Rule(1));
        var left = rig.Strip.TranslatePoint(new Point(0, 0), rig.View)!.Value.X;

        rig.Grab(line);
        rig.Drag(new Point(left + 20, line.Y));
        rig.Release();

        Assert.Equal("Corsair", rig.Words);
        Assert.True(rig.Store.Load().HeadsetLeftmostPaneHidden);
        Assert.False(rig.Store.Load().LeftmostPaneHidden);

        var edge = rig.Rule(0);

        rig.Aim(rig.Middle(edge) + new Point(10, 0));
        Assert.NotNull(((Border)edge).Background);

        Assert.True(rig.Grab(rig.Middle(edge) + new Point(4, 0)), "the rule left at the edge can be taken again");
        rig.Drag(new Point(left + 500, line.Y));
        rig.Release();

        Assert.Equal("Ships,Corsair", rig.Words);
        Assert.False(rig.Store.Load().HeadsetLeftmostPaneHidden);

        using var restarted = Headset(store: rig.Store);
        Assert.Equal("Ships,Corsair", restarted.Words);
    }

    [AvaloniaFact]
    public void ATakeAndReleaseWithoutMovingLeavesTheRuleWhereItWas()
    {
        using var rig = Headset();
        var line = rig.Middle(rig.Rule(1));

        Assert.True(rig.Grab(line + new Point(OffscreenSurface.AimTolerance - 2, 0)));
        rig.Release();

        Assert.Equal(line.X, rig.Middle(rig.Rule(1)).X, 1);
        Assert.Equal("Ships,Corsair", rig.Words);
    }

    [AvaloniaFact]
    public void APointerLostNearTheEdgeHidesNoPane()
    {
        using var rig = Headset();
        var line = rig.Middle(rig.Rule(1));
        var left = rig.Strip.TranslatePoint(new Point(0, 0), rig.View)!.Value.X;

        rig.Grab(line);
        rig.Drag(new Point(left + 20, line.Y));
        rig.Panel.ReleaseScroll(decide: false);
        rig.Panel.Serve();

        Assert.Equal("Ships,Corsair", rig.Words);
        Assert.False(rig.Store.Load().HeadsetLeftmostPaneHidden);
    }

    [AvaloniaFact]
    public void TheNearerOfARuleAndAScrollbarIsTaken()
    {
        using var rig = Headset(scrolling: true);
        var rule = rig.Rule(1);
        var bar = rig.Strip.GetVisualDescendants().OfType<ScrollBar>()
            .Single(found => found.Orientation == Avalonia.Layout.Orientation.Vertical && found.IsVisible && found.Maximum > 0);

        var line = rig.Middle(rule);
        var middle = rig.Middle(bar);

        Assert.InRange(line.X - middle.X, 1, OffscreenSurface.AimTolerance);

        rig.Aim(new Point(middle.X, line.Y));
        Assert.Contains(":pointerover", bar.Classes);
        Assert.DoesNotContain(":pointerover", rule.Classes);

        rig.Aim(new Point(rig.Panel.Size.Width, line.Y));
        rig.Aim(line);
        Assert.Contains(":pointerover", rule.Classes);
        Assert.DoesNotContain(":pointerover", bar.Classes);
    }

    [AvaloniaFact]
    public void APressAwayFromARuleOrABarIsAPress()
    {
        using var rig = Headset();
        var pane = rig.Grid.Children.OfType<Border>().Last(host => host.Child is not null);

        Assert.False(rig.Grab(rig.Middle(pane)));
    }

    [AvaloniaFact]
    public void TheDesktopStripKeepsItsMouseHandlesAndItsOwnShares()
    {
        var (_, store, _) = TestSurface.Create();
        var desktop = new PaneWidthMemory(store);
        var headset = new PaneWidthMemory(store, headset: true);

        headset.Remember(2, [0.7, 0.3]);
        headset.RememberLeftmostHidden(true);

        Assert.Null(desktop.Remembered(2));
        Assert.False(desktop.LeftmostHidden);

        desktop.Remember(2, [0.4, 0.6]);

        Assert.Equal([0.7, 0.3], new PaneWidthMemory(store, headset: true).Remembered(2));

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.Furnish(PanelTab.Assets, crumb => new TextBlock { Text = crumb.Word }, new NavCrumb("fleet", "Ships"));

        var window = new Window { Content = panel, Width = 1200, Height = 700 };
        window.Show();
        panel.EnableDraggablePanes(desktop);
        panel.Tab = PanelTab.Assets;
        panel.Nav.GoTo(new NavCrumb("fleet", "Ships"), new NavCrumb("ship:12", "Corsair"));
        Dispatcher.UIThread.RunJobs();

        var strip = panel.GetVisualDescendants().OfType<DrillView>().Single();

        Assert.Single(strip.GetVisualDescendants().OfType<GridSplitter>());
        Assert.Empty(strip.RayRules);
        Assert.Same(desktop, ((IPanelTabHost)panel).PaneWidths);

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetSearchFormHasNoMouseSplitter()
    {
        using var rig = Headset();

        Assert.Null(((IPanelTabHost)rig.View).PaneWidths);
    }
}
