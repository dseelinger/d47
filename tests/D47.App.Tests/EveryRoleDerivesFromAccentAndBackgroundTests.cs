using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Every role a theme publishes is opaque and comes from Accent and Background, on OKLab arithmetic
/// (#329) — the neutral ramp is asserted in <see cref="TheNeutralRampMixesAccentOntoBackgroundTests"/>;
/// this file covers the text ramp, the status ramp, and the roles that used to carry an alpha.
/// </summary>
public class EveryRoleDerivesFromAccentAndBackgroundTests
{
    private static ThemeManager Manager() => new(Application.Current!, NullLogger<ThemeManager>.Instance);

    private static Color Mix(Color from, Color to, double t)
    {
        var (r, g, b) = OklabMixing.Mix((from.R, from.G, from.B), (to.R, to.G, to.B), t);
        return Color.FromRgb(r, g, b);
    }

    private static Color Hue(Color colour, double degrees)
    {
        var (r, g, b) = OklabMixing.WithHue((colour.R, colour.G, colour.B), degrees);
        return Color.FromRgb(r, g, b);
    }

    /// <summary>Every non-gradient, non-effect role is fully opaque — a translucent fill inside a
    /// translucent border no longer stacks (.design-sync/NOTES.md) — except the scrim, which is a
    /// dimming overlay over the whole layer rather than a fill inside a card, and is translucent on
    /// purpose (#325).</summary>
    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Light)]
    public void NoRoleResolvesToAPartiallyTransparentSolidColourBrush(string themeId)
    {
        Manager().Apply(themeId);

        var resources = Application.Current!.Resources;

        foreach (var role in ThemeManager.Roles.Where(role => role != ThemeManager.ScrimKey))
        {
            if (resources[role] is SolidColorBrush brush)
            {
                Assert.True(1.0 == brush.Opacity, $"{role} resolved to opacity {brush.Opacity}");
            }
        }
    }

    [Fact]
    public void WarnAndGoodAreThemeRoles()
    {
        Assert.Contains(ThemeManager.WarnKey, ThemeManager.Roles);
        Assert.Contains(ThemeManager.GoodKey, ThemeManager.Roles);
    }

    [AvaloniaFact]
    public void DarkAndLightKeepTheirNeutralText()
    {
        Manager().Apply(ThemeCatalog.Dark);
        Assert.Equal(Palettes.Dark.Text, ((SolidColorBrush)Application.Current!.Resources[ThemeManager.TextKey]!).Color);

        Manager().Apply(ThemeCatalog.Light);
        Assert.Equal(Palettes.Light.Text, ((SolidColorBrush)Application.Current!.Resources[ThemeManager.TextKey]!).Color);
    }

    [AvaloniaFact]
    public void EliteSetsTextToAccent()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var resources = Application.Current!.Resources;

        Assert.Equal(
            ((SolidColorBrush)resources[ThemeManager.AccentKey]!).Color,
            ((SolidColorBrush)resources[ThemeManager.TextKey]!).Color);
    }

    [AvaloniaFact]
    public void DangerWarnGoodAndInfoKeepAccentsLightnessAndChromaAtTheirOwnHue()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var resources = Application.Current!.Resources;
        var accent = ((SolidColorBrush)resources[ThemeManager.AccentKey]!).Color;

        Assert.Equal(Hue(accent, 27), ((SolidColorBrush)resources[ThemeManager.DangerKey]!).Color);
        Assert.Equal(Hue(accent, 82), ((SolidColorBrush)resources[ThemeManager.WarnKey]!).Color);
        Assert.Equal(Hue(accent, 146), ((SolidColorBrush)resources[ThemeManager.GoodKey]!).Color);
        Assert.Equal(Hue(accent, 248), ((SolidColorBrush)resources[ThemeManager.InfoKey]!).Color);
    }

    [AvaloniaFact]
    public void TextMutedAndTextFaintAreAccentMixedOntoBackground()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var resources = Application.Current!.Resources;
        var background = ((SolidColorBrush)resources[ThemeManager.BackgroundKey]!).Color;
        var accent = ((SolidColorBrush)resources[ThemeManager.AccentKey]!).Color;

        Assert.Equal(Mix(background, accent, 0.66), ((SolidColorBrush)resources[ThemeManager.TextMutedKey]!).Color);
        Assert.Equal(Mix(background, accent, 0.42), ((SolidColorBrush)resources[ThemeManager.TextFaintKey]!).Color);
    }

    [AvaloniaFact]
    public void AccentInkIsAccentTowardWhiteOnDarkAndTowardNearBlackOnLight()
    {
        Manager().Apply(ThemeCatalog.Elite);
        var eliteAccent = ((SolidColorBrush)Application.Current!.Resources[ThemeManager.AccentKey]!).Color;
        var eliteInk = ((SolidColorBrush)Application.Current!.Resources[ThemeManager.AccentInkKey]!).Color;
        Assert.Equal(Mix(Colors.White, eliteAccent, 0.58), eliteInk);

        Manager().Apply(ThemeCatalog.Light);
        var lightAccent = ((SolidColorBrush)Application.Current!.Resources[ThemeManager.AccentKey]!).Color;
        var lightInk = ((SolidColorBrush)Application.Current!.Resources[ThemeManager.AccentInkKey]!).Color;
        Assert.Equal(Mix(Color.Parse("#140800"), lightAccent, 0.55), lightInk);
    }

    /// <summary>A green HUD matrix moves every status colour, every fill and Text, not only Accent
    /// (Accepted-when line of #329) — checked against the palette arithmetic directly, since the
    /// filesystem HUD-matrix read has no fixture in this suite.</summary>
    [Fact]
    public void RecolouringMovesEveryStatusColourEveryFillAndText()
    {
        var green = new GuiColourMatrix(0, 0, 0, 0, 1, 0, 0, 0, 0);

        var plain = DerivedPalette.From(Palettes.Elite);
        var recoloured = DerivedPalette.From(Palettes.Elite.RecolouredBy(green));

        Assert.NotEqual(plain.Danger, recoloured.Danger);
        Assert.NotEqual(plain.Warn, recoloured.Warn);
        Assert.NotEqual(plain.Good, recoloured.Good);
        Assert.NotEqual(plain.Info, recoloured.Info);
        Assert.NotEqual(plain.Text, recoloured.Text);
        Assert.NotEqual(plain.Rule, recoloured.Rule);
        Assert.NotEqual(plain.FillLow, recoloured.FillLow);
        Assert.NotEqual(plain.FillHigh, recoloured.FillHigh);
    }
}
