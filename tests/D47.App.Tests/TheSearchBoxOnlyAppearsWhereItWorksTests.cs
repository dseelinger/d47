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
/// The search box is drawn where a query would do something (remediation.md 11, item 6).
/// <para>
/// Reported from the Ships page: "Reaper" typed into the box, the list unmoved. The box was drawn
/// on every page of a surface that had one, whether or not the page answered it — and a control
/// that does nothing is worse than an absent one, because it costs the time to find out.
/// </para>
/// </summary>
public class TheSearchBoxOnlyAppearsWhereItWorksTests
{
    private static PanelView Furnished()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-search-visibility-tests"));

        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableChecklist(checklists);

        // A tab whose levels do not filter, which is what the Ships page is.
        panel.Furnish(
            PanelTab.Loadout,
            crumb => new TextBlock { Text = crumb.Word },
            new NavCrumb("fleet", "Ships"));

        // A stand-in for the real settings surface, which is ninety-odd rows and does filter.
        // A plain TextBlock here would be asserting that a page nobody made filterable is not
        // offered a search box, which is the other test.
        panel.EnableSettings(() => new Narrowable());
        panel.EnableSearch();

        var window = new Window { Content = panel, Width = 1200, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    private static bool BoxShown(PanelView panel) => panel.GetControl<DockPanel>("SearchRow").IsVisible;

    /// <summary>The transcript highlights and steps rather than filtering, which is still a search.</summary>
    [AvaloniaFact]
    public void TheTranscriptKeepsItsSearch()
    {
        var panel = Furnished();

        panel.Tab = PanelTab.Transcript;
        Dispatcher.UIThread.RunJobs();

        Assert.True(BoxShown(panel));
    }

    /// <summary>A list of checklist lines is narrowable, so the box belongs there.</summary>
    [AvaloniaFact]
    public void AFilterablePageKeepsItsSearch()
    {
        var panel = Furnished();

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        Assert.True(BoxShown(panel));
    }

    /// <summary>The report itself.</summary>
    [AvaloniaFact]
    public void APageThatCannotFilterHasNoBox()
    {
        var panel = Furnished();

        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        Assert.False(BoxShown(panel), "the Ships page offered a search box that does nothing");
    }

    /// <summary>
    /// And it comes back on the way out, rather than the surface losing its search for the rest of
    /// the session because one tab could not use it.
    /// </summary>
    [AvaloniaFact]
    public void TheBoxComesBackOnAPageThatCanUseIt()
    {
        var panel = Furnished();

        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        Assert.False(BoxShown(panel));

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        Assert.True(BoxShown(panel));
    }

    /// <summary>
    /// The settings page is ninety-odd rows and is the reason filtering exists at all.
    /// </summary>
    [AvaloniaFact]
    public void SettingsKeepsItsSearch()
    {
        var panel = Furnished();

        panel.Tab = PanelTab.Settings;
        Dispatcher.UIThread.RunJobs();

        Assert.True(BoxShown(panel));
    }

    /// <summary>A page that answers a query, standing in for the settings surface.</summary>
    private sealed class Narrowable : UserControl, IFilterablePage
    {
        public bool Filters => true;

        public void Filter(string? query)
        {
        }
    }
}
