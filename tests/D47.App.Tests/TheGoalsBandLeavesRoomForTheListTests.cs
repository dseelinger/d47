using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Goals;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Opening the goals band does not push the list off the page (remediation.md 11, item 4).
/// <para>
/// Reported with a picture: nine arcs, the third one cut off at the bottom edge, and the checklist
/// underneath gone entirely. The band was docked to the top of the page, and a docked child takes
/// the height it asks for — nine arcs ask for all of it.
/// </para>
/// </summary>
public class TheGoalsBandLeavesRoomForTheListTests
{
    private static Window? _window;

    private static (PanelView Panel, ChecklistService Checklists) Open(double height = 500)
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-goals-band-tests"));

        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        checklists.AddNote(ChecklistScope.Universal, "Get a new paint job");

        var goals = new GoalBook(
            new GoalStore(Path.Combine(paths.Data, "goals.json"), NullLogger<GoalStore>.Instance),
            () => null,
            () => null,
            checklists);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableChecklist(checklists, goals);

        var window = _window = new Window { Content = panel, Width = 1200, Height = height };

        window.Show();
        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        return (panel, checklists);
    }

    /// <summary>
    /// The band's scroller, found by name through the visual tree: it is built in code rather than
    /// declared in markup, so there is no name scope to ask.
    /// </summary>
    private static ScrollViewer BandScroller(PanelView panel) =>
        panel.GetVisualDescendants().OfType<ScrollViewer>().First(scroller => scroller.Name == "GoalsBand");

    /// <summary>
    /// The control that opens the band. A <c>CheckBox</c> since #203 — it was already a two-state
    /// disclosure with a hand-swapped label, and the label swap was throwing away the running
    /// count once the band was open.
    /// </summary>
    private static CheckBox Band(PanelView panel) =>
        panel.GetVisualDescendants()
            .OfType<CheckBox>()
            .First(box => (box.Content as string)?.StartsWith("Goals", StringComparison.Ordinal) == true);

    /// <summary>Opens or closes it the way a press does, so the page rebuilds around it.</summary>
    private static void Toggle(PanelView panel)
    {
        var band = Band(panel);

        band.IsChecked = band.IsChecked != true;
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Every scroller on the page, so the list's own can be found by what it holds.</summary>
    private static ScrollViewer ListScroller(PanelView panel) =>
        panel.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .First(scroller => scroller.Content is StackPanel stack
                               && stack.GetVisualDescendants().OfType<CheckBox>().Any());

    /// <summary>
    /// The report itself: open the band and the list is still there to work in.
    /// </summary>
    [AvaloniaFact]
    public void OpeningTheBandLeavesTheListOnScreen()
    {
        var (panel, _) = Open();

        Toggle(panel);

        // **The count survives being open** (#203). The label used to swap to "Hide goals", which
        // threw away the one number that makes anybody open the band in the first place.
        Assert.True(Band(panel).IsChecked);
        Assert.StartsWith("Goals (", (string)Band(panel).Content!, StringComparison.Ordinal);

        var list = ListScroller(panel);

        Assert.True(
            list.Bounds.Height > 80,
            $"the list was left {list.Bounds.Height} pixels tall");
    }

    /// <summary>
    /// And the arcs themselves are reachable rather than clipped: what does not fit scrolls.
    /// </summary>
    [AvaloniaFact]
    public void TheBandScrollsWhatDoesNotFit()
    {
        var (panel, _) = Open();

        Toggle(panel);

        var band = BandScroller(panel);

        Assert.True(band.IsVisible);
        Assert.True(
            band.Extent.Height > band.Viewport.Height,
            "the band is not taller than its window, so this proves nothing about scrolling");

        band.Offset = new Avalonia.Vector(0, band.Extent.Height - band.Viewport.Height);
        Dispatcher.UIThread.RunJobs();

        Assert.True(band.Offset.Y > 0, "the band did not scroll");
    }

    /// <summary>
    /// Closed, it costs nothing at all — it is a header a Commander opens, not a third of the tab.
    /// </summary>
    [AvaloniaFact]
    public void TheClosedBandTakesNoRoom()
    {
        var (panel, _) = Open();

        Assert.False(BandScroller(panel).IsVisible);
        Assert.Equal(0, BandScroller(panel).Bounds.Height);
    }

    /// <summary>
    /// A short window is where this bites hardest, and the list still gets a share of it rather
    /// than the band taking everything and leaving a sliver.
    /// </summary>
    [AvaloniaFact]
    public void EvenAShortWindowKeepsSomeList()
    {
        var (panel, _) = Open(360);

        Toggle(panel);

        Assert.True(
            ListScroller(panel).Bounds.Height > 60,
            $"the list was left {ListScroller(panel).Bounds.Height} pixels tall");
    }

    /// <summary>A picture of the band open, for looking at rather than for asserting on.</summary>
    [AvaloniaFact]
    public void TheOpenBandRendersToACapture()
    {
        new D47.App.Theming.ThemeManager(
                Avalonia.Application.Current!,
                NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var (panel, _) = Open(620);

        Toggle(panel);

        _window!.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "goals-band.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }
}
