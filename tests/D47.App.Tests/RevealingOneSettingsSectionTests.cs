using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.Core.Capabilities.Builtin;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Showing one section of Settings on request, which is where a help card lands.
/// </summary>
public class RevealingOneSettingsSectionTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    /// <summary>The section this is about, by its place's title.</summary>
    private const string Heading = "Voice Input";

    /// <summary>The card whose heading says this, as the Commander sees it.</summary>
    private static Border Card(SettingsView view) =>
        ((StackPanel)view.FindControl<Control>("Cards")!).Children
            .OfType<Border>()
            .First(card => card.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => string.Equals(text.Text, Heading, StringComparison.OrdinalIgnoreCase)));

    /// <summary>The rows under the heading, which are what collapsing hides.</summary>
    private static StackPanel Body(Border card) =>
        card.GetVisualDescendants().OfType<StackPanel>()
            .First(stack => stack.Margin == new Thickness(0, 8, 0, 0));

    /// <summary>The chevron beside the heading, which has to agree with the rows.</summary>
    private static TextBlock Chevron(Border card) =>
        card.GetVisualDescendants().OfType<TextBlock>()
            .First(text => text.Text is "▾" or "▸");

    /// <summary>Expanded before scrolled.</summary>
    [AvaloniaFact]
    public void RevealingASectionOpensItAsWellAsScrollingToIt()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        // How the Commander left it last time.
        viewState.Save(viewState.Load().With("voice-input", expanded: false));

        var host = SettingsHost.Open(settings, viewState, paths);
        var card = Card(host.View);

        Assert.False(Body(card).IsVisible, "the section starts this test collapsed");
        Assert.Equal("▸", Chevron(card).Text);

        host.View.Reveal(ListeningCapability.Id);
        Jobs();

        Assert.True(Body(card).IsVisible, "the reveal opened it");

        // And the chevron came with it.
        Assert.Equal("▾", Chevron(card).Text);

        host.Close();
    }

    /// <summary>A capability is revealed at the place holding its first row on the page.</summary>
    [AvaloniaFact]
    public void ACapabilityIsRevealedWhereItsFirstRowIs()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        host.View.Reveal(MemoryCapability.Id);
        Jobs();

        Assert.Equal("memory", host.View.SectionIds[host.View.ActiveSection]);

        host.View.Reveal(ListeningCapability.Id);
        Jobs();

        Assert.Equal("voice-input", host.View.SectionIds[host.View.ActiveSection]);

        host.Close();
    }

    /// <summary>A capability whose rows are all on tabs changes nothing.</summary>
    [AvaloniaFact]
    public void ACapabilityWithNoRowOnThePageChangesNothing()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        host.View.Reveal(MemoryCapability.Id);
        Jobs();

        var active = host.View.ActiveSection;
        var open = Enumerable.Range(0, host.View.SectionIds.Count).Select(host.View.IsSectionExpanded).ToList();

        host.View.Reveal(ShipsCapability.Id);
        Jobs();

        Assert.Equal(active, host.View.ActiveSection);
        Assert.Equal(open, Enumerable.Range(0, host.View.SectionIds.Count).Select(host.View.IsSectionExpanded));

        host.Close();
    }

    /// <summary>An id no section owns does nothing rather than throwing.</summary>
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
