using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// "Show every setting" is drawn above every card, not inside one
/// (<a href="https://github.com/dseelinger/d47/issues/60">#60</a>, the Commander's call on
/// 2026-08-26).
/// <para>
/// It shipped four rows into the Interface card, which is exactly where a Commander who cannot see
/// the rest of the page will not look for the reason. A control that governs the page belongs at
/// the top of the page.
/// </para>
/// <para>
/// <b>Everything here reads <c>IsEffectivelyVisible</c> rather than <c>IsVisible</c>.</b> A child's
/// own <c>IsVisible</c> stays true when an ancestor is hidden, so a folded row's label answers yes
/// to the wrong question — which is how the first draft of this test "proved" the fold had stopped
/// working.
/// </para>
/// </summary>
public class TheFoldToggleIsAtTheTopTests
{
    private static SettingsHost Folded()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        // SettingsHost shows the whole page for every test that is about a row. This one is about
        // the fold, so it puts it back.
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

    /// <summary>
    /// The cards themselves. The nav column lists every card title before the page starts, so an
    /// ordering question asked of the whole view is answered by the nav rather than by the page.
    /// </summary>
    private static Visual Cards(SettingsHost host) =>
        host.View.GetVisualDescendants().OfType<StackPanel>().First(panel => panel.Name == "Cards");

    /// <summary>
    /// On screen with the page folded — which is the state it exists to get a Commander out of. A
    /// control that hides the page and can hide itself has no way back.
    /// </summary>
    [AvaloniaFact]
    public void TheToggleIsDrawnWhileThePageIsFolded()
    {
        var host = Folded();

        Assert.Contains("Show every setting", Drawn(host.View), StringComparer.Ordinal);

        host.Close();
    }

    /// <summary>
    /// Above every card heading, which is what "the top of the page" means when the page is a
    /// column of cards.
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

    /// <summary>
    /// The page still folds. A test that only proved the toggle was on screen would pass just as
    /// well if the fold had stopped working.
    /// </summary>
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
