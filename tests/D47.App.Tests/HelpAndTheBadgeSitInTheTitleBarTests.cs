using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Interface;
using D47.Core.Updates;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// On the desktop, HELP and the PRE-RELEASE badge are in the window's title bar and the tab row holds only
/// tabs; the headset's panel keeps both on its tab row (#550).
/// </summary>
public class HelpAndTheBadgeSitInTheTitleBarTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static MainWindow Shown(ReleaseChannel channel, double width = 1280)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var window = new MainWindow(host: null) { Width = width, Height = 800 };
        window.Panel.ShowChannel(channel);
        window.Show();
        Jobs();

        return window;
    }

    private static Control Strip(Window window) =>
        window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "CaptionStrip");

    private static T InStrip<T>(Window window, string name)
        where T : Control =>
        Strip(window).GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static object? Resource(string key) =>
        Application.Current!.TryFindResource(key, out var value) ? value : null;

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void HelpIsInTheTitleBarAndNotOnTheTabRow()
    {
        var window = Shown(ReleaseChannel.Release);

        var help = InStrip<Button>(window, "TitleBarHelp");

        Assert.True(help.IsVisible);
        Assert.Equal(CaptionStrip.StripHeight, help.Bounds.Height);
        Assert.Equal(12, help.FontSize);
        Assert.False(window.Panel.GetControl<Button>("HelpButton").IsVisible);

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void HelpSitsJustBeforeTheWindowControls()
    {
        var window = Shown(ReleaseChannel.Release);

        var help = InStrip<Button>(window, "TitleBarHelp");
        var controls = Strip(window).GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("caption-button"))
            .ToList();

        var helpRight = help.TranslatePoint(new Point(help.Bounds.Width, 0), window)!.Value.X;

        Assert.All(controls, button => Assert.True(
            button.TranslatePoint(default, window)!.Value.X >= helpRight, $"{button} is left of HELP"));

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void PressingTitleBarHelpOpensHelpInThePanel()
    {
        var window = Shown(ReleaseChannel.Release);

        InStrip<Button>(window, "TitleBarHelp")
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Jobs();

        Assert.True(window.Panel.Nav.Modal, "help took the panel");
        Assert.Equal("Help", window.Panel.Nav.Trail[^1].Word);

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void APreReleaseShowsTheBadgeInWarnRightAfterTheVersion()
    {
        var window = Shown(ReleaseChannel.PreRelease);

        var badge = InStrip<Border>(window, "TitleBarBadge");
        var text = InStrip<TextBlock>(window, "TitleBarBadgeText");

        Assert.True(badge.IsVisible);
        Assert.Equal("PRE-RELEASE", text.Text);
        Assert.Equal(22, badge.Bounds.Height);
        Assert.Equal(11, text.FontSize);
        Assert.Equal(FontWeight.SemiBold, text.FontWeight);
        Assert.Equal(Resource(ThemeManager.WarnGroundKey), badge.Background);
        Assert.Equal(Resource(ThemeManager.WarnKey), text.Foreground);
        Assert.False(window.Panel.GetControl<Border>("PreReleaseBadge").IsVisible);

        // 10px after the version, which is the text block before it in the same row.
        var row = (StackPanel)badge.Parent!;
        var version = row.Children[row.Children.IndexOf(badge) - 1];
        var gap = badge.TranslatePoint(default, window)!.Value.X
                  - version.TranslatePoint(new Point(version.Bounds.Width, 0), window)!.Value.X;

        Assert.Equal(10, gap, precision: 0);

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void AReleaseShowsNoBadge()
    {
        var window = Shown(ReleaseChannel.Release);

        Assert.False(InStrip<Border>(window, "TitleBarBadge").IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void ADialogsStripCarriesNeither()
    {
        var dialog = new Window { Content = new TextBlock { Text = "Dialog" } };
        CaptionStrip.Apply(dialog, showMinimize: false);
        dialog.Show();
        Jobs();

        var names = Strip(dialog).GetVisualDescendants().Select(c => c.Name).ToList();

        Assert.DoesNotContain("TitleBarHelp", names);
        Assert.DoesNotContain("TitleBarBadge", names);

        dialog.Close();
    }

    /// <summary>The headset's panel is a bare <see cref="PanelView"/> no window took the chrome from.</summary>
    [AvaloniaFact]
    public void TheHeadsetsPanelKeepsHelpAndTheBadgeOnItsTabRow()
    {
        var view = new PanelView { DataContext = new PanelViewModel() };
        view.Classes.Add("headset");
        view.ShowChannel(ReleaseChannel.PreRelease);

        var window = new Window { Content = view };
        window.Show();
        Jobs();

        var chrome = view.GetControl<StackPanel>("ChromeRow");

        Assert.True(view.GetControl<Button>("HelpButton").IsVisible);
        Assert.True(view.GetControl<Border>("PreReleaseBadge").IsVisible);
        Assert.Contains(view.GetControl<Button>("HelpButton"), chrome.Children);
        Assert.Contains(view.GetControl<Border>("PreReleaseBadge"), chrome.Children);

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void At1280EveryTabFitsOnOneRowAndTheTabRowHoldsOnlyTabs()
    {
        using var look = AppLook.Put();
        var window = Shown(ReleaseChannel.PreRelease);
        var panel = window.Panel;

        EveryTabDrawsOneHeaderTests.FurnishEveryTabButSettings(panel);
        panel.EnableSettings(() => new TextBlock { Text = "Settings" });
        Jobs();
        Jobs();

        var tabs = panel.GetControl<Avalonia.Controls.Panel>("Tabs").Children.Where(tab => tab.IsVisible).ToList();

        Assert.Equal(6, tabs.Count);
        Assert.Single(tabs.Select(tab => tab.TranslatePoint(default, panel)!.Value.Y).Distinct());
        Assert.DoesNotContain(
            panel.GetControl<StackPanel>("ChromeRow").Children, child => child.IsVisible);
        Assert.True(panel.Avatar.IsVisible);

        window.CaptureRenderedFrame()!.SaveCapture("title-bar-chrome-1280.png");

        window.Close();
    }
}
