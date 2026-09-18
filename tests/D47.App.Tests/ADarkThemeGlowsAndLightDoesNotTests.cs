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

/// <summary>Bloom and scanlines (#281, recalibrated #285): present in every dark theme, absent in Light.</summary>
public class ADarkThemeGlowsAndLightDoesNotTests
{
    private static ThemeManager Manager() =>
        new(Application.Current!, NullLogger<ThemeManager>.Instance);

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Guardian)]
    [InlineData(ThemeCatalog.ElitePaletteId)]
    public void EveryDarkThemeCarriesEveryBloomAndScanlines(string themeId)
    {
        Manager().Apply(themeId);

        var resources = Application.Current!.Resources;

        Assert.IsType<DropShadowEffect>(resources[ThemeManager.BloomFillKey]);
        Assert.IsType<DropShadowEffect>(resources[ThemeManager.BloomFillHeadsetKey]);
        Assert.IsType<DropShadowEffect>(resources[ThemeManager.BloomRuleKey]);
        Assert.IsType<DropShadowEffect>(resources[ThemeManager.BloomRuleHeadsetKey]);
        Assert.IsType<DropShadowEffect>(resources[ThemeManager.BloomEdgeKey]);
        Assert.IsType<DropShadowEffect>(resources[ThemeManager.BloomEdgeHeadsetKey]);
        Assert.IsType<ImageBrush>(resources[ThemeManager.ScanlinesKey]);
    }

    /// <summary>Each headset value reads stronger than its desktop counterpart — #285's whole point.</summary>
    [AvaloniaFact]
    public void EveryHeadsetGlowIsStrongerThanItsDesktopCounterpart()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var resources = Application.Current!.Resources;

        void AssertStronger(string desktopKey, string headsetKey)
        {
            var desktop = (DropShadowEffect)resources[desktopKey]!;
            var headset = (DropShadowEffect)resources[headsetKey]!;

            Assert.True(headset.BlurRadius > desktop.BlurRadius);
            Assert.True(headset.Opacity > desktop.Opacity);
        }

        AssertStronger(ThemeManager.BloomFillKey, ThemeManager.BloomFillHeadsetKey);
        AssertStronger(ThemeManager.BloomRuleKey, ThemeManager.BloomRuleHeadsetKey);
        AssertStronger(ThemeManager.BloomEdgeKey, ThemeManager.BloomEdgeHeadsetKey);
    }

    /// <summary>The rule glow's shadow falls downward, the way the design asks for it.</summary>
    [AvaloniaFact]
    public void TheTabStripRuleGlowFallsDownward()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var resources = Application.Current!.Resources;

        Assert.Equal(2, ((DropShadowEffect)resources[ThemeManager.BloomRuleKey]!).OffsetY);
        Assert.Equal(2, ((DropShadowEffect)resources[ThemeManager.BloomRuleHeadsetKey]!).OffsetY);
    }

    [AvaloniaFact]
    public void LightCarriesNoBloomAndNoScanlinesButStillDrawsTheRule()
    {
        Manager().Apply(ThemeCatalog.Light);

        var resources = Application.Current!.Resources;

        Assert.Null(resources[ThemeManager.BloomFillKey]);
        Assert.Null(resources[ThemeManager.BloomFillHeadsetKey]);
        Assert.Null(resources[ThemeManager.BloomRuleKey]);
        Assert.Null(resources[ThemeManager.BloomRuleHeadsetKey]);
        Assert.Null(resources[ThemeManager.BloomEdgeKey]);
        Assert.Null(resources[ThemeManager.BloomEdgeHeadsetKey]);
        Assert.Null(resources[ThemeManager.ScanlinesKey]);
        Assert.IsType<SolidColorBrush>(resources[ThemeManager.TabStripRuleKey]);
    }

    /// <summary>A theme switch recomputes every key, rather than leaving Light with Elite's values still set.</summary>
    [AvaloniaFact]
    public void SwitchingBackToLightTurnsEveryBloomOff()
    {
        var manager = Manager();
        manager.Apply(ThemeCatalog.Elite);
        manager.Apply(ThemeCatalog.Light);

        var resources = Application.Current!.Resources;

        Assert.Null(resources[ThemeManager.BloomFillKey]);
        Assert.Null(resources[ThemeManager.BloomFillHeadsetKey]);
        Assert.Null(resources[ThemeManager.BloomRuleKey]);
        Assert.Null(resources[ThemeManager.BloomRuleHeadsetKey]);
        Assert.Null(resources[ThemeManager.BloomEdgeKey]);
        Assert.Null(resources[ThemeManager.BloomEdgeHeadsetKey]);
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

    /// <summary>The unselected tab's label carries no Effect at all — it sits on a tinted fill, where a glow has
    /// nowhere to spread (#285).</summary>
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

    /// <summary>The desktop's PanelView resolves the desktop bloom values; the headset's copy — carrying the
    /// "headset" class — resolves the stronger ones (#285).</summary>
    [AvaloniaFact]
    public void TheHeadsetPanelResolvesTheHeadsetValuesAndTheDesktopPanelResolvesTheDesktopValues()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var desktopView = new PanelView { DataContext = new PanelViewModel() };
        using var desktopSurface = new OffscreenSurface(desktopView, new PixelSize(1180, 880));
        desktopSurface.Render();

        var headsetView = new PanelView { DataContext = new PanelViewModel() };
        headsetView.Classes.Add("headset");
        using var headsetSurface = new OffscreenSurface(headsetView, new PixelSize(1180, 880));
        headsetSurface.Render();

        var desktopEdge = desktopView.GetVisualDescendants().OfType<ChamferedBorder>().Single(b => b.Name == "EdgeGlow");
        var headsetEdge = headsetView.GetVisualDescendants().OfType<ChamferedBorder>().Single(b => b.Name == "EdgeGlow");

        var desktopEffect = (DropShadowEffect)desktopEdge.Effect!;
        var headsetEffect = (DropShadowEffect)headsetEdge.Effect!;

        Assert.Equal(22, desktopEffect.BlurRadius);
        Assert.Equal(44, headsetEffect.BlurRadius);

        var desktopRule = desktopView.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "TabStripRule");
        var headsetRule = headsetView.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "TabStripRule");

        var desktopRuleEffect = (DropShadowEffect)desktopRule.Effect!;
        var headsetRuleEffect = (DropShadowEffect)headsetRule.Effect!;

        Assert.Equal(16, desktopRuleEffect.BlurRadius);
        Assert.Equal(28, headsetRuleEffect.BlurRadius);
    }

    /// <summary>The panel's outer edge glow (#285) draws behind the frame rather than on it, so the frame's own
    /// text does not render into the glow's offscreen layer.</summary>
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
    public void TheTabStripRuleStillDrawsInLightWithoutAGlow()
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
