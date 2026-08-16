using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.Core;
using D47.Core.Checklists;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The one surface (list.md Phase 17). The card carries a one-line summary; this carries the list,
/// and the distinction it has to make visible is the one the whole phase turns on — some ticks are
/// computed and some are opinions, and a Commander must be able to tell which at a glance.
/// </summary>
public class ChecklistWindowTests
{
    private static ChecklistService Checklists(string root)
    {
        var paths = new AppPaths(root);
        paths.EnsureCreated();

        return new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);
    }

    private static ChecklistItem Derived(bool done = false)
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines")
        {
            Detail = "Engine_Dirty",
            Grade = 5,
        };

        return new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = ChecklistScope.Ship(12),
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineeringPlan,
            Text = "Grade 5 dirty drives",
            Intent = intent,
            State = done ? ChecklistState.Done : ChecklistState.Open,
        };
    }

    [AvaloniaFact]
    public void ADerivedItemHasNoCheckboxAtAll()
    {
        var root = TempFolders.Create("d47-checklist-tests");
        var checklists = Checklists(root);

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.List.Save(
        [
            checklists.Document with { Items = [.. checklists.Document.Items, Derived()] },
        ]);

        var window = new ChecklistWindow(checklists);
        window.Show();

        var ticks = window.GetVisualDescendants().OfType<CheckBox>().ToList();

        // One authored item and one derived one, and exactly one checkbox. Not a disabled tick:
        // a greyed-out control still asserts that ticking is the mechanism here, and it is not.
        Assert.Single(ticks);
        Assert.Equal("buy limpets", ticks[0].Content);

        window.Close();
    }

    [AvaloniaFact]
    public void FinishedItemsSitBelowTheLineWithTheirCountShowing()
    {
        var root = TempFolders.Create("d47-checklist-tests");
        var checklists = Checklists(root);

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        var done = checklists.Document.Items[0].Id;
        checklists.Complete(done);
        checklists.AddNote(ChecklistScope.Universal, "fit a fuel scoop");

        var window = new ChecklistWindow(checklists);
        window.Show();

        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToList();

        // Kept, counted, and out of the way. Forty finished items must not bury the six still open.
        Assert.Contains("Done (1)", text);

        window.Close();
    }

    [AvaloniaFact]
    public void AProposalOffersAcceptAndDeclineAndSaysItCannotActItself()
    {
        var root = TempFolders.Create("d47-checklist-tests");
        var checklists = Checklists(root);

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);

        var window = new ChecklistWindow(checklists);
        window.Show();

        var buttons = window.GetVisualDescendants().OfType<Button>()
            .Select(button => button.Content as string ?? string.Empty)
            .ToList();

        Assert.Contains("Accept", buttons);
        Assert.Contains("Decline", buttons);

        // Nothing has moved yet — the panel is where the Commander decides, and the AI cannot.
        Assert.Empty(checklists.Document.Items);

        window.Close();
    }

    [AvaloniaFact]
    public void AcceptingFromThePanelCommitsIt()
    {
        var root = TempFolders.Create("d47-checklist-tests");
        var checklists = Checklists(root);

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);

        var window = new ChecklistWindow(checklists);
        window.Show();

        var accept = window.GetVisualDescendants().OfType<Button>()
            .First(button => button.Content as string == "Accept");

        accept.Command?.Execute(null);
        accept.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal("buy limpets", checklists.Document.Items.Single().Text);
        Assert.Empty(checklists.Proposals.Pending);

        window.Close();
    }

    [AvaloniaFact]
    public void ThePanelFollowsAChangeMadeFromSomewhereElse()
    {
        var root = TempFolders.Create("d47-checklist-tests");
        var checklists = Checklists(root);

        var window = new ChecklistWindow(checklists);
        window.Show();

        // A voice command, or a text editor. The panel is a view of the file rather than a second
        // copy of it, so it has to follow whoever wrote.
        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            window.GetVisualDescendants().OfType<CheckBox>(),
            tick => tick.Content as string == "buy limpets");

        window.Close();
    }
}
