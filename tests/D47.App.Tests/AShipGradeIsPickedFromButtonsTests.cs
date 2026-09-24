using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// On a hull with no layout, the grade is a row of 1 to 5 and Any, where Any plans any grade (#434).
/// </summary>
public class AShipGradeIsPickedFromButtonsTests
{
    private static (PanelView Panel, ShipsMode Mode, ShipPlanService Ships, string Item) Open()
    {
        var root = TempFolders.Create("d47-ship-grade-tests");

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => null);

        // A hull the shipped table has no layout for.
        ships.BuildFor(14, "type12_prototype", "Scout");

        var mode = new ShipsMode(ships, checklists, () => null);
        var panel = new PanelView { DataContext = new PanelViewModel() };

        new Window { Content = panel, Width = 900, Height = 700 }.Show();

        return (panel, mode, ships, mode.Items().Single().Key);
    }

    private static List<Button> Grades(PanelView panel) =>
        [
            .. panel.GetVisualDescendants().OfType<Control>()
                .First(control => control.GetType().Name == "ButtonPage")
                .GetVisualDescendants().OfType<Button>()
                .Where(button => button.Content is string label && label != "Back"),
        ];

    private static int? Grade(ShipPlanService ships) =>
        ships.Store.Builds.Single().For("Slot01")?.Grade;

    [AvaloniaFact]
    public void OneToFiveAndAnyAreOfferedAndAnyPlansAnyGrade()
    {
        var (panel, mode, ships, item) = Open();

        mode.Ask(item, "Slot01", panel.Prompts, () => { });
        Dispatcher.UIThread.RunJobs();

        panel.Prompts.Hear(new Heard("Overcharged", 1, Final: true));
        Dispatcher.UIThread.RunJobs();

        var grades = Grades(panel);

        Assert.Equal(["1", "2", "3", "4", "5", "Any"], grades.Select(button => (string)button.Content!));
        Assert.DoesNotContain(grades, button => button.BorderThickness.Top == 2);

        grades.Last().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, Grade(ships));
    }

    [AvaloniaFact]
    public void SayingGradeFourPlansFour()
    {
        var (panel, mode, ships, item) = Open();

        mode.Ask(item, "Slot01", panel.Prompts, () => { });
        Dispatcher.UIThread.RunJobs();

        panel.Prompts.Hear(new Heard("Overcharged", 1, Final: true));
        Dispatcher.UIThread.RunJobs();

        panel.Prompts.Hear(new Heard("grade four", 1, Final: true));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(4, Grade(ships));
    }
}
