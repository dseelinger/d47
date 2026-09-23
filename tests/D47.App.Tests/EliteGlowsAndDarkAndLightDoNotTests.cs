using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Bloom and scanlines: on for Elite and the HUD-matrix theme, off for Dark and Light.</summary>
public class EliteGlowsAndDarkAndLightDoNotTests
{
    private static ThemeManager Manager() =>
        new(Application.Current!, NullLogger<ThemeManager>.Instance);

    private static IEnumerable<string> StopKeys() =>
        Enum.GetValues<BloomTier>().SelectMany(tier =>
            Enumerable.Range(0, BloomTiers.Table(tier).Count).Select(stop => ThemeManager.BloomStopKey(tier, stop)));

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.ElitePaletteId)]
    public void AGlowingThemeCarriesEveryBloomStopAndScanlines(string themeId)
    {
        Manager().Apply(themeId);

        var resources = Application.Current!.Resources;

        foreach (var key in StopKeys())
        {
            var stop = Assert.IsType<DropShadowEffect>(resources[key]);
            Assert.InRange(stop.Opacity, 0, 1);
        }

        Assert.IsType<ImageBrush>(resources[ThemeManager.ScanlinesKey]);
    }

    /// <summary>The Bloom setting is disabled on exactly the themes whose palette does not glow.</summary>
    [Fact]
    public void TheBloomRowIsDisabledWhereThePaletteDoesNotGlow()
    {
        var row = InterfaceCapability.Create().Settings.Single(r => r.Key == InterfaceCapability.BloomKey);

        foreach (var id in ThemeCatalog.Ids)
        {
            var settings = D47Settings.Defaults with { Ui = D47Settings.Defaults.Ui with { Theme = id } };

            Assert.Equal(!Palettes.For(id).Glows, row.DisabledWhen!(settings));
        }
    }

    /// <summary>The bloom tier's widest stop, [38, 22] at amount 1.1, with no offset.</summary>
    [AvaloniaFact]
    public void TheBloomTierMatchesTheHandoffsStops()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var widest = (DropShadowEffect)Application.Current!.Resources[ThemeManager.BloomStopKey(BloomTier.Normal, 3)]!;

        Assert.Equal(ThemeManager.SkiaBlurRadius(38 * 1.1), widest.BlurRadius, 9);
        Assert.Equal(0.22 * 1.1, widest.Opacity, 9);
        Assert.Equal(0, widest.OffsetX);
        Assert.Equal(0, widest.OffsetY);
    }

    /// <summary>Avalonia.Skia's σ for the converted radius is the CSS σ of r / 2.</summary>
    [Theory]
    [InlineData(38)]
    [InlineData(112)]
    [InlineData(6)]
    public void AConvertedRadiusSpreadsAsFarAsTheCssOne(double css) =>
        Assert.Equal(css / 2, (0.288675 * ThemeManager.SkiaBlurRadius(css)) + 0.5, 9);

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Light)]
    public void DarkAndLightCarryNoBloomAndNoScanlinesButStillDrawTheRule(string themeId)
    {
        Manager().Apply(themeId);

        var resources = Application.Current!.Resources;

        Assert.All(StopKeys(), key => Assert.Null(resources[key]));
        Assert.Null(resources[ThemeManager.ScanlinesKey]);
        Assert.IsType<SolidColorBrush>(resources[ThemeManager.RuleKey]);
    }

    /// <summary>A theme switch recomputes the keys, rather than leaving Light with Elite's value still set.</summary>
    [AvaloniaFact]
    public void SwitchingBackToLightTurnsBloomOff()
    {
        var manager = Manager();
        manager.Apply(ThemeCatalog.Elite);
        manager.Apply(ThemeCatalog.Light);

        var resources = Application.Current!.Resources;

        Assert.All(StopKeys(), key => Assert.Null(resources[key]));
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

    /// <summary>The overlay is half opaque as a whole, on top of the tile's own 30% alpha line.</summary>
    [AvaloniaFact]
    public void TheScanlineLayerIsHalfOpaque()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var view = new PanelView { DataContext = new PanelViewModel() };
        using var surface = new OffscreenSurface(view, new PixelSize(1180, 880));
        surface.Render();

        var scanlines = view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Scanlines");

        Assert.Equal(0.5, scanlines.Opacity);
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

    /// <summary>The tab-strip rule draws in every theme but carries no glow — only the elements #345 names do.</summary>
    [AvaloniaFact]
    public void TheTabStripRuleNeverGlows()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var view = new PanelView { DataContext = new PanelViewModel() };
        using var surface = new OffscreenSurface(view, new PixelSize(1180, 880));
        surface.Render();

        var rule = view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "TabStripRule");

        Assert.Null(rule.Effect);
    }

    /// <summary>The window edge is the panel's edge: no frame, no chamfer, no edge glow — 32px of padding.</summary>
    [AvaloniaFact]
    public void ThePanelHasNoFrame()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var view = new PanelView { DataContext = new PanelViewModel() };
        using var surface = new OffscreenSurface(view, new PixelSize(1180, 880));
        surface.Render();

        var frame = view.GetVisualDescendants().OfType<DockPanel>().Single(d => d.Name == "Frame");

        Assert.Equal(new Thickness(32), frame.Margin);
        Assert.Empty(frame.GetVisualAncestors().OfType<ChamferedBorder>());
        Assert.Equal(default, view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "ContentPane").BorderThickness);
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
