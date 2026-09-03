using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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

/// <summary>
/// "Which ship do you intend to buy?" is picked from every hull there is (#282).
/// <para>
/// The hulls are a closed set and d47 holds all of them, so the free-text box it used to be could
/// only ever tell a Commander that what they typed was not a ship — after the fact, and without
/// saying what would have been. Voice is untouched: it is still armed while the picker is open.
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
    private static (PanelView Panel, ShipPlanService Ships, ComboBox Pick) Asking()
    {
        var (panel, ships) = Fleet();

        var intend = panel.GetVisualDescendants()
            .OfType<Button>()
            .First(button => (button.Content as string) == "Plan a ship you do not own");

        intend.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // The picker's own box rather than the panel's mode box, which is still in the tree.
        return (
            panel,
            ships,
            panel.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(box => box.PlaceholderText == "Pick one, or say it"));
    }

    /// <summary>Every hull is offered, and the list is exactly what the validation accepts.</summary>
    [AvaloniaFact]
    public void EveryHullIsOfferedRatherThanWaitingToBeSpelled()
    {
        var (_, _, pick) = Asking();

        var offered = pick.ItemsSource!.Cast<string>().ToList();

        Assert.Contains("Anaconda", offered);
        Assert.Contains("Sidewinder", offered);
        Assert.True(offered.Count > 20, $"only {offered.Count} hulls were offered");

        // The promise the list makes about Validate: nothing is offered that would be refused.
        Assert.All(offered, hull => Assert.NotNull(EliteSpecifications.Ship(hull)));
    }

    /// <summary>
    /// Nothing is committed by the page merely opening. The question has to be read before it is
    /// answered, so the box starts on no hull at all.
    /// </summary>
    [AvaloniaFact]
    public void TheQuestionIsNotAnsweredBeforeItIsAsked()
    {
        var (_, ships, pick) = Asking();

        Assert.Null(pick.SelectedItem);
        Assert.Empty(ships.Store.Builds);
    }

    /// <summary>Picking a hull is the answer — there is no Done to press after it.</summary>
    [AvaloniaFact]
    public void PickingAHullPlansIt()
    {
        var (panel, ships, pick) = Asking();

        pick.SelectedItem = "Anaconda";
        Dispatcher.UIThread.RunJobs();

        var planned = ships.Store.Builds.Single();

        Assert.False(planned.IsOwned);
        Assert.Equal("Anaconda", planned.HullName);

        // And the question is gone: answering it is what closed it.
        Assert.False(panel.Nav.Modal);
    }

    /// <summary>
    /// The two controls a free-text prompt needs and a picker does not. The drawn keyboard exists
    /// because free text needs one in a cockpit, and Done exists to commit free text once;
    /// neither applies when every answer is already on the page.
    /// </summary>
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

    /// <summary>
    /// Voice still answers it. The picker does not replace speech, it replaces the blind box that
    /// speech had to fall back to.
    /// </summary>
    [AvaloniaFact]
    public void ASpokenHullStillAnswersIt()
    {
        var (panel, ships, _) = Asking();

        Assert.True(panel.Prompts.IsListening);

        panel.Prompts.Hear(new Heard("Krait MkII", 1, Final: true));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Krait MkII", ships.Store.Builds.Single().HullName);
    }

    /// <summary>
    /// A hull that is not a hull says so and leaves the picker up. There is no keyboard to put
    /// back here, so the complaint is the whole of what a failure leaves behind.
    /// </summary>
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
    /// The page does not claim the hull is absent from the fleet, because a Commander who already
    /// flies a Python and plans a second one would be reading something untrue.
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
