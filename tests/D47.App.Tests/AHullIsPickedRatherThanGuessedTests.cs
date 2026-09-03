using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Knowledge;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// "Which ship do you intend to buy?" offers the hulls rather than a blind box (#282).
/// <para>
/// The hulls are a closed set and d47 holds all of them, so the free-text box could only ever
/// tell a Commander at a mouse that what they typed was not a ship — after the fact, and without
/// saying what would have been. The voice path is untouched: what opens is still listening, and
/// the list is what is under it.
/// </para>
/// </summary>
public class AHullIsPickedRatherThanGuessedTests
{
    private static (PanelView Panel, ShipPlanService Ships) Fleet()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-hull-picker-tests"));

        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => null);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, () => null, null);

        var window = new Window { Content = panel, Width = 1400, Height = 900 };

        window.Show();
        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        return (panel, ships);
    }

    /// <summary>Opens the question the way a Commander does: by pressing the button that asks it.</summary>
    private static PanelView Asking()
    {
        var (panel, _) = Fleet();

        Press(panel, "Plan a ship you do not own");

        return panel;
    }

    private static void Press(PanelView panel, string label)
    {
        var button = panel.GetVisualDescendants()
            .OfType<Button>()
            .First(candidate => Label(candidate) == label);

        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>What a button says, whether that is a string or the row's own text block.</summary>
    private static string? Label(Button button) => button.Content switch
    {
        string said => said,
        TextBlock block => block.Text,
        _ => null,
    };

    private static IReadOnlyList<string> Offered(PanelView panel) =>
    [
        .. panel.GetVisualDescendants()
            .OfType<Button>()
            .Select(Label)
            .OfType<string>()
            .Where(said => EliteSpecifications.Ship(said)?.Name == said),
    ];

    [AvaloniaFact]
    public void TheHullsAreOnScreenRatherThanWaitingToBeSpelled()
    {
        var offered = Offered(Asking());

        Assert.Contains("Anaconda", offered);
        Assert.Contains("Sidewinder", offered);

        // The whole table, not a handful of it: the list is what the validation accepts.
        Assert.True(
            offered.Count > 20,
            $"only {offered.Count} hulls were offered");
    }

    /// <summary>
    /// Typing narrows the list rather than resolving it. Nothing is refused on the way — what is
    /// typed stays in the box, and the rows under it are what still match.
    /// </summary>
    [AvaloniaFact]
    public void TypingNarrowsTheListRatherThanRefusingWhatIsTyped()
    {
        var panel = Asking();

        // The prompt's own box rather than the panel's search row, which is still in the tree
        // behind the question.
        var box = panel.GetVisualDescendants()
            .OfType<TextBox>()
            .First(candidate => candidate.FontSize == TypeScale.Heading);

        box.Text = "anacon";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Anaconda"], Offered(panel));
        Assert.Equal("anacon", box.Text);
    }

    /// <summary>
    /// A pressed hull is the answer, and it goes through the same validation a spoken one does —
    /// so the plan is pointed at the ship without a word being typed.
    /// </summary>
    [AvaloniaFact]
    public void PressingAHullPlansIt()
    {
        var (panel, ships) = Fleet();

        Press(panel, "Plan a ship you do not own");
        Press(panel, "Anaconda");

        var planned = ships.Store.Builds.Single();

        Assert.False(planned.IsOwned);
        Assert.Equal("Anaconda", planned.HullName);

        // And the question is gone: answering it is what closed it.
        Assert.False(panel.Nav.Modal);
    }
}
