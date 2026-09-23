using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Every glyph-only button takes a d47 theme rather than Fluent's own: the amount control's spinner
/// and the three reset icons take D47.GlyphButton (#376), the stepper's arrows D47.StepperArrow (#394).
///
/// HeadlessApp does not merge ControlKitTheme.axaml the way App.axaml does (that gap predates
/// this issue and is outside it), so each test merges it onto Application.Current for its own
/// duration only, and takes it back off on Dispose — the same resource the button's own
/// production code resolves from, not a copy built for the test.
/// </summary>
public class GlyphButtonsTakeTheirOwnThemeTests
{
    [AvaloniaFact]
    public void TheStepperArrowsTakeTheStepperArrowTheme()
    {
        using var _ = ControlKitTheme();
        var host = Open();
        var theme = (ControlTheme)Application.Current!.FindResource("D47.StepperArrow")!;

        // Laid out, not merely built — a card the view keeps in memory but has not expanded never
        // gets its content templated, so its RepeatButtons are not visual descendants yet.
        var stepper = host.View.GetVisualDescendants().OfType<Stepper>().First(s => s.Bounds.Width > 0);
        var arrows = stepper.GetVisualDescendants().OfType<RepeatButton>().ToList();

        Assert.NotEmpty(arrows);
        Assert.All(arrows, arrow => Assert.Same(theme, arrow.Theme));

        host.Close();
    }

    [AvaloniaFact]
    public void TheAmountControlSpinnerButtonsTakeTheGlyphButtonTheme()
    {
        using var _ = ControlKitTheme();
        var host = Open();
        var theme = GlyphButtonTheme();

        var number = host.View.GetVisualDescendants().OfType<NumericUpDown>().First(n => n.Bounds.Width > 0);
        var spinnerButtons = number.GetVisualDescendants().OfType<RepeatButton>()
            .Where(button => button.Name is "PART_IncreaseButton" or "PART_DecreaseButton")
            .ToList();

        Assert.Equal(2, spinnerButtons.Count);
        Assert.All(spinnerButtons, button => Assert.Same(theme, button.Theme));

        host.Close();
    }

    [AvaloniaFact]
    public void EveryRowResetTakesTheGlyphButtonTheme()
    {
        using var _ = ControlKitTheme();
        var host = Open();
        var theme = GlyphButtonTheme();

        var resets = host.View.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Name == SettingsView.RowResetName)
            .ToList();

        Assert.NotEmpty(resets);
        Assert.All(resets, reset => Assert.Same(theme, reset.Theme));

        host.Close();
    }

    [AvaloniaFact]
    public void EveryCardAndGroupResetTakesTheGlyphButtonTheme()
    {
        using var _ = ControlKitTheme();
        var host = Open();
        var theme = GlyphButtonTheme();

        var headingResets = host.View.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Name != SettingsView.RowResetName
                && Avalonia.Automation.AutomationProperties.GetName(button) is { } name
                && name.StartsWith("Reset ", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(headingResets);
        Assert.All(headingResets, reset => Assert.Same(theme, reset.Theme));

        host.Close();
    }

    private static ControlTheme GlyphButtonTheme() =>
        (ControlTheme)Application.Current!.FindResource("D47.GlyphButton")!;

    /// <summary>Merges ControlKitTheme.axaml onto Application.Current, removed again on Dispose.</summary>
    private static IDisposable ControlKitTheme()
    {
        var include = new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        };

        Application.Current!.Styles.Add(include);

        return new Removal(include);
    }

    private sealed class Removal(IStyle style) : IDisposable
    {
        public void Dispose() => Application.Current!.Styles.Remove(style);
    }

    private static SettingsHost Open()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        return SettingsHost.Open(settings, viewState, paths);
    }
}
