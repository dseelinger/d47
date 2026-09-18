using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Checklists;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A checklist proposal shows as a card in the conversation, in place of the bare sentence it used to
/// be the only trace of — Accept and Decline settle it from either page, or by voice (#277).
/// </summary>
public class AProposalIsACardInTheConversationTests
{
    /// <summary>The two events AppHost wires between the checklist and the transcript (#277).</summary>
    private static ChecklistService Wired(PanelViewModel model)
    {
        var paths = new AppPaths(TempFolders.Create("d47-proposal-card-tests"));
        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"), NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        checklists.Proposals.Added += proposal => model.AppendProposal(proposal.Id, proposal.Summary);
        checklists.ProposalSettled += (id, accepted, outcome) => model.SettleProposal(id, accepted, outcome);

        return checklists;
    }

    private static (PanelView Panel, PanelViewModel Model, ChecklistService Checklists) Open()
    {
        var model = new PanelViewModel();
        var checklists = Wired(model);

        var panel = new PanelView { DataContext = model };
        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        var bounds = new Rect(0, 0, 900, 700);
        window.Measure(bounds.Size);
        window.Arrange(bounds);
        Dispatcher.UIThread.RunJobs();

        return (panel, model, checklists);
    }

    private static IReadOnlyList<Button> Buttons(PanelView panel) =>
        [.. panel.GetControl<StackPanel>("Bubbles").GetVisualDescendants().OfType<Button>()];

    [AvaloniaFact]
    public void RaisingAProposalAddsOneCardWithWorkingAcceptAndDecline()
    {
        var (panel, _, checklists) = Open();

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);
        Dispatcher.UIThread.RunJobs();

        var buttons = Buttons(panel);

        Assert.Equal(["Accept", "Decline"], buttons.Select(button => button.Content?.ToString()));
    }

    [AvaloniaFact]
    public void PressingAcceptInTheThreadRemovesTheProposalFromTheChecklistPage()
    {
        var (panel, _, checklists) = Open();

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);
        Dispatcher.UIThread.RunJobs();

        var accept = Buttons(panel).Single(button => Equals(button.Content?.ToString(), "Accept"));
        accept.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(checklists.Proposals.Pending);
        Assert.Empty(Buttons(panel));
    }

    [AvaloniaFact]
    public void DecliningOnTheChecklistPageSettlesTheThreadCardToo()
    {
        var (panel, _, checklists) = Open();

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);
        Dispatcher.UIThread.RunJobs();

        // The reverse direction from the button test above: settled from the checklist side rather than
        // the thread's own buttons.
        checklists.Decline();
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(Buttons(panel));
    }

    [AvaloniaFact]
    public void SayingAcceptTheProposalSettlesTheThreadCard()
    {
        var (panel, _, checklists) = Open();

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);
        Dispatcher.UIThread.RunJobs();

        // What the keyword router calls for the phrase "accept the proposal": no id, meaning everything
        // waiting.
        checklists.Accept();
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(Buttons(panel));
    }

    [AvaloniaFact]
    public void AnOldSettledProposalShowsNoButtonsOnceALaterTurnForcesARedraw()
    {
        var (panel, model, checklists) = Open();

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);
        Dispatcher.UIThread.RunJobs();

        checklists.Accept();
        Dispatcher.UIThread.RunJobs();

        // Something else lands afterwards — the append the fast-redraw path exists for, which must not
        // leave a stale button on the card behind it.
        model.Append("\nWelcome back.\n");
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(Buttons(panel));
    }
}
