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

    /// <summary>The page head's title, as the Commander sees it.</summary>
    private static string? PageTitle(SettingsView view) =>
        view.FindControl<StackPanel>("Cards")!.Children
            .OfType<StackPanel>()
            .Single(panel => panel.Name == SettingsView.PageHeadName)
            .GetVisualDescendants().OfType<TextBlock>()
            .Single(text => text.FontSize == Theming.TypeScale.Title)
            .Text;

    /// <summary>A reveal opens the page holding the capability, from whichever page was open.</summary>
    [AvaloniaFact]
    public void RevealingASectionOpensItsPage()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        host.View.Reveal(MemoryCapability.Id);
        Jobs();

        host.View.Reveal(ListeningCapability.Id);
        Jobs();

        Assert.Equal("Voice input", PageTitle(host.View), ignoreCase: true);

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
        var title = PageTitle(host.View);

        host.View.Reveal(ShipsCapability.Id);
        Jobs();

        Assert.Equal(active, host.View.ActiveSection);
        Assert.Equal(title, PageTitle(host.View));

        host.Close();
    }

    /// <summary>An id no section owns does nothing rather than throwing.</summary>
    [AvaloniaFact]
    public void AnIdNoSectionOwnsIsIgnored()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        var active = host.View.ActiveSection;

        host.View.Reveal("telepathy");
        Jobs();

        // Still standing, and still showing the page it was on.
        Assert.Equal(active, host.View.ActiveSection);
        Assert.NotNull(PageTitle(host.View));

        host.Close();
    }
}
