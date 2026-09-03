using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The headset back on the tab and the reading it was left on, across launches
/// (<a href="https://github.com/dseelinger/d47/issues/276">#276</a>) — the counterpart to
/// <see cref="EveryTabOpensWhereItWasLeftTests"/>, which covers the desktop window that #268
/// already gave this to.
/// </summary>
public sealed class TheHeadsetOpensWhereItWasLeftTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public void TheHeadsetOpensOnTheTabItWasLeftOn()
    {
        var store = Store();

        var first = Headset(store);

        first.Nav.Select(PanelTab.Checklist);
        Jobs();

        first.Dispose();

        // A second headset over the same store, which is what the next launch has.
        var second = Headset(store);

        Assert.Equal(PanelTab.Checklist, second.Nav.Tab);

        second.Dispose();
    }

    [AvaloniaFact]
    public void TheHeadsetOpensOnTheReadingItWasLeftOn()
    {
        var store = Store();

        var first = Headset(store);

        first.Nav.SelectRoot(PanelTab.Transcript, PanelView.JournalRoot);
        Jobs();

        first.Dispose();

        var second = Headset(store);

        Assert.Equal(PanelView.JournalRoot, second.Nav.RootKeyOf(PanelTab.Transcript));

        second.Dispose();
    }

    /// <summary>
    /// The window can be three levels into a ship's slots while the headset reads the
    /// conversation, so a store shared by both must not have one surface's tab overwrite the
    /// other's. Asserted directly rather than trusted, since both write through the one file.
    /// </summary>
    [AvaloniaFact]
    public void TheWindowAndTheHeadsetKeepIndependentTabs()
    {
        var store = Store();

        var window = new PanelView { DataContext = new PanelViewModel() };

        window.EnableRouting(new RoutingSurface(
            () => D47.Core.Journal.NavRoute.None, () => null));
        window.RememberRoots(new PanelRootMemory(store));
        window.RememberTab(new PanelTabMemory(store));

        var headset = Headset(store);

        window.Tab = PanelTab.Routing;
        headset.Nav.Select(PanelTab.Checklist);
        Jobs();

        Assert.Equal(PanelTab.Routing.ToString(), store.Load().LastTab);
        Assert.Equal(PanelTab.Checklist.ToString(), store.Load().LastTabVr);

        headset.Dispose();
    }

    private static ViewStateStore Store() =>
        new(
            new D47.Core.AppPaths(TempFolders.Create("d47-headset-tab-memory-tests")),
            NullLogger<ViewStateStore>.Instance);

    private static VrPanelSurface Headset(ViewStateStore store)
    {
        var headset = new VrPanelSurface(
            new PanelViewModel(),
            TestSurface.Settings(),
            _ => null,
            checklists: Checklists(),
            viewState: store);

        Jobs();

        return headset;
    }

    private static ChecklistService Checklists()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-headset-tab-memory-checklist"));
        paths.EnsureCreated();

        return new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);
    }
}
