using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The rule, the border and the fills are Accent mixed onto Background in OKLab, opaque, rather than
/// Accent drawn at partial alpha (#273, retuned #329) — so this asks ThemeManager rather than Palette,
/// since the derivation and the recolour both happen in Apply.
/// </summary>
public class TheNeutralRampMixesAccentOntoBackgroundTests
{
    private static Color Mix(Color background, Color accent, double t)
    {
        var (r, g, b) = OklabMixing.Mix((background.R, background.G, background.B), (accent.R, accent.G, accent.B), t);
        return Color.FromRgb(r, g, b);
    }

    [AvaloniaFact]
    public void TheRuleAndBorderAndFillsAreAccentMixedOntoBackground()
    {
        var manager = new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance);

        manager.Apply(ThemeCatalog.Elite);

        var background = ((SolidColorBrush)Application.Current!.Resources[ThemeManager.BackgroundKey]!).Color;
        var accent = ((SolidColorBrush)Application.Current!.Resources[ThemeManager.AccentKey]!).Color;

        var rule = (SolidColorBrush)Application.Current!.Resources[ThemeManager.RuleKey]!;
        var border = (SolidColorBrush)Application.Current!.Resources[ThemeManager.BorderKey]!;
        var fillLow = (SolidColorBrush)Application.Current!.Resources[ThemeManager.FillLowKey]!;
        var fillHigh = (SolidColorBrush)Application.Current!.Resources[ThemeManager.FillHighKey]!;
        var fillHigher = (SolidColorBrush)Application.Current!.Resources[ThemeManager.FillHigherKey]!;

        Assert.Equal(Mix(background, accent, 0.50), rule.Color);
        Assert.Equal(1.0, rule.Opacity);

        Assert.Equal(Mix(background, accent, 0.24), border.Color);
        Assert.Equal(1.0, border.Opacity);

        Assert.Equal(Mix(background, accent, 0.09), fillLow.Color);
        Assert.Equal(1.0, fillLow.Opacity);

        Assert.Equal(Mix(background, accent, 0.16), fillHigh.Color);
        Assert.Equal(1.0, fillHigh.Opacity);

        Assert.Equal(Mix(background, accent, 0.27), fillHigher.Color);
        Assert.Equal(1.0, fillHigher.Opacity);
    }

    /// <summary>Surface and SurfaceAlt are the same mixes as FillLow and FillHigh, not a fixed grey.</summary>
    [AvaloniaFact]
    public void SurfaceAndSurfaceAltAreTheFillLowAndFillHighMixes()
    {
        var manager = new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance);

        manager.Apply(ThemeCatalog.Elite);

        var resources = Application.Current!.Resources;

        Assert.Equal(
            ((SolidColorBrush)resources[ThemeManager.FillLowKey]!).Color,
            ((SolidColorBrush)resources[ThemeManager.SurfaceKey]!).Color);

        Assert.Equal(
            ((SolidColorBrush)resources[ThemeManager.FillHighKey]!).Color,
            ((SolidColorBrush)resources[ThemeManager.SurfaceAltKey]!).Color);
    }

    [AvaloniaFact]
    public void TheRuleChangesColourWithTheTheme()
    {
        var manager = new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance);

        manager.Apply(ThemeCatalog.Elite);
        var eliteRule = ((SolidColorBrush)Application.Current!.Resources[ThemeManager.RuleKey]!).Color;

        manager.Apply(ThemeCatalog.Guardian);
        var guardianRule = ((SolidColorBrush)Application.Current!.Resources[ThemeManager.RuleKey]!).Color;

        Assert.NotEqual(eliteRule, guardianRule);
    }
}
