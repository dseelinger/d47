using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Knowledge;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>"Which ship do you intend to buy?" is picked from every hull there is.</summary>
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
    private static (PanelView Panel, ShipPlanService Ships, ListBox Pick) Asking()
    {
        var (panel, ships) = Fleet();

        var intend = panel.GetVisualDescendants()
            .OfType<Button>()
            .First(button => (button.Content as string) == "Plan a ship you do not own");

        intend.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        return (
            panel,
            ships,
            panel.GetVisualDescendants()
                .OfType<ListBox>()
                .Single(box => AutomationProperties.GetName(box) == "Ship"));
    }

    private static List<string> Offered(ListBox pick) => pick.ItemsSource!.Cast<string>().ToList();

    private static Button Commit(PanelView panel) =>
        panel.GetVisualDescendants().OfType<Button>().Single(button => (button.Content as string) == "Plan this hull");

    private static TextBox Filter(PanelView panel) =>
        panel.GetVisualDescendants().OfType<TextBox>().Single(box => AutomationProperties.GetName(box) == "Filter");

    /// <summary>Every hull is offered, and the list is exactly what the validation accepts.</summary>
    [AvaloniaFact]
    public void EveryHullIsOfferedRatherThanWaitingToBeSpelled()
    {
        var (_, _, pick) = Asking();

        var offered = Offered(pick);

        Assert.Contains("Anaconda", offered);
        Assert.Contains("Sidewinder", offered);
        Assert.True(offered.Count > 20, $"only {offered.Count} hulls were offered");

        // The promise the list makes about Validate: nothing is offered that would be refused.
        Assert.All(offered, hull => Assert.NotNull(EliteSpecifications.Ship(hull)));
    }

    /// <summary>Nothing is committed by the page merely opening.</summary>
    [AvaloniaFact]
    public void TheQuestionIsNotAnsweredBeforeItIsAsked()
    {
        var (_, ships, pick) = Asking();

        Assert.Null(pick.SelectedItem);
        Assert.Empty(ships.Store.Builds);
    }

    /// <summary>Walking the highlight down the list passes every hull on the way, and plans none of them.</summary>
    [AvaloniaFact]
    public void MovingTheHighlightPlansNothing()
    {
        var (panel, ships, pick) = Asking();

        foreach (var hull in Offered(pick).Take(10))
        {
            pick.SelectedItem = hull;
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Empty(ships.Store.Builds);
        Assert.True(panel.Nav.Modal);
    }

    /// <summary>The button that says what it will do is the one thing that plans the highlighted hull.</summary>
    [AvaloniaFact]
    public void PressingPlanThisHullPlansTheHighlightedOne()
    {
        var (panel, ships, pick) = Asking();

        Assert.False(Commit(panel).IsEnabled, "There is nothing highlighted to plan yet.");

        pick.SelectedItem = "Anaconda";
        Dispatcher.UIThread.RunJobs();

        Commit(panel).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var planned = ships.Store.Builds.Single();

        Assert.False(planned.IsOwned);
        Assert.Equal("Anaconda", planned.HullName);

        // And the question is gone: answering it is what closed it.
        Assert.False(panel.Nav.Modal);
    }

    /// <summary>Typing narrows the list rather than replacing it, and when one hull is left, Enter takes it.</summary>
    [AvaloniaFact]
    public void TypingNarrowsTheList()
    {
        var (panel, ships, pick) = Asking();

        Filter(panel).Text = "anac";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Anaconda"], Offered(pick));
        Assert.Equal("Anaconda", pick.SelectedItem);
        Assert.Empty(ships.Store.Builds);

        Filter(panel).Text = string.Empty;
        Dispatcher.UIThread.RunJobs();

        Assert.True(Offered(pick).Count > 20, "Clearing the filter brings every hull back.");
        Assert.Equal("Anaconda", pick.SelectedItem);
    }

    /// <summary>The two controls a free-text prompt needs and a picker does not.</summary>
    [AvaloniaFact]
    public void NeitherTheDrawnKeyboardNorDoneIsOnThePage()
    {
        var (panel, _, _) = Asking();

        var labels = panel.GetVisualDescendants()
            .OfType<Button>()
            .Select(button => button.Content as string)
            .ToList();

        Assert.DoesNotContain("Done", labels);
        Assert.DoesNotContain("Type it instead", labels);
        Assert.DoesNotContain("Say it instead", labels);
    }

    /// <summary>Voice still answers it.</summary>
    [AvaloniaFact]
    public void ASpokenHullStillAnswersIt()
    {
        var (panel, ships, _) = Asking();

        Assert.True(panel.Prompts.IsListening);

        panel.Prompts.Hear(new Heard("Krait MkII", 1, Final: true));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Krait MkII", ships.Store.Builds.Single().HullName);
    }

    /// <summary>A hull that is not a hull says so and leaves the picker up.</summary>
    [AvaloniaFact]
    public void SomethingThatIsNotAHullIsRefusedWithoutClosingTheQuestion()
    {
        var (panel, ships, _) = Asking();

        panel.Prompts.Hear(new Heard("Millennium Falcon", 1, Final: true));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(ships.Store.Builds);
        Assert.True(panel.Nav.Modal);

        Assert.Contains(
            panel.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text?.Contains("Millennium Falcon", StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// The page does not claim the hull is absent from the fleet, because a Commander who already flies
    /// a Python and plans a second one would be reading something untrue.
    /// </summary>
    [AvaloniaFact]
    public void ThePageClaimsNothingAboutWhatIsAlreadyOwned()
    {
        var (panel, _, _) = Asking();

        Assert.DoesNotContain(
            panel.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text?.Contains("not in your fleet", StringComparison.OrdinalIgnoreCase) == true);
    }
}
