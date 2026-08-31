using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
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
        panel.EnableHelp(_ => { });

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
    /// Words while they fit, marks when they do not — before any tab leaves the strip (#234).
    /// <para>
    /// <b>Scrolling used to be the first resort and is now the last.</b> On a narrow window whole
    /// tabs went off the end while the ones still visible carried full words, which is the wrong
    /// thing to spend the width on: a Commander can recognise a mark, and cannot click a tab that
    /// is not there.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheTabsShedTheirWordsBeforeTheyShedTabs()
    {
        var wide = Furnished(1400);

        Assert.Equal("Transcript", wide.GetControl<RadioButton>("TranscriptTab").Content);

        var narrow = Furnished(420);

        // The mark, and the word kept where it still has to be reachable.
        var tab = narrow.GetControl<RadioButton>("TranscriptTab");

        Assert.IsType<Avalonia.Controls.Shapes.Path>(tab.Content);
        Assert.Equal("Transcript", Avalonia.Automation.AutomationProperties.GetName(tab));
        Assert.Equal("Transcript", ToolTip.GetTip(tab));
    }

    /// <summary>
    /// And the strip goes back to words when the room comes back, without flickering between the
    /// two on the way.
    /// <para>
    /// The decision is made against the width the <em>words</em> wanted, remembered from when they
    /// were last drawn. Made against the current extent it would feed itself: collapsing shrinks
    /// the extent, a shrunken extent fits, fitting expands, expanding overflows.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheWordsComeBackWhenTheRoomDoes()
    {
        var panel = Furnished(420);

        Assert.IsType<Avalonia.Controls.Shapes.Path>(
            panel.GetControl<RadioButton>("TranscriptTab").Content);

        if (panel.Parent is ContentControl host)
        {
            host.Width = 1400;
        }

        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Transcript", panel.GetControl<RadioButton>("TranscriptTab").Content);
    }

    /// <summary>
    /// The strip holds the tabs, the steppers, and the panel's own chrome — and nothing that
    /// competes with the tabs for width.
    /// <para>
    /// <b>The mode control and the search box are still barred</b>, which is what this test was
    /// written for: they are sized by their content, they grew with it, and below a certain
    /// window width the row overlapped itself. They live in the pane.
    /// </para>
    /// <para>
    /// <b>The avatar, the badge and the help mark joined it in #234.</b> They are fixed-size and
    /// docked, so they take the same few pixels at every width — and they were costing a whole
    /// band of height above the strip to do it.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheStripHoldsTabsTheSteppersAndTheChrome()
    {
        var panel = Furnished(1400);
        var strip = panel.GetControl<DockPanel>("TabStrip");

        Assert.All(
            strip.Children,
            child => Assert.True(
                child is ScrollViewer or Button or StackPanel,
                $"{child.GetType().Name} is in the tab strip and should not be"));

        // The chrome is one row of a fixed height rather than three controls each aligning
        // themselves, because bottom-aligning three different heights lines up their bottom edges
        // and nothing a reader looks at.
        var chrome = panel.GetControl<StackPanel>("ChromeRow");

        Assert.Equal(35, chrome.Height);
        Assert.All(chrome.Children, child => Assert.Equal(VerticalAlignment.Center, child.VerticalAlignment));

        // And the avatar is last in it, which puts it rightmost — the Commander asked for that by
        // name.
        Assert.IsType<D47.App.Panel.AvatarView>(chrome.Children[^1]);
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

        // It draws a mark rather than the words now (asked for 2026-08-24), so what is asserted is
        // the name it still answers to — which is the half that has to survive a picture.
        Assert.Equal(
            "Copy this whole page to the clipboard",
            Avalonia.Automation.AutomationProperties.GetName(copy));

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

    /// <summary>
    /// The help glyph sits in the middle of the box that highlights under it
    /// (remediation.md 10, item 4).
    /// <para>
    /// Measured rather than looked at, because "not quite centred" is exactly the kind of thing an
    /// eye reports and a capture argues about. The highlight is the button's own background, so
    /// the claim is that the drawn mark's centre and the button's centre are the same point.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheHelpGlyphIsCentredInItsButton()
    {
        var panel = Furnished(1200);

        var button = panel.GetControl<Button>("HelpButton");
        var glyph = panel.GetControl<Avalonia.Controls.Shapes.Path>("HelpGlyph");

        // The drawn geometry, not the element's box. The box was always centred; what hung left
        // was the ink inside it, because Stretch scales the geometry's own bounds and a question
        // mark is about half as wide as it is tall. A test measuring the box passes either way and
        // proves nothing — the first version of this one did exactly that.
        Assert.NotNull(glyph.RenderedGeometry);

        var ink = glyph.RenderedGeometry.Bounds;

        var middle = glyph.TranslatePoint(
            new Point(ink.X + (ink.Width / 2), ink.Y + (ink.Height / 2)),
            button);

        Assert.NotNull(middle);

        // Half a pixel either way: a square button with an odd-width glyph in it cannot land on a
        // whole number, and anything a Commander can see is a great deal larger than that.
        Assert.True(
            Math.Abs(middle.Value.X - (button.Bounds.Width / 2)) <= 0.5,
            $"the glyph sits at x={middle.Value.X} in a {button.Bounds.Width}-wide button");

        Assert.True(
            Math.Abs(middle.Value.Y - (button.Bounds.Height / 2)) <= 0.5,
            $"the glyph sits at y={middle.Value.Y} in a {button.Bounds.Height}-tall button");
    }
}
