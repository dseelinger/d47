using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The local voice row, through the drawn page (#101).
/// <para>
/// <b>Written after shipping it broken.</b> v0.84.0 went out with the row's download button
/// missing, and every test passed: the descriptor-level tests did not model a host that arrives
/// after the rows do, and the App-level ones bound a surface that supplied neither of the row's
/// host delegates — so the row they drew was not the row that ships.
/// </para>
/// <para>
/// A probe of the capability is not the page. This opens the real settings surface, in the panel,
/// in a window, and looks at what a Commander would see.
/// </para>
/// </summary>
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
    /// <para>
    /// The first version of this looked for a three-column grid, which is what an ordinary row is
    /// and what a pressable one is not — so it reported the row missing when the row was on the
    /// page. A locator that encodes a layout is a test that fails when the layout is fine.
    /// </para>
    /// </summary>
    private static Control? Row(SettingsHost host, string label)
    {
        var caption = host.View.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(text => text.Text == label);

        Control? at = caption;

        // Up until the container also holds the row's own controls.
        while (at is not null && at.GetVisualDescendants().OfType<Button>().Any() is false)
        {
            at = at.GetVisualParent() as Control;
        }

        return at ?? caption;
    }

    /// <summary>
    /// It is on the page at all, which is the half the absent-row rule is about: a row that does
    /// not exist cannot be wrong, and nothing in the suite notices.
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

    /// <summary>
    /// It says whether the model is here. This is what a Commander reads before deciding to spend
    /// a 350 MB download, so it has to be on the row rather than implied by the button beside it.
    /// </summary>
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

    /// <summary>
    /// <b>And the button is there.</b> This is the one that failed in the shipped build: the row
    /// asked its host delegate while the descriptor was being built, the host did not exist yet,
    /// and the button was dropped for the life of the process.
    /// </summary>
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
