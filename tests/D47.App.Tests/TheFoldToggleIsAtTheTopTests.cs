using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// "Show every setting" is drawn above every card, not inside one (#60, the Commander's call on
/// 2026-08-26).
/// </summary>
public class TheFoldToggleIsAtTheTopTests
{
    private static SettingsHost Folded()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        // SettingsHost shows the whole page for every test that is about a row.
        settings.Apply(InterfaceCapability.ShowEverySettingKey, "false", SettingsCaller.Panel);
        host.View.Refresh();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return host;
    }

    /// <summary>Every label actually on screen, in the order it is drawn.</summary>
    private static List<string> Drawn(Visual root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(block.Text))
            .Select(block => block.Text!)];

    /// <summary>The cards themselves.</summary>
    private static Visual Cards(SettingsHost host) =>
        host.View.GetVisualDescendants().OfType<StackPanel>().First(panel => panel.Name == "Cards");

    /// <summary>On screen with the page folded — which is the state it exists to get a Commander out of.</summary>
    [AvaloniaFact]
    public void TheToggleIsDrawnWhileThePageIsFolded()
    {
        var host = Folded();

        Assert.Contains("Show every setting", Drawn(host.View), StringComparer.Ordinal);

        host.Close();
    }

    /// <summary>
    /// Above every card heading, which is what "the top of the page" means when the page is a column of
    /// cards.
    /// </summary>
    [AvaloniaFact]
    public void TheToggleComesBeforeEveryCard()
    {
        var host = Folded();

        var drawn = Drawn(Cards(host));

        var toggle = drawn.FindIndex(label => label == "Show every setting");
        var firstCard = drawn.FindIndex(label => label == "Language model");

        Assert.True(toggle >= 0, "The toggle is not on the page at all.");
        Assert.True(firstCard >= 0, "The first card is not on the page at all.");
        Assert.True(
            toggle < firstCard,
            "The toggle is drawn after a card heading, so it is inside the page rather than above it.");

        host.Close();
    }

    /// <summary>And not drawn twice — lifted out of its card rather than copied above it.</summary>
    [AvaloniaFact]
    public void TheToggleIsDrawnExactlyOnce()
    {
        var host = Folded();

        Assert.Equal(1, Drawn(Cards(host)).Count(label => label == "Show every setting"));

        host.Close();
    }

    /// <summary>The page still folds.</summary>
    [AvaloniaFact]
    public void TheRestOfThePageIsStillFolded()
    {
        var host = Folded();

        var drawn = Drawn(Cards(host));

        Assert.Contains("Provider", drawn, StringComparer.Ordinal);
        Assert.DoesNotContain("Endpoint", drawn, StringComparer.Ordinal);

        host.Close();
    }
}
