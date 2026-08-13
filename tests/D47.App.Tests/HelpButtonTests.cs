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
/// headset overlay instantiates too — so it asks rather than acts, exactly as the gear does.
/// </summary>
public class HelpButtonTests
{
    [AvaloniaFact]
    public void TheHeaderCarriesAHelpButtonBesideTheGear()
    {
        var window = new MainWindow(host: null);
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var buttons = window.GetVisualDescendants().OfType<Button>().Select(b => b.Name).ToList();

        Assert.Contains("HelpButton", buttons);
        Assert.Contains("SettingsButton", buttons);

        window.Close();
    }

    /// <summary>
    /// Pressing it raises a request rather than opening anything. A panel that launched a
    /// browser would be a panel that knows what a desktop is, and one of the two surfaces it
    /// renders to has no desktop at all.
    /// </summary>
    [AvaloniaFact]
    public void PressingItAsksTheSurfaceRatherThanOpeningABrowser()
    {
        var model = new PanelViewModel();

        var asked = 0;
        model.HelpRequested += () => asked++;

        var view = new PanelView { DataContext = model };
        var window = new Window { Content = view };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var help = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "HelpButton");

        help.Command?.Execute(null);
        help.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1, asked);

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

    /// <summary>Mini drops the header, so it drops both buttons with it.</summary>
    [AvaloniaFact]
    public void MiniModeHidesItAlongWithTheGear()
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
