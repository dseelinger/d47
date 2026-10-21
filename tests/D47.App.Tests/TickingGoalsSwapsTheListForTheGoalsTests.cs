using System.Text.Json;
using Avalonia.Automation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Goals;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Ticking Goals replaces the checklist with the goals, full height, and unticking brings the list back.</summary>
public class TickingGoalsSwapsTheListForTheGoalsTests
{
    private static readonly DateTimeOffset Now = new(3311, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static Window? _window;

    private static (PanelView Panel, ChecklistService Checklists) Open(double height = 640, double width = 820)
    {
        var paths = new D47.Core.AppPaths(TestSurface.MemoryFolder("d47-goals-mode-tests"));


        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        checklists.AddNote(ChecklistScope.Universal, "Get a new paint job");
        checklists.AddNote(ChecklistScope.Universal, "Buy limpets");

        var state = new CommanderGameState(new CommanderIdentity("F1", "Jameson"));
        state.Apply(Event("Rank", "\"Combat\":3, \"Trade\":12, \"Explore\":6, \"Empire\":4, \"Federation\":2"));
        state.Apply(Event("Progress", "\"Combat\":20, \"Trade\":56, \"Explore\":10, \"Empire\":30, \"Federation\":70"));

        var goals = new GoalBook(
            new GoalStore(Path.Combine(paths.Data, "goals.json"), new MemoryFileSystem(), NullLogger<GoalStore>.Instance),
            () => "F1",
            () => state,
            checklists);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableChecklist(checklists, goals);

        var window = _window = new Window { Content = panel, Width = width, Height = height };

        window.Show();
        panel.Tab = PanelTab.Commander;
        Dispatcher.UIThread.RunJobs();

        return (panel, checklists);
    }

    private static JournalEvent Event(string kind, string fields)
    {
        var text = $"{{ \"timestamp\":\"{Now.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}\", \"event\":\"{kind}\", {fields} }}";

        return new JournalEvent(Now, kind, JsonDocument.Parse(text).RootElement);
    }

    private static ScrollViewer Named(PanelView panel, string name) =>
        panel.GetVisualDescendants().OfType<ScrollViewer>().First(scroller => scroller.Name == name);

    private static CheckBox Goals(PanelView panel) =>
        panel.GetVisualDescendants()
            .OfType<CheckBox>()
            .First(box => box.Content is string label && label.StartsWith("Goals", StringComparison.Ordinal));

    private static void Toggle(PanelView panel)
    {
        var goals = Goals(panel);

        goals.IsChecked = goals.IsChecked != true;
        Dispatcher.UIThread.RunJobs();
    }

    private static bool Shown(PanelView panel, string text) =>
        panel.GetVisualDescendants()
            .OfType<TextBlock>()
            .Any(block => block.Text == text && block.IsEffectivelyVisible);

    [AvaloniaFact]
    public void TickingGoalsLeavesOnlyTheGoalsCheckboxAndTheGoals()
    {
        var (panel, _) = Open();

        // The list of lists, whose row for the Commander's notes is drawn.
        Assert.True(Shown(panel, "YOUR NOTES"));

        Toggle(panel);

        Assert.True(Goals(panel).IsEffectivelyVisible);
        Assert.True(Named(panel, "GoalsView").IsEffectivelyVisible);
        Assert.False(Named(panel, "ChecklistView").IsEffectivelyVisible);
        Assert.False(Shown(panel, "YOUR NOTES"));
        Assert.False(Shown(panel, "Delete completed"));
        Assert.DoesNotContain(
            panel.GetVisualDescendants().OfType<Control>(),
            control => control.IsEffectivelyVisible
                       && AutomationProperties.GetName(control) is "Checklist scope" or "Add a line");
    }

    [AvaloniaFact]
    public void UntickingRestoresTheListWithItsQuery()
    {
        var (panel, checklists) = Open();

        checklists.Search("limpets");
        Dispatcher.UIThread.RunJobs();

        Toggle(panel);
        Toggle(panel);

        Assert.Equal("limpets", checklists.Query);
        Assert.True(Named(panel, "ChecklistView").IsEffectivelyVisible);
        Assert.False(Named(panel, "GoalsView").IsEffectivelyVisible);
        Assert.True(Shown(panel, "Buy limpets"));
        Assert.False(Shown(panel, "Get a new paint job"));
    }

    [AvaloniaFact]
    public void ATallWindowShowsEveryGoalWithoutScrolling()
    {
        var (panel, _) = Open(height: 1600);

        Toggle(panel);

        var view = Named(panel, "GoalsView");

        Assert.True(
            view.Extent.Height <= view.Viewport.Height,
            $"the goals were {view.Extent.Height} pixels in a {view.Viewport.Height} pixel window");
    }

    [AvaloniaFact]
    public void AShortWindowScrollsTheGoalsDownToThePageFoot()
    {
        var (panel, _) = Open(height: 360);

        Toggle(panel);

        var view = Named(panel, "GoalsView");
        var page = view.FindAncestorOfType<ChecklistPage>()!;
        var bottom = view.TranslatePoint(new Point(0, view.Bounds.Height), page)!.Value.Y;

        Assert.True(view.Extent.Height > view.Viewport.Height, "the goals fit, so this proves nothing about scrolling");
        Assert.True(bottom >= page.Bounds.Height - 15, $"the goals stop {page.Bounds.Height - bottom} pixels above the page's foot");
    }

    private static Border Card(PanelView panel, string name) =>
        panel.GetVisualDescendants()
            .OfType<Border>()
            .First(border => AutomationProperties.GetName(border) == name);

    [AvaloniaFact]
    public void EliteIVAtFiftySixPercentDrawsOneTrackAndNoSecondBar()
    {
        var (panel, _) = Open();

        Toggle(panel);

        var texts = Card(panel, "Elite V in Trade").GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();

        Assert.Contains("ELITE IV · 56%", texts);
        Assert.Contains("96%", texts);
        Assert.DoesNotContain("TO ELITE V", texts);
        Assert.Single(texts, text => text is not null && text.StartsWith("TO ELITE V", StringComparison.Ordinal));
        Assert.Contains(texts, text => text is not null && text.StartsWith("TO ELITE ·", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void AnOpenGoalFillsOnlyItsHeadWithA()
    {
        using var look = AppLook.Put();

        var (panel, _) = Open(height: 1200);

        Toggle(panel);

        var head = Card(panel, "Elite V in Trade");
        var center = head.TranslatePoint(new Point(head.Bounds.Width / 2, head.Bounds.Height / 2), _window!)!.Value;

        _window!.MouseDown(center, Avalonia.Input.MouseButton.Left);
        _window!.MouseUp(center, Avalonia.Input.MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        var open = panel.GetVisualDescendants().OfType<StackPanel>()
            .First(stack => AutomationProperties.GetName(stack) == "Elite V in Trade");

        var filled = open.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Classes.Contains("d47-row") && border.Classes.Contains("selected"))
            .ToList();

        Assert.Single(filled);
        Assert.InRange(filled[0].Bounds.Height, 50, 60);

        var texts = open.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();

        Assert.Contains("NEXT RANK", texts);
        Assert.Contains("Elite V", texts);
        Assert.Contains("RANKS TO GO", texts);
        Assert.Equal(12, open.GetVisualDescendants().OfType<TextBlock>().Count(text => text.Text == "✓"));

        _window!.CaptureRenderedFrame()!.SaveCapture("goals-mode-open.png");
    }

    [AvaloniaTheory]
    [InlineData(640, "goals-mode.png")]
    [InlineData(360, "goals-mode-short.png")]
    public void GoalsModeRendersToACapture(double height, string file)
    {
        using var look = AppLook.Put();

        var (panel, _) = Open(height);

        Toggle(panel);

        _window!.CaptureRenderedFrame()!.SaveCapture(file);
    }
}
