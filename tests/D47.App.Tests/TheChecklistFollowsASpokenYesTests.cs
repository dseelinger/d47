using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The page follows a change it did not make (remediation.md 11, item 3).
/// <para>
/// Reported with a picture: "Yes" was heard, D47 answered "Added 'Run to the supermarket' to the
/// custom list", and the panel went on showing the proposal card, the stale count on the
/// Suggestions button, and a list without the new line on it.
/// </para>
/// <para>
/// Accepting from the page's own buttons refreshed it, which is why this was invisible until a
/// spoken yes did the same thing from somewhere else.
/// </para>
/// </summary>
public class TheChecklistFollowsASpokenYesTests
{
    private static (PanelView Panel, ChecklistService Checklists) Waiting()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-spoken-yes-tests"));

        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        checklists.AddNote(ChecklistScope.Universal, "Get a new paint job");
        checklists.ProposeAdd(ChecklistScope.Universal, ["Run to the supermarket"]);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 1600, Height = 700 };

        window.Show();
        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        return (panel, checklists);
    }

    private static string Written(PanelView panel) =>
        string.Join(
            " | ",
            panel.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(block => block.Text)
                .Concat(panel.GetVisualDescendants().OfType<CheckBox>().Select(tick => tick.Content as string))
                .Concat(panel.GetVisualDescendants().OfType<Button>().Select(button => button.Content as string))
                .Where(text => !string.IsNullOrWhiteSpace(text)));

    /// <summary>
    /// The whole report, from the Suggestions page the Commander was looking at.
    /// </summary>
    [AvaloniaFact]
    public void ASpokenYesClearsTheCardAndPutsTheLineOnTheList()
    {
        var (panel, checklists) = Waiting();

        panel.Nav.Drill(new NavCrumb(ChecklistPage.SuggestionsKey, "Suggestions"));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Run to the supermarket", Written(panel), StringComparison.Ordinal);

        // What a spoken yes does: the committing half, from somewhere that is not this page.
        checklists.Accept();
        Dispatcher.UIThread.RunJobs();

        var shown = Written(panel);

        Assert.Contains("Nothing waiting", shown, StringComparison.Ordinal);
        Assert.DoesNotContain("cannot accept it itself", shown, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the count on the button that opened it, which was still offering one.
    /// </summary>
    [AvaloniaFact]
    public void TheSuggestionsCountFollowsTooo()
    {
        var (panel, checklists) = Waiting();

        panel.Nav.Drill(new NavCrumb(ChecklistPage.SuggestionsKey, "Suggestions"));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Suggestions (1)", Written(panel), StringComparison.Ordinal);

        checklists.Accept();
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("Suggestions (1)", Written(panel), StringComparison.Ordinal);
    }

    /// <summary>
    /// And the line itself is on the list, which is the thing D47 said out loud that it had done.
    /// </summary>
    [AvaloniaFact]
    public void TheAcceptedLineAppearsOnTheList()
    {
        var (panel, checklists) = Waiting();

        panel.Nav.Drill(new NavCrumb(ChecklistPage.SuggestionsKey, "Suggestions"));
        Dispatcher.UIThread.RunJobs();

        checklists.Accept();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            panel.GetVisualDescendants().OfType<CheckBox>(),
            tick => (tick.Content as string) == "Run to the supermarket");
    }

    /// <summary>
    /// The subscription has to survive the page being reparented. Drilling reflows the tab into
    /// two panes, which detaches the page and attaches it again — and a page that unsubscribed on
    /// the way out and never came back is deaf for the rest of the session.
    /// </summary>
    [AvaloniaFact]
    public void ThePageStillListensAfterDrillingAndComingBack()
    {
        var (panel, checklists) = Waiting();

        panel.Nav.Drill(new NavCrumb(ChecklistPage.SuggestionsKey, "Suggestions"));
        Dispatcher.UIThread.RunJobs();

        panel.Nav.Back();
        Dispatcher.UIThread.RunJobs();

        checklists.AddNote(ChecklistScope.Universal, "Fit a fuel scoop");
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            panel.GetVisualDescendants().OfType<CheckBox>(),
            tick => (tick.Content as string) == "Fit a fuel scoop");
    }
}
