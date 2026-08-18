using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The tab strip at any width, and what moved out of it (remediation.md 10, items 1, 2 and 3).
/// <para>
/// The strip carried the tabs, the mode control and the search box in one row. A DockPanel gives
/// its docked children what they ask for and leaves the filling child whatever is left, which
/// below a certain width is a negative number rather than a scrollbar — so the tabs and the
/// controls beside them simply drew on top of each other.
/// </para>
/// </summary>
public class TheTabStripFitsAnyWidthTests
{
    private static PanelView Furnished(double width)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.Furnish(
            PanelTab.Loadout,
            crumb => new TextBlock { Text = crumb.Word },
            new NavCrumb("fleet", "Ships"),
            new NavCrumb("locker", "Suits and weapons"));

        panel.Furnish(PanelTab.Checklist, _ => new TextBlock(), new NavCrumb("checklist", "Checklist"));
        panel.Furnish(PanelTab.Engineers, _ => new TextBlock(), new NavCrumb("engineers", "Engineers"));
        panel.Furnish(PanelTab.Utilities, _ => new TextBlock(), new NavCrumb("utilities", "Utilities"));
        panel.EnableSettings(() => new TextBlock());

        var window = new Window { Content = panel, Width = width, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    /// <summary>
    /// Wide enough for every tab: no steppers, because a pair of arrows either side of six tabs
    /// that all fit is two controls that permanently do nothing.
    /// </summary>
    [AvaloniaFact]
    public void AWideSurfaceShowsNoSteppers()
    {
        var panel = Furnished(1400);

        Assert.False(panel.GetControl<Button>("TabsLeft").IsVisible);
        Assert.False(panel.GetControl<Button>("TabsRight").IsVisible);
    }

    /// <summary>
    /// And narrow enough that they do not: the steppers appear, and every tab stays reachable
    /// rather than being drawn underneath its neighbour.
    /// </summary>
    [AvaloniaFact]
    public void ANarrowSurfaceGainsSteppersAndScrolls()
    {
        var panel = Furnished(340);

        Assert.True(panel.GetControl<Button>("TabsLeft").IsVisible);
        Assert.True(panel.GetControl<Button>("TabsRight").IsVisible);

        var scroller = panel.GetControl<ScrollViewer>("TabsScroller");

        Assert.Equal(0, scroller.Offset.X);

        panel.GetControl<Button>("TabsRight").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(scroller.Offset.X > 0, "the strip did not scroll");
    }

    /// <summary>
    /// Nothing but tabs in the strip. The mode control and the search box are what made the row a
    /// competition, and they are in the pane now.
    /// </summary>
    [AvaloniaFact]
    public void TheStripHoldsTabsAndTheSteppersAndNothingElse()
    {
        var panel = Furnished(1400);
        var strip = panel.GetControl<DockPanel>("TabStrip");

        Assert.All(
            strip.Children,
            child => Assert.True(
                child is ScrollViewer or Button,
                $"{child.GetType().Name} is in the tab strip and should not be"));
    }

    /// <summary>
    /// Copy All is the transcript's and the desktop's. It had no visibility rule at all, so it
    /// offered to copy the transcript from Checklist, Loadout, Engineers and Settings.
    /// </summary>
    [AvaloniaFact]
    public void CopyBelongsToTheTranscriptAlone()
    {
        var panel = Furnished(1400);

        panel.EnableSearch();
        Dispatcher.UIThread.RunJobs();

        var copy = panel.GetControl<Button>("CopyButton");

        Assert.True(copy.IsVisible);
        Assert.Equal("Copy All", copy.Content);

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        Assert.False(copy.IsVisible);
    }

    /// <summary>
    /// And it is the desktop's alone, which it was only by accident before: it lived inside the
    /// search row, and only the window turns that on. A headset has no clipboard to copy into.
    /// </summary>
    [AvaloniaFact]
    public void ASurfaceWithNoSearchHasNoCopyEither()
    {
        var panel = Furnished(1400);

        Assert.False(panel.GetControl<Button>("CopyButton").IsVisible);
    }

    /// <summary>
    /// The page bar exists only when it has something in it. It is a row, and a row costs height
    /// the pane below does not get — nothing on a desktop window, and most of the page on a
    /// 280-pixel headset panel.
    /// </summary>
    [AvaloniaFact]
    public void ABarWithNothingInItIsNotDrawn()
    {
        var panel = Furnished(1400);

        // Settings has one root and this surface has no search, so there is nothing for the bar
        // to carry.
        panel.Tab = PanelTab.Settings;
        Dispatcher.UIThread.RunJobs();

        Assert.False(panel.GetControl<DockPanel>("PageBar").IsVisible);

        // The transcript has three readings, so it does.
        panel.Tab = PanelTab.Transcript;
        Dispatcher.UIThread.RunJobs();

        Assert.True(panel.GetControl<DockPanel>("PageBar").IsVisible);
    }
}
