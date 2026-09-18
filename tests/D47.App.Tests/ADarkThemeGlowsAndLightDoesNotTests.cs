using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Bloom and scanlines (#281): present in every dark theme, absent in Light.</summary>
public class ADarkThemeGlowsAndLightDoesNotTests
{
    private static ThemeManager Manager() =>
        new(Application.Current!, NullLogger<ThemeManager>.Instance);

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Guardian)]
    [InlineData(ThemeCatalog.ElitePaletteId)]
    public void EveryDarkThemeCarriesBothEffects(string themeId)
    {
        Manager().Apply(themeId);

        var resources = Application.Current!.Resources;

        Assert.IsType<DropShadowEffect>(resources[ThemeManager.BloomKey]);
        Assert.IsType<ImageBrush>(resources[ThemeManager.ScanlinesKey]);
    }

    [AvaloniaFact]
    public void LightCarriesNeither()
    {
        Manager().Apply(ThemeCatalog.Light);

        var resources = Application.Current!.Resources;

        Assert.Null(resources[ThemeManager.BloomKey]);
        Assert.Null(resources[ThemeManager.ScanlinesKey]);
    }

    /// <summary>A theme switch recomputes both, rather than leaving Light with Elite's values still set.</summary>
    [AvaloniaFact]
    public void SwitchingBackToLightTurnsBothOff()
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
}
