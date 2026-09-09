using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>The panel's way into the documentation site.</summary>
public class HelpButtonTests
{
    /// <summary>It is what is left in the header's right-hand corner.</summary>
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

    /// <summary>A surface the host handed nothing — which is how the headset builds its copy — shows the mark anyway.</summary>
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
