using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.Core.Capabilities.Builtin;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Showing one section of Settings on request, which is where a help card lands (asked for
/// 2026-08-23).
/// <para>
/// The sections are a scroll-spy rather than a tab strip, so "go to Listening" is a scroll and
/// not a selection — and a card the Commander left collapsed is a card that scrolls to a heading
/// with nothing under it. That reads exactly like a button that did not work, which is the
/// failure these tests are here to prevent: the Commander pressed it precisely because they did
/// not know where those rows were.
/// </para>
/// </summary>
public class RevealingOneSettingsSectionTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    /// <summary>The section this is about, by the title its capability declares for the panel.</summary>
    private const string Heading = "Listening";

    /// <summary>
    /// The card whose heading says this, as the Commander sees it. Found through the tree rather
    /// than through the private section list, so this asserts what is drawn.
    /// </summary>
    private static Border Card(SettingsView view) =>
        ((StackPanel)view.FindControl<Control>("Cards")!).Children
            .OfType<Border>()
            .First(card => card.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == Heading));

    /// <summary>The rows under the heading, which are what collapsing hides.</summary>
    private static StackPanel Body(Border card) =>
        card.GetVisualDescendants().OfType<StackPanel>()
            .First(stack => stack.Margin == new Thickness(18, 4, 18, 18));

    /// <summary>The chevron beside the heading, which has to agree with the rows.</summary>
    private static TextBlock Chevron(Border card) =>
        card.GetVisualDescendants().OfType<TextBlock>()
            .First(text => text.Text is "▾" or "▸");

    /// <summary>
    /// <b>Expanded before scrolled.</b> The section is left collapsed on disk before the page is
    /// built, so this asserts the reveal opening it rather than it having happened to be open.
    /// </summary>
    [AvaloniaFact]
    public void RevealingASectionOpensItAsWellAsScrollingToIt()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        // How the Commander left it last time.
        viewState.Save(viewState.Load().With(ListeningCapability.Id, expanded: false));

        var host = SettingsHost.Open(settings, viewState, paths);
        var card = Card(host.View);

        Assert.False(Body(card).IsVisible, "the section starts this test collapsed");
        Assert.Equal("▸", Chevron(card).Text);

        host.View.Reveal(ListeningCapability.Id);
        Jobs();

        Assert.True(Body(card).IsVisible, "the reveal opened it");

        // And the chevron came with it. Both routes go through one method for this reason: a
        // second copy of the toggle is how an open card ends up wearing a closed chevron.
        Assert.Equal("▾", Chevron(card).Text);

        host.Close();
    }

    /// <summary>
    /// An id no section owns does nothing rather than throwing. The ids come from shipped markup
    /// rather than from the registry, so a page naming a capability this build no longer registers
    /// is a stale link — and a stale link is worth a dead button rather than a crash on the
    /// settings page.
    /// </summary>
    [AvaloniaFact]
    public void AnIdNoSectionOwnsIsIgnored()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        host.View.Reveal("telepathy");
        Jobs();

        // Still standing, and still showing the page it was on.
        Assert.NotNull(Card(host.View));

        host.Close();
    }
}
