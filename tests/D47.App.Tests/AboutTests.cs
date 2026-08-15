using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Where the build number lives. It used to be the panel's first line, alongside a tagline —
/// permanent chrome for something read once. The short version moved to the title bar, which is
/// on screen anyway, and the exact build to a dialog, which is where forty characters of commit
/// hash can be selected and pasted into a report.
/// </summary>
public class AboutTests
{
    /// <summary>The title bar answers "what am I running" without anything being opened.</summary>
    [AvaloniaFact]
    public void TheTitleBarCarriesTheVersion()
    {
        var window = new MainWindow(host: null);

        Assert.StartsWith("Directive 47", window.Title, StringComparison.Ordinal);
        Assert.Contains(BuildInfo.Semantic, window.Title, StringComparison.Ordinal);

        // The short form only. A commit hash in the one piece of chrome that is never off
        // screen is noise the Commander cannot dismiss.
        Assert.DoesNotContain("+", window.Title, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the panel no longer spends its best line on either the build or a tagline.
    /// </summary>
    [AvaloniaFact]
    public void ThePanelHeaderCarriesNeitherTheBuildNorATagline()
    {
        var window = new MainWindow(host: null);
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var text = string.Join(
            " ",
            window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));

        Assert.DoesNotContain("Optimize Inferior Systems", text, StringComparison.Ordinal);
        Assert.DoesNotContain("build ", text, StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    /// The dialog carries the exact build, selectable — a build number you cannot copy is a
    /// build number you transcribe wrongly into a bug report.
    /// </summary>
    [AvaloniaFact]
    public void TheAboutDialogCarriesTheExactBuildAndCanBeCopied()
    {
        var (_, _, paths) = TestSurface.Create();

        var about = new AboutWindow(paths);
        about.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var selectable = about.GetVisualDescendants().OfType<SelectableTextBlock>().ToList();
        var shown = selectable.Select(block => block.Text).ToList();

        Assert.Contains(BuildInfo.Full, shown);
        Assert.Contains(paths.Data, shown);

        Assert.NotNull(about.CaptureRenderedFrame());

        about.Close();
    }

    /// <summary>
    /// Frontier's media usage rules permit a non-commercial project to use their game content
    /// provided it is attributed somewhere a person can find, and d47 ships their ship figures,
    /// blueprints, material names and engineer locations. The README and the documentation site
    /// carry the same words, but a Commander who installs the binary and never visits either
    /// still has to be able to see it, so it has to be in the app.
    /// </summary>
    [AvaloniaFact]
    public void TheAboutDialogAttributesFrontier()
    {
        var (_, _, paths) = TestSurface.Create();

        var about = new AboutWindow(paths);
        about.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var shown = about.GetVisualDescendants().OfType<SelectableTextBlock>()
            .Select(block => block.Text)
            .ToList();

        Assert.Contains(AboutWindow.Attribution, shown);

        // Their wording, not a paraphrase of it. The rules supply the sentence.
        Assert.Contains(
            "with the permission of Frontier Developments plc",
            AboutWindow.Attribution,
            StringComparison.Ordinal);

        Assert.Contains("non-commercial", AboutWindow.Attribution, StringComparison.Ordinal);

        about.Close();
    }

    /// <summary>It is reachable, which a dialog nobody can open is not.</summary>
    [AvaloniaFact]
    public void TheSettingsFooterOffersAWayIn()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        Assert.Contains(
            host.View.GetVisualDescendants().OfType<Button>(),
            button => button.Name == "AboutButton");

        host.Close();
    }
}
