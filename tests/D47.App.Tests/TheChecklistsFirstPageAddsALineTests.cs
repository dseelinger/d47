using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Checklist's first page offers ADD A LINE, so a Commander with no lines can start a list (#1815).</summary>
public class TheChecklistsFirstPageAddsALineTests
{
    private const string Unfiltered =
        "Nothing here yet. Add a line, or say \"add buy limpets to my checklist\", or ask for a build plan, "
        + "and D47 will propose it.";

    private static ChecklistService Checklists()
    {
        var paths = new AppPaths(TestSurface.MemoryFolder("d47-checklist-first-page-add"));

        return new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);
    }

    private static (Window Window, PanelView Panel) Open(ChecklistService checklists)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 1280, Height = 900 };
        window.Show();

        panel.Tab = PanelTab.Commander;
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static bool AddShown(PanelView panel)
    {
        Dispatcher.UIThread.RunJobs();

        return panel.GetVisualDescendants().OfType<Control>()
            .Any(control => control.IsEffectivelyVisible && AutomationProperties.GetName(control) == "Add a line");
    }

    private static IReadOnlyList<string> Words(PanelView panel)
    {
        Dispatcher.UIThread.RunJobs();

        return
        [
            .. panel.GetVisualDescendants().OfType<TextBlock>()
                .Where(block => block.IsEffectivelyVisible)
                .Select(block => block.Text ?? string.Empty)
                .Where(text => text.Length > 0),
        ];
    }

    [AvaloniaFact]
    public void TheFirstPageShowsAddALineWithNoLinesAtAll()
    {
        var (window, panel) = Open(Checklists());

        Assert.True(AddShown(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void TheFirstPageShowsAddALineWithLinesOnTheChecklist()
    {
        var checklists = Checklists();
        checklists.AddNote(ChecklistScope.Universal, "Buy limpets");

        var (window, panel) = Open(checklists);

        Assert.True(AddShown(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void ALineWithNoListOpenGoesToYourNotes()
    {
        var checklists = Checklists();

        var scope = checklists.ScopeFor(null, null);
        checklists.AddTyped(scope, "Buy limpets");

        Assert.Equal(ChecklistScope.Universal, scope);

        var line = Assert.Single(checklists.Document.Items);
        Assert.Equal(ChecklistItemKind.Authored, line.Kind);
        Assert.Equal(ChecklistScope.Universal, line.Scope);

        var (window, panel) = Open(checklists);

        Assert.Contains("Your notes", Words(panel).Select(word => word.TrimEnd()).Concat(RowNames(panel)));

        window.Close();
    }

    private static IEnumerable<string> RowNames(PanelView panel) =>
        panel.GetVisualDescendants().OfType<Button>()
            .Select(button => AutomationProperties.GetName(button) ?? string.Empty)
            .Select(name => name.Split(',')[0]);

    [AvaloniaFact]
    public void AnEmptyFirstPageIgnoresASavedFilter()
    {
        var checklists = Checklists();
        checklists.Choose("authored");

        var (window, panel) = Open(checklists);

        Assert.True(AddShown(panel));
        Assert.Contains(Unfiltered, Words(panel));
        Assert.DoesNotContain(Words(panel), word => word.Contains("authored", StringComparison.Ordinal));

        window.Close();
    }

    [AvaloniaFact]
    public void AnAbsentFilterIsNamedInWordsOnAllLists()
    {
        var checklists = Checklists();
        checklists.Choose("authored");

        var (window, panel) = Open(checklists);

        panel.Nav.Drill(ChecklistPage.AllLists);
        Dispatcher.UIThread.RunJobs();

        var all = Words(panel);

        Assert.Contains("Nothing on your list is in Written down — you say when.", all);

        var stepper = panel.GetVisualDescendants()
            .OfType<D47.App.Controls.Stepper>()
            .Single(found => found.Name == "ChecklistScope");

        Assert.Equal("Written down — you say when", stepper.SelectedItem);
        Assert.False(AddShown(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void MiniHidesAddALineOnTheFirstPage()
    {
        var (window, panel) = Open(Checklists());

        panel.Mode = PanelMode.Mini;

        Assert.False(AddShown(panel));

        window.Close();
    }
}
