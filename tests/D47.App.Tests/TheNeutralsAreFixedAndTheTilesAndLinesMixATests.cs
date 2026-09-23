using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The palette is Elite's token table: the neutrals are the table's values in every theme, the derived
/// tokens are OKLab mixes of the theme's a onto its bg, and the HUD matrix moves only the coloured tokens.
/// </summary>
public class TheNeutralsAreFixedAndTheTilesAndLinesMixATests
{
    private static readonly string[] Neutrals =
    [
        ThemeManager.BgKey, ThemeManager.BarKey, ThemeManager.SlabKey,
        ThemeManager.WhiteKey, ThemeManager.GreyKey, ThemeManager.Grey2Key,
    ];

    /// <summary>A diagonal matrix taking Elite's a, #FF7A1A, to #1A7AFF.</summary>
    private static readonly GuiColourMatrix Blue = new(0x1A / 255.0, 0, 0, 0, 1, 0, 0, 0, 255.0 / 0x1A);

    private static ThemeManager Manager() => new(Application.Current!, NullLogger<ThemeManager>.Instance);

    private static Color Published(string key) => ((SolidColorBrush)Application.Current!.Resources[key]!).Color;

    private static Color Mix(Color from, Color to, double t)
    {
        var (r, g, b) = OklabMixing.Mix((from.R, from.G, from.B), (to.R, to.G, to.B), t);
        return Color.FromRgb(r, g, b);
    }

    [AvaloniaTheory]
    [InlineData(ThemeManager.BgKey, "#070606")]
    [InlineData(ThemeManager.BarKey, "#0F0D0C")]
    [InlineData(ThemeManager.SlabKey, "#232120")]
    [InlineData(ThemeManager.WhiteKey, "#EDE9E3")]
    [InlineData(ThemeManager.GreyKey, "#A09B94")]
    [InlineData(ThemeManager.Grey2Key, "#6E6A65")]
    [InlineData(ThemeManager.AKey, "#FF7A1A")]
    [InlineData(ThemeManager.KnockKey, "#140800")]
    [InlineData(ThemeManager.BrownKey, "#6B2F00")]
    [InlineData(ThemeManager.CyanKey, "#33D6E8")]
    [InlineData(ThemeManager.BlueKey, "#1FA8F5")]
    [InlineData(ThemeManager.RedKey, "#F0343F")]
    [InlineData(ThemeManager.YellowKey, "#F5D426")]
    public void EliteIsTheTablesColumn(string key, string hex)
    {
        Manager().Apply(ThemeCatalog.Elite);

        Assert.Equal(Color.Parse(hex), Published(key));
    }

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Dark, "#1E1E1E", "#2D2D2D", "#252526", "#D4D4D4", "#9D9D9D", "#3794FF", "#0B1F33")]
    [InlineData(ThemeCatalog.Light, "#F4F1EB", "#E6E1D8", "#E4DFD6", "#1C1917", "#5E5852", "#B84E00", "#FFFFFF")]
    public void DarkAndLightAreTheirColumnsWithGrey2AndBrownMixed(
        string themeId, string bg, string bar, string slab, string white, string grey, string a, string knock)
    {
        Manager().Apply(themeId);

        Assert.Equal(Color.Parse(bg), Published(ThemeManager.BgKey));
        Assert.Equal(Color.Parse(bar), Published(ThemeManager.BarKey));
        Assert.Equal(Color.Parse(slab), Published(ThemeManager.SlabKey));
        Assert.Equal(Color.Parse(white), Published(ThemeManager.WhiteKey));
        Assert.Equal(Color.Parse(grey), Published(ThemeManager.GreyKey));
        Assert.Equal(Color.Parse(a), Published(ThemeManager.AKey));
        Assert.Equal(Color.Parse(knock), Published(ThemeManager.KnockKey));
        Assert.Equal(Mix(Color.Parse(grey), Color.Parse(bg), 0.45), Published(ThemeManager.Grey2Key));
        Assert.Equal(Mix(Color.Parse(a), Color.Parse(knock), 0.60), Published(ThemeManager.BrownKey));
    }

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Light)]
    public void TheTilesAndLinesAreAMixedOntoBg(string themeId)
    {
        Manager().Apply(themeId);

        var bg = Published(ThemeManager.BgKey);
        var a = Published(ThemeManager.AKey);

        Assert.Equal(Mix(bg, a, 0.20), Published(ThemeManager.TileKey));
        Assert.Equal(Mix(bg, a, 0.30), Published(ThemeManager.Tile2Key));
        Assert.Equal(Mix(bg, a, 0.55), Published(ThemeManager.LineKey));
        Assert.Equal(Mix(bg, a, 0.28), Published(ThemeManager.Line2Key));
    }

    [AvaloniaFact]
    public void TheHudMatrixMovesAAndLeavesTheNeutrals()
    {
        Manager().Apply(ThemeCatalog.Elite);
        var elite = Neutrals.ToDictionary(key => key, Published);
        var eliteA = Published(ThemeManager.AKey);

        Manager().Apply(ThemeCatalog.ElitePaletteId, Blue);

        Assert.All(Neutrals, key => Assert.Equal(elite[key], Published(key)));
        Assert.NotEqual(eliteA, Published(ThemeManager.AKey));
        Assert.Equal(
            Mix(Published(ThemeManager.BgKey), Published(ThemeManager.AKey), 0.20),
            Published(ThemeManager.TileKey));
    }

    /// <summary>Colours with next to no chroma have no hue worth the name, so the near-greys are skipped.</summary>
    [AvaloniaFact]
    public void ABlueHudMatrixLeavesNothingOrange()
    {
        Manager().Apply(ThemeCatalog.ElitePaletteId, Blue);

        foreach (var key in ThemeManager.Roles)
        {
            if (Application.Current!.Resources[key] is not SolidColorBrush { Color: var c })
            {
                continue;
            }

            var lch = OklabColour.FromSrgb(c.R, c.G, c.B).ToOklch();

            Assert.False(lch.C > 0.02 && lch.H is >= 30 and <= 80, $"{key} is {c} at hue {lch.H:F0}°");
        }
    }

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Light)]
    public void EveryLegacyKeyIsTheTokenItNames(string themeId)
    {
        Manager().Apply(themeId);

        Assert.Equal(Published(ThemeManager.BgKey), Published(ThemeManager.BgKey));
        Assert.Equal(Published(ThemeManager.WhiteKey), Published(ThemeManager.WhiteKey));
        Assert.Equal(Published(ThemeManager.GreyKey), Published(ThemeManager.GreyKey));
        Assert.Equal(Published(ThemeManager.AKey), Published(ThemeManager.AKey));
        Assert.Equal(Published(ThemeManager.RedKey), Published(ThemeManager.RedKey));
        Assert.Equal(Published(ThemeManager.BlueKey), Published(ThemeManager.BlueKey));
        Assert.Equal(Published(ThemeManager.LineKey), Published(ThemeManager.LineKey));
        Assert.Equal(Published(ThemeManager.Line2Key), Published(ThemeManager.Line2Key));
        Assert.Equal(Published(ThemeManager.TileKey), Published(ThemeManager.TileKey));
        Assert.Equal(Published(ThemeManager.Tile2Key), Published(ThemeManager.Tile2Key));
        Assert.Equal(Published(ThemeManager.SlabKey), Published(ThemeManager.SlabKey));
    }

    /// <summary>The scrim is a dimming overlay and translucent on purpose; every other colour is opaque.</summary>
    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Light)]
    public void NoRoleButTheScrimIsTranslucent(string themeId)
    {
        Manager().Apply(themeId);

        foreach (var role in ThemeManager.Roles.Where(role => role != ThemeManager.ScrimKey))
        {
            if (Application.Current!.Resources[role] is SolidColorBrush brush)
            {
                Assert.True(brush.Opacity == 1.0 && brush.Color.A == 255, $"{role} is translucent");
            }
        }

        Assert.Equal(0.72, ((SolidColorBrush)Application.Current!.Resources[ThemeManager.ScrimKey]!).Opacity);
    }
}
