using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A filterable tab's first visit draws the search row even when the tab was selected before the
/// window attached the panel to a visual tree — a cold start, where the strip's own first draw runs
/// later than the row was decided (#103).
/// </summary>
public class TheSearchBoxSurvivesAColdStartTests
{
    private static bool BoxShown(PanelView panel) => panel.GetControl<DockPanel>("SearchRow").IsVisible;

    [AvaloniaFact]
    public void AFilterableTabDrawsItsSearchOnFirstVisit()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-search-cold-start-tests"));

        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableChecklist(checklists);
        panel.EnableSearch();

        // Selected before the panel is under a shown window, so the strip attaches to the visual tree
        // — and draws for the first time — only once Show() runs below, after this method already
        // asked whether the page filters.
        panel.Tab = PanelTab.Checklist;

        var window = new Window { Content = panel, Width = 1200, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(BoxShown(panel), "the search row stayed hidden on the Checklist tab's first draw");
    }
}
