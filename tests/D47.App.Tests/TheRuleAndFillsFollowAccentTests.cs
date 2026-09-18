using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The rule and the two fills are derived from Accent rather than stored on Palette (#273), so this
/// asks ThemeManager rather than Palette — the derivation and the recolour both happen in Apply.
/// </summary>
public class TheRuleAndFillsFollowAccentTests
{
    [AvaloniaFact]
    public void TheRuleIsAccentAt42PercentAndTheFillsAt10And18()
    {
        var manager = new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance);

        manager.Apply(ThemeCatalog.Elite);

        var accent = ((SolidColorBrush)Application.Current!.Resources[ThemeManager.AccentKey]!).Color;
        var rule = (SolidColorBrush)Application.Current!.Resources[ThemeManager.RuleKey]!;
        var fillLow = (SolidColorBrush)Application.Current!.Resources[ThemeManager.FillLowKey]!;
        var fillHigh = (SolidColorBrush)Application.Current!.Resources[ThemeManager.FillHighKey]!;

        Assert.Equal(accent, rule.Color);
        Assert.Equal(0.42, rule.Opacity, 3);

        Assert.Equal(accent, fillLow.Color);
        Assert.Equal(0.10, fillLow.Opacity, 3);

        Assert.Equal(accent, fillHigh.Color);
        Assert.Equal(0.18, fillHigh.Opacity, 3);
    }

    [AvaloniaFact]
    public void TheRuleChangesColourWithTheTheme()
    {
        var manager = new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance);

        manager.Apply(ThemeCatalog.Elite);
        var eliteRule = ((SolidColorBrush)Application.Current!.Resources[ThemeManager.RuleKey]!).Color;

        manager.Apply(ThemeCatalog.Guardian);
        var guardianAccent = ((SolidColorBrush)Application.Current!.Resources[ThemeManager.AccentKey]!).Color;
        var guardianRule = ((SolidColorBrush)Application.Current!.Resources[ThemeManager.RuleKey]!).Color;

        Assert.NotEqual(eliteRule, guardianRule);
        Assert.Equal(guardianAccent, guardianRule);
    }
}
