using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Tabs setting's second value: the tabs in a rail down the left, the chrome above the page (#491).</summary>
public class TheTabsCanRunDownTheLeftTests
{
    private static readonly string[] TabNames =
    [
        "TranscriptTab", "LoadoutTab", "EngineersTab", "ChecklistTab",
        "RoutingTab", "AdventuresTab", "UtilitiesTab", "SettingsTab",
    ];

    /// <summary>A panel with every tab shown, as the desktop window has them once its hosts have furnished them.</summary>
    private static PanelView Panel()
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        foreach (var name in TabNames)
        {
            panel.FindControl<RadioButton>(name)!.IsVisible = true;
        }

        return panel;
    }

    private static (Window Window, PanelView Panel) Open(bool left, double width = 1024)
    {
        var panel = Panel();
        panel.SetTabsDownTheLeft(left);

        var window = new Window { Content = panel, Width = width, Height = 640 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    [AvaloniaFact]
    public void TheRailHoldsTheTabsAndTheChromeMovesAboveThePage()
    {
        using var look = AppLook.Put();
        var (window, panel) = Open(left: true);

        var strip = panel.FindControl<DockPanel>("TabStrip")!;
        var chrome = panel.FindControl<Control>("ChromeRow")!;

        Assert.Equal(Dock.Left, DockPanel.GetDock(strip));
        Assert.Same(panel.FindControl<Border>("ChromeSlot"), chrome.Parent);
        Assert.True(panel.FindControl<Control>("CrumbBar")!.IsVisible);

        var tabs = TabNames.Select(name => panel.FindControl<RadioButton>(name)!).ToList();
        var width = tabs[0].Bounds.Width;

        Assert.All(tabs, tab => Assert.Equal(width, tab.Bounds.Width));
        Assert.All(tabs, tab => Assert.True(tab.Bounds.Height >= 44));

        // Stacked in strip order, top to bottom.
        Assert.Equal(tabs.OrderBy(tab => tab.Bounds.Y), tabs);

        window.Close();
    }

    [AvaloniaFact]
    public void ARailTabIsWideEnoughForTheLongestLabel()
    {
        using var look = AppLook.Put();
        var (window, panel) = Open(left: true);

        foreach (var name in TabNames)
        {
            var label = panel.FindControl<RadioButton>(name)!.GetVisualDescendants().OfType<TextBlock>().Single();

            Assert.DoesNotContain(label.TextLayout.TextLines, line => line.HasCollapsed);
        }

        window.Close();
    }

    [AvaloniaFact]
    public void BackAlongTheTopIsTheStripItWas()
    {
        using var look = AppLook.Put();
        var (window, panel) = Open(left: true);

        panel.SetTabsDownTheLeft(false);
        Dispatcher.UIThread.RunJobs();

        var strip = panel.FindControl<DockPanel>("TabStrip")!;
        var chrome = panel.FindControl<Control>("ChromeRow")!;

        Assert.Equal(Dock.Top, DockPanel.GetDock(strip));
        Assert.Same(strip, chrome.Parent);
        Assert.Same(chrome, strip.Children[0]);
        Assert.False(panel.FindControl<Control>("CrumbBar")!.IsVisible);
        Assert.Equal(40, panel.FindControl<RadioButton>("TranscriptTab")!.Bounds.Height);

        window.Close();
    }

    [AvaloniaFact]
    public void TheSettingIsANoOpWhenItHasNotMoved()
    {
        var panel = Panel();

        Assert.False(panel.SetTabsDownTheLeft(false));
        Assert.True(panel.SetTabsDownTheLeft(true));
        Assert.False(panel.SetTabsDownTheLeft(true));
    }

    [AvaloniaFact]
    public void DownMovesToTheNextTabInTheRail()
    {
        using var look = AppLook.Put();
        var (window, panel) = Open(left: true);

        var transcript = panel.FindControl<RadioButton>("TranscriptTab")!;
        transcript.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(panel.FindControl<RadioButton>("LoadoutTab"), TopLevel.GetTopLevel(panel)!.FocusManager!.GetFocusedElement());

        window.Close();
    }

    [AvaloniaFact]
    public void BothPlacementsRenderToACapture()
    {
        foreach (var (left, width, file) in new[]
        {
            (false, 1024.0, "tabs-along-the-top.png"),
            (true, 1024.0, "tabs-down-the-left.png"),
            (true, 640.0, "tabs-down-the-left-narrow.png"),
        })
        {
            var panel = Panel();
            panel.SetTabsDownTheLeft(left);

            var path = AppLook.Capture(panel, file, width: width);

            Assert.True(File.Exists(path));
        }
    }
}
