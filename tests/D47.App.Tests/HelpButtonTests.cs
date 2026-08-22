using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The panel's way into the documentation site. The button lives on the panel, which the
/// headset overlay instantiates too — so it asks rather than acts.
/// </summary>
public class HelpButtonTests
{
    /// <summary>
    /// It is what is left in the header's right-hand corner. The gear stood beside it until
    /// Phase 12 made settings a page of this window, chosen from the tab strip.
    /// </summary>
    [AvaloniaFact]
    public void TheHeaderCarriesAHelpButtonAndNoGear()
    {
        var window = new MainWindow(host: null);
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var buttons = window.GetVisualDescendants().OfType<Button>().Select(b => b.Name).ToList();

        Assert.Contains("HelpButton", buttons);
        Assert.DoesNotContain("SettingsButton", buttons);

        window.Close();
    }

    /// <summary>
    /// Pressing it does what the host handed in and nothing of its own. A panel that launched a
    /// browser would be a panel that knows what a desktop is, and one of the two surfaces it
    /// renders to has no desktop at all.
    /// </summary>
    [AvaloniaFact]
    public void PressingItDoesWhatTheHostHandedIn()
    {
        var opened = 0;

        var view = new PanelView { DataContext = new PanelViewModel() };
        view.EnableHelp(() => opened++);

        var window = new Window { Content = view };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var help = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "HelpButton");
        Assert.True(help.IsVisible);

        help.Command?.Execute(null);
        help.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1, opened);

        window.Close();
    }

    /// <summary>
    /// A surface the host handed nothing — which is how the headset builds its copy — has no
    /// help button. It used to: the press raised an event on the shared model, and the one
    /// handler opened a browser on the desktop, invisible from inside the headset
    /// (change-requests.md 24).
    /// </summary>
    [AvaloniaFact]
    public void ASurfaceHandedNoWayToOpenTheSiteShowsNoButton()
    {
        var view = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = view };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var help = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "HelpButton");
        Assert.False(help.IsVisible);

        window.Close();
    }

    /// <summary>One address for the site, shared by the button and every settings row's "?".</summary>
    [Fact]
    public void TheDocumentationAddressHasOneSource()
    {
        Assert.Equal("https://dseelinger.github.io/d47/", DocsSite.Root);

        Assert.Equal(
            "https://dseelinger.github.io/d47/capabilities/listening.html#push-to-talk-key",
            DocsSite.Capability("listening", "push-to-talk-key"));

        Assert.Equal(
            "https://dseelinger.github.io/d47/capabilities/privacy.html",
            DocsSite.Capability("privacy"));
    }

    /// <summary>Mini drops the header, so it drops the button with it.</summary>
    [AvaloniaFact]
    public void MiniModeHidesItAlongWithTheRestOfTheHeader()
    {
        var view = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Mini };
        var window = new Window { Content = view };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var header = view.GetVisualDescendants().OfType<Control>().First(c => c.Name == "Header");

        Assert.False(header.IsVisible);

        window.Close();
    }
}
