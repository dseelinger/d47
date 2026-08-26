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
    /// About is an <em>area</em> in the settings nav rather than a button in the footer
    /// (<a href="https://github.com/dseelinger/d47/issues/50">#50</a>).
    /// <para>
    /// The tests below used to drive <c>AboutWindow</c>, which is gone. The behaviour did not
    /// disappear — it moved into a capability, so the rows are asserted where they now live and
    /// the window that is left is the changelog.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void AboutIsACardInTheSettingsNav()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        var text = host.View.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible)
            .Select(block => block.Text ?? string.Empty)
            .ToList();

        Assert.Contains("About", text, StringComparer.Ordinal);

        host.Close();
    }

    /// <summary>
    /// And not in two places. The footer button is gone rather than kept beside the area, because
    /// two ways in that can drift is the thing this repository keeps writing rules about.
    /// </summary>
    [AvaloniaFact]
    public void TheFooterNoLongerOffersASecondWayIn()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        Assert.DoesNotContain(
            host.View.GetVisualDescendants().OfType<Button>(),
            button => button.Name == "AboutButton");

        host.Close();
    }

    /// <summary>
    /// The exact build is still a row, and still the reason the area exists: a build number you
    /// cannot copy is one that gets transcribed wrongly into a bug report.
    /// </summary>
    [Fact]
    public void TheAreaStatesTheExactBuild()
    {
        var (_, _, paths) = TestSurface.Create();

        var about = D47.Core.Capabilities.Builtin.AboutCapability.Create(
            paths,
            "1.2.3",
            "1.2.3+abcdef0");

        var build = about.Settings.Single(
            row => row.Key == D47.Core.Capabilities.Builtin.AboutCapability.BuildKey);

        Assert.Equal("1.2.3+abcdef0", build.Binding!.Read(new D47.Core.Configuration.D47Settings()));
    }

    /// <summary>
    /// Frontier's own wording, verbatim, because their media usage rules supply the sentence and
    /// ask that it be somewhere a person can find. It lives in Core now, so the bytes that ship
    /// and the bytes this reads are the same ones.
    /// </summary>
    [Fact]
    public void TheAttributionIsFrontiersOwnWording()
    {
        var attribution = D47.Core.Capabilities.Builtin.AboutCapability.Attribution;

        Assert.Contains("not endorsed by Frontier Developments plc", attribution, StringComparison.Ordinal);
        Assert.Contains("registered trademark", attribution, StringComparison.Ordinal);
    }

    /// <summary>
    /// The changelog ships inside the build, which is what lets the area answer with no internet
    /// at all — the one thing the browser button it replaces could never do.
    /// </summary>
    [Fact]
    public void TheChangelogIsInsideTheBuild()
    {
        Assert.True(D47.Core.Help.Changelog.Exists);
        Assert.Contains("## 0.75", D47.Core.Help.Changelog.Text, StringComparison.Ordinal);

        // Newest first, which is the order the file is already written in and the reason the
        // window shows the whole thing rather than hunting for a section.
        var newest = D47.Core.Help.Changelog.Text.IndexOf("## 0.75", StringComparison.Ordinal);
        var older = D47.Core.Help.Changelog.Text.IndexOf("## 0.1.0", StringComparison.Ordinal);

        Assert.True(newest >= 0 && (older < 0 || newest < older));
    }

    /// <summary>And the online one survives, because it is the only one that can show a newer release.</summary>
    [Fact]
    public void TheOnlineChangelogPointsAtTheBranchRatherThanATag()
    {
        Assert.Equal(
            "https://github.com/dseelinger/d47/blob/main/CHANGELOG.md",
            Controls.ChangelogWindow.OnlineUrl);
    }
}
