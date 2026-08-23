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
    /// Pressing it draws help in the panel rather than handing the press back to the host.
    /// <para>
    /// This test asserted the opposite until the index existed, and both readings were right in
    /// their turn. When the only thing behind the mark was a browser, the panel had to hand the
    /// press out — a panel that launched one would be a panel that knows what a desktop is, and
    /// one of its two surfaces has no desktop at all. Now there is always something to draw, so
    /// the press is answered here and the host's opener is what <em>links</em> use.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void PressingItDrawsHelpRatherThanHandingThePressOut()
    {
        var opened = 0;

        var view = new PanelView { DataContext = new PanelViewModel() };
        view.EnableHelp(_ => opened++);

        var window = new Window { Content = view };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var help = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "HelpButton");
        Assert.True(help.IsVisible);

        help.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(view.Nav.Modal, "help took the panel");
        Assert.Equal("Help", view.Nav.Trail[^1].Word);
        Assert.Equal(0, opened);

        window.Close();
    }

    /// <summary>
    /// A surface the host handed nothing — which is how the headset builds its copy — shows the
    /// mark anyway now, and that reverses change-requests.md 24 on purpose.
    /// <para>
    /// That request hid the button because the only thing behind it was a browser the headset
    /// cannot see, and a control that does nothing is worse than an absent one. The complaint was
    /// never about the mark; it was about there being nothing behind it. There is now — help is
    /// drawn in the panel — so hiding it would withhold the feature from the surface it was built
    /// for.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void ASurfaceWithNoBrowserShowsTheMarkAnyway()
    {
        var view = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = view };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var help = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "HelpButton");
        Assert.True(help.IsVisible);

        help.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(view.Nav.Modal, "and it opens something, with no browser anywhere");

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
