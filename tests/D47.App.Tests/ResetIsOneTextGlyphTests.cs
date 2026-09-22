using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using Xunit;

namespace D47.App.Tests;

/// <summary>Reset is the <c>↺</c> text glyph at every level of the settings page, in a 44 × 44 cell (#360).</summary>
public sealed class ResetIsOneTextGlyphTests
{
    private static IReadOnlyList<Button> ResetButtons(SettingsHost host) =>
    [
        .. host.View.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Content is TextBlock { Text: Glyphs.ResetText }),
    ];

    private static SettingsHost Opened()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return host;
    }

    [AvaloniaFact]
    public void EveryResetSaysWhatItResets()
    {
        var resets = ResetButtons(Opened());

        Assert.NotEmpty(resets);

        foreach (var button in resets)
        {
            Assert.StartsWith("Reset ", AutomationProperties.GetName(button));
        }
    }

    [AvaloniaFact]
    public void EveryResetIsAtLeastAMinimumTarget()
    {
        foreach (var button in ResetButtons(Opened()))
        {
            Assert.True(button.Width >= TypeScale.MinimumTarget, $"{AutomationProperties.GetName(button)} is {button.Width} wide");
            Assert.True(button.Height >= TypeScale.MinimumTarget, $"{AutomationProperties.GetName(button)} is {button.Height} tall");
        }
    }
}
