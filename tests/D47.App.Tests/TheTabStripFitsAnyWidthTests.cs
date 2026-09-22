using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>The tab strip at any width, and what moved out of it.</summary>
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

    /// <summary>Every tab shows the word alone — no icon, on any width (#355).</summary>
    [AvaloniaFact]
    public void ATabShowsItsWordAloneAtAnyWidth()
    {
        foreach (var width in new[] { 340, 512, 1400 })
        {
            var panel = Furnished(width);
            var tab = panel.GetControl<RadioButton>("TranscriptTab");

            Assert.Equal("Transcript", tab.Content);
        }
    }

    /// <summary>The tab takes the label type, not the data type (#273).</summary>
    [AvaloniaFact]
    public void TheTabIsNotMonospace()
    {
        var wide = Furnished(2000);
        var tab = wide.GetControl<RadioButton>("TranscriptTab");

        Assert.DoesNotContain("Cascadia", tab.FontFamily.Name, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Too narrow for every tab on one line: the strip wraps a tab onto a second row rather than
    /// clipping it or scrolling to reach it (#355).
    /// </summary>
    [AvaloniaFact]
    public void ANarrowStripWrapsRatherThanClipping()
    {
        var panel = Furnished(512);
        var tabs = panel.GetControl<WrapPanel>("Tabs");

        var rows = tabs.Children
            .OfType<RadioButton>()
            .Where(t => t.IsVisible)
            .Select(t => t.Bounds.Y)
            .Distinct()
            .Count();

        Assert.True(rows > 1, "the tabs stayed on one row at 512px");

        Assert.All(
            tabs.Children.OfType<RadioButton>().Where(t => t.IsVisible),
            t => Assert.True(
                t.Bounds.Right <= tabs.Bounds.Width + 0.5,
                $"{t.Name} sits at x={t.Bounds.Right} outside a {tabs.Bounds.Width}-wide strip"));
    }

    /// <summary>
    /// The strip holds the tabs and the panel's own chrome — and nothing that competes with the tabs
    /// for width.
    /// </summary>
    [AvaloniaFact]
    public void TheStripHoldsTabsAndTheChrome()
    {
        var panel = Furnished(1400);
        var strip = panel.GetControl<DockPanel>("TabStrip");

        Assert.All(
            strip.Children,
            child => Assert.True(
                child is WrapPanel or StackPanel,
                $"{child.GetType().Name} is in the tab strip and should not be"));

        // Every child centred in one row, so what a reader sees lines up rather than the children's bottom edges.
        var chrome = panel.GetControl<StackPanel>("ChromeRow");

        Assert.All(chrome.Children, child => Assert.Equal(VerticalAlignment.Center, child.VerticalAlignment));

        // And the avatar is last in it, which puts it rightmost — the Commander asked for that by name.
        Assert.IsType<D47.App.Panel.AvatarView>(chrome.Children[^1]);
    }

    /// <summary>Copy All is the transcript's and the desktop's.</summary>
    [AvaloniaFact]
    public void CopyBelongsToTheTranscriptAlone()
    {
        var panel = Furnished(1400);

        panel.EnableSearch();
        Dispatcher.UIThread.RunJobs();

        var copy = panel.GetControl<Button>("CopyButton");

        Assert.True(copy.IsVisible);

        Assert.Equal(D47.App.Controls.CopyWord.Word, copy.Content);
        Assert.Equal(
            "Copy this whole page to the clipboard",
            Avalonia.Automation.AutomationProperties.GetName(copy));

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        Assert.False(copy.IsVisible);
    }

    /// <summary>
    /// And it is the desktop's alone, which it was only by accident before: it lived inside the search
    /// row, and only the window turns that on.
    /// </summary>
    [AvaloniaFact]
    public void ASurfaceWithNoSearchHasNoCopyEither()
    {
        var panel = Furnished(1400);

        Assert.False(panel.GetControl<Button>("CopyButton").IsVisible);
    }

    /// <summary>The page bar exists only when it has something in it.</summary>
    [AvaloniaFact]
    public void ABarWithNothingInItIsNotDrawn()
    {
        var panel = Furnished(1400);

        // Settings has one root and this surface has no search, so there is nothing for the bar to carry.
        panel.Tab = PanelTab.Settings;
        Dispatcher.UIThread.RunJobs();

        Assert.False(panel.GetControl<DockPanel>("PageBar").IsVisible);

        // The transcript has three readings, so it does.
        panel.Tab = PanelTab.Transcript;
        Dispatcher.UIThread.RunJobs();

        Assert.True(panel.GetControl<DockPanel>("PageBar").IsVisible);
    }

    /// <summary>Help is a quiet word on the tab strip (#360).</summary>
    [AvaloniaFact]
    public void HelpIsAQuietWord()
    {
        var button = Furnished(1200).GetControl<Button>("HelpButton");

        Assert.Equal("HELP", button.Content);
        Assert.Contains("quiet", button.Classes);
    }
}
