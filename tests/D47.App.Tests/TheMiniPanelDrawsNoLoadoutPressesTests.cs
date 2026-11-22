using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Assets pages draw their presses on the full panel and none on the mini one.</summary>
[Trait("Category", "Integration")]
public class TheMiniPanelDrawsNoLoadoutPressesTests
{
    private static readonly string[] Pressed =
    [
        "Plan a ship you do not own", "Plan a suit or weapon you do not own", "Compare", "Plan this slot", "Change the plan", "Clear it",
        "Put this build on my checklist", "Clear this plan",
    ];

    private static (Window Window, PanelView Panel) Open()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"SuitLoadout","SuitID":7,"SuitName":"utilitysuit_class3","LoadoutName":"Ground","SuitMods":[],"Modules":[]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var state = store.Active!;
        var (checklists, _, _, ships, kit, _) = PlanBench.Over(TestSurface.MemoryFolder("d47-mini-loadout-presses"), () => state);
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, () => state, kit);

        var window = new Window { Content = panel, Width = 900, Height = 700 };

        window.Show();
        panel.Nav.Show(PanelTabNames.AssetsRoot);
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static List<string> Presses(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible && button.Content is string label && Pressed.Contains(label))
            .Select(button => (string)button.Content!)];

    private static void Mode(PanelView panel, PanelMode mode)
    {
        panel.Mode = mode;
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheIndexAndTheSuitAndSlotPagesDrawNoPressesOnTheMiniPanel()
    {
        var (window, panel) = Open();

        Assert.True(panel.Nav.Show(OnFootMode.Root));
        Dispatcher.UIThread.RunJobs();
        Assert.NotEmpty(Presses(panel));

        Mode(panel, PanelMode.Mini);
        Assert.Empty(Presses(panel));

        Mode(panel, PanelMode.Full);
        Assert.NotEmpty(Presses(panel));

        var suit = panel.GetVisualDescendants().OfType<Button>()
            .First(button => AutomationProperties.GetName(button) == "Maverick Suit"
                             || button.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Maverick Suit"));

        suit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Mode(panel, PanelMode.Mini);
        Assert.Empty(Presses(panel));

        var grade = panel.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(button => button.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "GRADE"));

        grade?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(Presses(panel));

        Mode(panel, PanelMode.Full);
        Assert.NotEmpty(Presses(panel));

        window.Close();
    }
}
