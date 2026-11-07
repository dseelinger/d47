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

/// <summary>The goals are a section of Commander › Progress, and the Checklist root holds only the lists.</summary>
public class TickingGoalsSwapsTheListForTheGoalsTests
{
    private static readonly DateTimeOffset Now = new(3311, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static Window? _window;

    private static (PanelView Panel, ChecklistService Checklists, GoalBook Goals) Open(double height = 640, double width = 820)
    {
        var paths = new D47.Core.AppPaths(TestSurface.MemoryFolder("d47-goals-mode-tests"));


        var checklists = TestSurface.Checklists(paths.Data);

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
        panel.EnableStanding(() => state, goals);

        var window = _window = new Window { Content = panel, Width = width, Height = height };

        window.Show();
        panel.Tab = PanelTab.Commander;
        Dispatcher.UIThread.RunJobs();

        return (panel, checklists, goals);
    }

    private static JournalEvent Event(string kind, string fields)
    {
        var text = $"{{ \"timestamp\":\"{Now.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}\", \"event\":\"{kind}\", {fields} }}";

        return new JournalEvent(Now, kind, JsonDocument.Parse(text).RootElement);
    }

    private static ScrollViewer Named(PanelView panel, string name) =>
        panel.GetVisualDescendants().OfType<ScrollViewer>().First(scroller => scroller.Name == name);

    private static bool HasGoalsBox(PanelView panel) =>
        panel.GetVisualDescendants()
            .OfType<CheckBox>()
            .Any(box => box.Content is string label && label.StartsWith("Goals", StringComparison.Ordinal));

    private static void ToProgress(PanelView panel)
    {
        Assert.True(panel.Nav.SelectRoot(PanelTab.Commander, StandingPage.RootKey));
        Dispatcher.UIThread.RunJobs();
    }

    private static bool Shown(PanelView panel, string text) =>
        panel.GetVisualDescendants()
            .OfType<TextBlock>()
            .Any(block => block.Text == text && block.IsEffectivelyVisible);

    private static bool ShownStarting(PanelView panel, string text) =>
        panel.GetVisualDescendants()
            .OfType<TextBlock>()
            .Any(block => block.Text is { } shown && shown.StartsWith(text, StringComparison.OrdinalIgnoreCase) && block.IsEffectivelyVisible);

    [AvaloniaFact]
    public void TheChecklistRootHasNoGoalsBoxAndNoGoalsView()
    {
        var (panel, _, _) = Open();

        Assert.True(Shown(panel, "YOUR NOTES"));
        Assert.False(HasGoalsBox(panel));
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<ScrollViewer>(), scroller => scroller.Name == "GoalsView");
        Assert.True(Named(panel, "ChecklistView").IsEffectivelyVisible);
        Assert.False(ShownStarting(panel, "Goals ("));
    }

    [AvaloniaFact]
    public void ProgressDrawsTheGoalsAndNoChecklistChrome()
    {
        var (panel, _, _) = Open();

        ToProgress(panel);

        Assert.True(ShownStarting(panel, "Goals ("));
        Assert.False(HasGoalsBox(panel));
        Assert.False(Shown(panel, "YOUR NOTES"));
        Assert.False(Shown(panel, "Delete completed"));
        Assert.DoesNotContain(
            panel.GetVisualDescendants().OfType<Control>(),
            control => control.IsEffectivelyVisible
                       && AutomationProperties.GetName(control) is "Checklist scope" or "Add a line");
    }

    [AvaloniaFact]
    public void ReturningToTheChecklistFromProgressDrawsEveryList()
    {
        var (panel, _, _) = Open();

        ToProgress(panel);
        Assert.True(panel.Nav.SelectRoot(PanelTab.Commander, "checklist"));
        Dispatcher.UIThread.RunJobs();

        Assert.True(Named(panel, "ChecklistView").IsEffectivelyVisible);
        Assert.True(Shown(panel, "YOUR NOTES"));
        Assert.False(ShownStarting(panel, "Goals ("));
    }

    private static ScrollViewer ProgressScroller(PanelView panel) =>
        panel.GetVisualDescendants()
            .OfType<TextBlock>()
            .First(block => block.Text is { } text && text.StartsWith("Goals (", StringComparison.OrdinalIgnoreCase))
            .FindAncestorOfType<ScrollViewer>()!;

    [AvaloniaFact]
    public void AShortWindowScrollsProgressDownToTheLastGoal()
    {
        var (panel, _, goals) = Open(height: 360);

        ToProgress(panel);

        var view = ProgressScroller(panel);

        Assert.True(view.Extent.Height > view.Viewport.Height, "the page fits, so this proves nothing about scrolling");

        view.ScrollToEnd();
        Dispatcher.UIThread.RunJobs();

        var last = Card(panel, goals.Standings[^1].Arc.Name);
        var bottom = last.TranslatePoint(new Point(0, last.Bounds.Height), view)!.Value.Y;

        Assert.True(bottom <= view.Viewport.Height + 1, $"the last goal ends {bottom - view.Viewport.Height} pixels below the window");
    }

    private static Border Card(PanelView panel, string name) =>
        panel.GetVisualDescendants()
            .OfType<Border>()
            .First(border => AutomationProperties.GetName(border) == name);

    [AvaloniaFact]
    public void EliteIVAtFiftySixPercentDrawsOneTrackAndNoSecondBar()
    {
        var (panel, _, _) = Open();

        ToProgress(panel);

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

        var (panel, _, _) = Open(height: 1200);

        ToProgress(panel);

        var head = Card(panel, "Elite V in Trade");
        head.BringIntoView();
        Dispatcher.UIThread.RunJobs();
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
        Assert.DoesNotContain("✓", texts);

        _window!.CaptureRenderedFrame()!.SaveCapture("goals-mode-open.png");
    }

    [AvaloniaTheory]
    [InlineData(640, "goals-mode.png")]
    [InlineData(360, "goals-mode-short.png")]
    public void GoalsModeRendersToACapture(double height, string file)
    {
        using var look = AppLook.Put();

        var (panel, _, _) = Open(height);

        ToProgress(panel);

        _window!.CaptureRenderedFrame()!.SaveCapture(file);
    }
}
