using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Every address the window is on is one every UI is on; a UI that cannot draw the page there says so (#2738).</summary>
public class AUiThatCannotDrawAnAddressSaysNotAvailableHereTests
{
    private const string Checklist = "checklist";
    private const string Standing = "standing";

    [AvaloniaFact]
    public void TheOverlayOnSettingsNamesItAndSaysNotAvailableHere()
    {
        var (window, overlay) = Pair(overlayMode: PanelMode.Mini);

        window.Tab = PanelTab.Settings;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Settings, overlay.Nav.StandingIn?.Tab);
        Assert.Equal("SETTINGS", TrailOf(overlay));
        Assert.Equal(PanelView.NotAvailableHere, StandInText(overlay));
        Assert.False(overlay.GetControl<Border>("TranscriptPane").IsVisible);

        window.Tab = PanelTab.Transcript;
        Dispatcher.UIThread.RunJobs();

        Assert.Null(overlay.Nav.StandingIn);
        Assert.Null(StandInText(overlay));
        Assert.True(overlay.GetControl<Border>("TranscriptPane").IsVisible);
    }

    [AvaloniaFact]
    public void AHeadsetInMiniOnSearchStaysAndShowsTheStandInAndBigDrawsThePage()
    {
        var (window, headset) = Pair(overlayMode: PanelMode.Full);

        headset.Furnish(PanelTab.Search, _ => new TextBlock { Text = "search page" }, new NavCrumb("search.system", "System"));
        window.Tab = PanelTab.Search;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Search, headset.Tab);
        Assert.Null(StandInText(headset));

        headset.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Search, headset.Tab);
        Assert.Equal("SEARCH", TrailOf(headset));
        Assert.Equal(PanelView.NotAvailableHere, StandInText(headset));

        headset.Mode = PanelMode.Full;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Search, headset.Tab);
        Assert.Null(StandInText(headset));
        Assert.IsType<DrillView>(headset.GetControl<Border>("PagePane").Child);
    }

    [AvaloniaFact]
    public void TheBigPanelWritesTheFullPathAndHighlightsTheTabItHas()
    {
        var (window, big) = Pair(overlayMode: PanelMode.Full);

        window.Tab = PanelTab.Commander;
        Assert.True(window.Nav.SelectRoot(PanelTab.Commander, Standing));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Commander, big.Tab);
        Assert.Equal(PanelView.NotAvailableHere, StandInText(big));
        Assert.Equal(["Commander", "Standing"], Crumbs(big));
        Assert.True(big.GetControl<RadioButton>("CommanderTab").IsChecked);

        Assert.True(window.Nav.Drill(new NavCrumb("rank", "A rank")));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Commander", "Standing", "A rank"], Crumbs(big));
        Assert.All(
            big.GetControl<StackPanel>("CrumbRow").Children.OfType<Button>(),
            crumb => Assert.False(crumb.IsEnabled));
    }

    /// <summary>The follower's own dialog with the same key is not what it draws for the window's.</summary>
    [AvaloniaFact]
    public void ADialogOpenedInTheWindowIsTheStandInElsewhereNotTheFollowersOwnDialog()
    {
        var (window, big) = Pair(overlayMode: PanelMode.Full);

        window.Tab = PanelTab.Commander;
        Dispatcher.UIThread.RunJobs();

        var own = new Confirm("own");
        _ = big.Open(own);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("dialog:1", big.Nav.Trail[^1].Key);

        _ = window.Open(new Confirm("window's"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("dialog:1", window.Nav.Trail[^1].Key);
        Assert.Equal(["checklist", "dialog:1"], big.Nav.StandingIn?.Trail.Select(crumb => crumb.Key));
        Assert.Equal(PanelView.NotAvailableHere, StandInText(big));
        Assert.Equal(["Commander", "Checklist", "Confirm"], Crumbs(big));

        window.Nav.Back();
        Dispatcher.UIThread.RunJobs();

        Assert.Null(big.Nav.StandingIn);
        Assert.Null(StandInText(big));
        Assert.Equal([Checklist], big.Nav.Trail.Select(crumb => crumb.Key));
    }

    [AvaloniaFact]
    public void AChooserStaysOnTheWindowAndTheOthersStayOnThePageUnderIt()
    {
        var (window, big) = Pair(overlayMode: PanelMode.Full);

        window.Tab = PanelTab.Commander;
        Dispatcher.UIThread.RunJobs();

        Assert.True(window.Nav.Take(new NavCrumb("pick", "Pick one")));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(big.Nav.StandingIn);
        Assert.False(big.Nav.Modal);
        Assert.Null(StandInText(big));
    }

    /// <summary>The panel reads as one place while UIs stand in, so a toggle closes rather than opens again.</summary>
    [AvaloniaFact]
    public void EveryUiReadsAsAgreeingWhileOneStandsIn()
    {
        var (window, overlay) = Pair(overlayMode: PanelMode.Mini);
        var routing = new PanelRouting(() => false, () => []);

        routing.RouteNavigation(window.Nav, move => move(), leads: true);
        routing.RouteNavigation(overlay.Nav, move => move());

        window.Tab = PanelTab.Settings;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(SettingsFurnishing.Root, routing.Snapshot.Showing);

        window.Tab = PanelTab.Commander;
        _ = window.Open(new Confirm("window's"));
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(overlay.Nav.StandingIn);
        Assert.Equal(Checklist, routing.Snapshot.Showing);
    }

    private sealed class Confirm : DialogPage
    {
        public Confirm(string text) => Content = new TextBlock { Text = text };

        public override string Crumb => "Confirm";
    }

    /// <summary>The window has Settings and two Commander roots; the follower has the first Commander root only.</summary>
    private static (PanelView Window, PanelView Follower) Pair(PanelMode overlayMode)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var model = new PanelViewModel();
        var window = new PanelView { DataContext = model };
        var follower = new PanelView { DataContext = model, Mode = overlayMode };

        window.EnableSettings(() => new TextBlock { Text = "settings" });
        window.Furnish(PanelTab.Search, _ => new TextBlock { Text = "search page" }, new NavCrumb("search.system", "System"));
        window.Furnish(
            PanelTab.Commander,
            crumb => new TextBlock { Text = crumb.Word },
            new NavCrumb(Checklist, "Checklist"),
            new NavCrumb(Standing, "Standing"));
        follower.Furnish(PanelTab.Commander, crumb => new TextBlock { Text = crumb.Word }, new NavCrumb(Checklist, "Checklist"));

        new Window { Content = new StackPanel { Children = { window, follower } }, Width = 900, Height = 1280 }.Show();

        var mirror = new TranscriptMirror();
        mirror.Lead(window.Nav);
        mirror.Add(follower.Nav);
        Dispatcher.UIThread.RunJobs();

        return (window, follower);
    }

    private static string? StandInText(PanelView view) =>
        view.GetControl<Border>("PagePane") is { IsVisible: true, Child: TextBlock { Text: PanelView.NotAvailableHere } line } ? line.Text : null;

    private static string? TrailOf(PanelView view) =>
        AutomationProperties.GetName(view.GetLogicalDescendants().OfType<Control>().Single(control => control.Name == "MiniTrailRow"));

    private static List<string> Crumbs(PanelView view) =>
        [.. view.GetControl<StackPanel>("CrumbRow").Children.OfType<Button>().Select(crumb => crumb.Content as string ?? string.Empty)];
}

/// <summary>Reads whether a panel shows the stand-in in its page area.</summary>
internal static class StandIns
{
    public static bool Shows(PanelView view) =>
        view.GetControl<Border>("PagePane") is { IsVisible: true, Child: TextBlock { Text: PanelView.NotAvailableHere } };
}
