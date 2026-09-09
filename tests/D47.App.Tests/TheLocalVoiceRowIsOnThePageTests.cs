using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class TheLocalVoiceRowIsOnThePageTests
{
    private static SettingsHost Open()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);
        Dispatcher.UIThread.RunJobs();

        return host;
    }

    /// <summary>
    /// The row's container, found by walking up from its caption rather than by assuming a shape.
    /// </summary>
    private static Control? Row(SettingsHost host, string label)
    {
        var caption = host.View.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(text => text.Text == label);

        Control? at = caption;

        // Up until the container also holds the row's own controls.
        while (at is not null && at.GetVisualDescendants().OfType<Button>()
                   .Any(button => !D47.App.Settings.SettingsView.IsRowChrome(button)) is false)
        {
            at = at.GetVisualParent() as Control;
        }

        return at ?? caption;
    }

    /// <summary>
    /// It is on the page at all, which is the half the absent-row rule is about: a row that does not
    /// exist cannot be wrong, and nothing in the suite notices.
    /// </summary>
    [AvaloniaFact]
    public void TheRowIsDrawn()
    {
        var host = Open();

        var everything = host.View.GetVisualDescendants().OfType<TextBlock>()
            .Select(text => text.Text ?? string.Empty).ToList();

        var speech = everything.Where(t => t.Contains("voice", StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.True(
            everything.Any(t => t == "Local voice"),
            "no 'Local voice' text anywhere. Voice-ish text on the page: " + string.Join(" | ", speech));

        host.Window.Close();
    }

    [AvaloniaFact]
    public void ItSaysWhetherTheModelIsHere()
    {
        var host = Open();
        var row = Row(host, "Local voice")!;

        var said = string.Join(
            " | ",
            row!.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text ?? string.Empty));

        Assert.True(said.Contains("Not downloaded", StringComparison.Ordinal), said);

        host.Window.Close();
    }

    /// <summary>And the button is there.</summary>
    [AvaloniaFact]
    public void ThereIsAWayToDownloadIt()
    {
        var host = Open();
        var row = Row(host, "Local voice")!;

        var buttons = row!.GetVisualDescendants().OfType<Button>()
            .Select(button => button.Content as string ?? string.Empty)
            .ToList();

        Assert.Contains("Download it", buttons);

        host.Window.Close();
    }
}
