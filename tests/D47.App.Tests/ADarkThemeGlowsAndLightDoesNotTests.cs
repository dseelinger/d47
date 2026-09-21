using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Bloom and scanlines (#345): present in every dark theme, absent in Light.</summary>
public class ADarkThemeGlowsAndLightDoesNotTests
{
    private static ThemeManager Manager() =>
        new(Application.Current!, NullLogger<ThemeManager>.Instance);

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Guardian)]
    [InlineData(ThemeCatalog.ElitePaletteId)]
    public void EveryDarkThemeCarriesBloomAndScanlines(string themeId)
    {
        Manager().Apply(themeId);

        var resources = Application.Current!.Resources;

        Assert.IsType<DropShadowEffect>(resources[ThemeManager.BloomKey]);
        Assert.IsType<ImageBrush>(resources[ThemeManager.ScanlinesKey]);
    }

    /// <summary>The handoff's literal values: 10px, 34%, no offset (#345's design correction).</summary>
    [AvaloniaFact]
    public void TheBloomMatchesTheHandoffsNumbers()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var bloom = (DropShadowEffect)Application.Current!.Resources[ThemeManager.BloomKey]!;

        Assert.Equal(10, bloom.BlurRadius);
        Assert.Equal(0.34, bloom.Opacity);
        Assert.Equal(0, bloom.OffsetX);
        Assert.Equal(0, bloom.OffsetY);
    }

    [AvaloniaFact]
    public void LightCarriesNoBloomAndNoScanlinesButStillDrawsTheRule()
    {
        Manager().Apply(ThemeCatalog.Light);

        var resources = Application.Current!.Resources;

        Assert.Null(resources[ThemeManager.BloomKey]);
        Assert.Null(resources[ThemeManager.ScanlinesKey]);
        Assert.IsType<SolidColorBrush>(resources[ThemeManager.RuleKey]);
    }

    /// <summary>A theme switch recomputes the key, rather than leaving Light with Elite's value still set.</summary>
    [AvaloniaFact]
    public void SwitchingBackToLightTurnsBloomOff()
    {
        var manager = Manager();
        manager.Apply(ThemeCatalog.Elite);
        manager.Apply(ThemeCatalog.Light);

        var resources = Application.Current!.Resources;

        Assert.Null(resources[ThemeManager.BloomKey]);
        Assert.Null(resources[ThemeManager.ScanlinesKey]);
    }

    [AvaloniaFact]
    public void TheScanlineLayerNeverReceivesAPointerPress()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var view = new PanelView { DataContext = new PanelViewModel() };
        using var surface = new OffscreenSurface(view, new PixelSize(1180, 880));
        surface.Render();

        var scanlines = view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Scanlines");

        Assert.False(scanlines.IsHitTestVisible);
    }

    /// <summary>Drawn last, over the frame and every page inside it, rather than under any of them.</summary>
    [AvaloniaFact]
    public void TheScanlineLayerIsTheTopmostChildOfTheWindow()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var view = new PanelView { DataContext = new PanelViewModel() };
        using var surface = new OffscreenSurface(view, new PixelSize(1180, 880));
        surface.Render();

        var scanlines = view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Scanlines");
        var root = (Avalonia.Controls.Panel)scanlines.GetVisualParent()!;

        Assert.Same(scanlines, root.Children[^1]);
    }

    /// <summary>The overlay is 55% opaque as a whole, on top of the tile's own 34% alpha line (#345).</summary>
    [AvaloniaFact]
    public void TheScanlineLayerIsFiftyFivePercentOpaque()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var view = new PanelView { DataContext = new PanelViewModel() };
        using var surface = new OffscreenSurface(view, new PixelSize(1180, 880));
        surface.Render();

        var scanlines = view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Scanlines");

        Assert.Equal(0.55, scanlines.Opacity);
    }

    /// <summary>The unselected tab's label carries no Effect at all — it sits on a tinted fill, where a glow has
    /// nowhere to spread.</summary>
    [AvaloniaFact]
    public void TheUnselectedTabsLabelHasNoEffect()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var view = new PanelView { DataContext = new PanelViewModel() };
        using var surface = new OffscreenSurface(view, new PixelSize(1180, 880));
        surface.Render();

        var unselected = view.FindControl<RadioButton>("LoadoutTab")!;
        unselected.ApplyTemplate();

        var label = unselected.GetVisualDescendants().OfType<ContentPresenter>().Single(c => c.Name == "PART_ContentPresenter");

        Assert.Null(label.Effect);
    }

    /// <summary>The tab-strip rule and the panel's outer edge draw in every theme, but neither carries a glow
    /// any more — only the elements #345 names do.</summary>
    [AvaloniaFact]
    public void TheTabStripRuleAndTheEdgeGlowNeverGlow()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var view = new PanelView { DataContext = new PanelViewModel() };
        using var surface = new OffscreenSurface(view, new PixelSize(1180, 880));
        surface.Render();

        var rule = view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "TabStripRule");
        var edge = view.GetVisualDescendants().OfType<ChamferedBorder>().Single(b => b.Name == "EdgeGlow");

        Assert.Null(rule.Effect);
        Assert.Null(edge.Effect);
    }

    /// <summary>The panel's outer edge draws behind the frame rather than on it, so the frame's own text does
    /// not render into its offscreen layer, and is never hit-testable.</summary>
    [AvaloniaFact]
    public void TheEdgeGlowIsAnEmptySiblingBehindTheFrame()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var view = new PanelView { DataContext = new PanelViewModel() };
        using var surface = new OffscreenSurface(view, new PixelSize(1180, 880));
        surface.Render();

        var edgeGlow = view.GetVisualDescendants().OfType<ChamferedBorder>().Single(b => b.Name == "EdgeGlow");
        var root = (Avalonia.Controls.Panel)edgeGlow.GetVisualParent()!;
        var frameIndex = root.Children.ToList().FindIndex(c => c is ChamferedBorder chamfered && chamfered.Name != "EdgeGlow");
        var edgeIndex = root.Children.IndexOf(edgeGlow);

        Assert.False(edgeGlow.IsHitTestVisible);
        Assert.True(edgeIndex < frameIndex);
    }

    [AvaloniaFact]
    public void TheTabStripRuleStillDrawsInLight()
    {
        Manager().Apply(ThemeCatalog.Light);

        var view = new PanelView { DataContext = new PanelViewModel() };
        using var surface = new OffscreenSurface(view, new PixelSize(1180, 880));
        surface.Render();

        var rule = view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "TabStripRule");

        Assert.IsType<SolidColorBrush>(rule.Background);
        Assert.Null(rule.Effect);
    }
}
