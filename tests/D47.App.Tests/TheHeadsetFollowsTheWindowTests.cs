using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using D47.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The headset shows where the window is, from launch and on every move (#948).</summary>
[Trait("Category", "Integration")]
public sealed class TheHeadsetFollowsTheWindowTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    /// <summary>
    /// SteamVR keeps the last texture until the surface says it is dirty, so a move the window made has
    /// to say so in the same call.
    /// </summary>
    [AvaloniaFact]
    public void AMoveInTheWindowMarksTheHeadsetForARedraw()
    {
        var window = Window();
        var headset = Headset();
        var mirror = new TranscriptMirror();

        mirror.Lead(window.Nav);
        mirror.Add(headset.Nav);

        var (width, height) = headset.Size;
        var pixels = new VrPixels(width, height);

        headset.Draw(pixels.Address, pixels.RowBytes);
        Assert.False(headset.IsDirty);

        Assert.True(window.Nav.Select(PanelTab.Navigation));

        Assert.Equal(PanelTab.Navigation, headset.Nav.Tab);
        Assert.True(headset.IsDirty);

        headset.Dispose();
    }

    /// <summary>The window reopens on its own tab and root, and the headset arrives on them.</summary>
    [AvaloniaFact]
    public void TheHeadsetOpensWhereTheWindowReopened()
    {
        var store = Store();
        var window = Window();
        var root = window.Nav.Roots(PanelTab.Navigation)[^1].Key;

        store.Save(store.Load().With(PanelTab.Navigation.ToString(), root) with
        {
            LastTab = PanelTab.Navigation.ToString(),
        });

        // In the order the app wires it: the window leads, then restores where it was left, and the
        // headset is routed after.
        var mirror = new TranscriptMirror();
        mirror.Lead(window.Nav);
        window.RememberRoots(new PanelRootMemory(store));
        window.RememberTab(new PanelTabMemory(store));
        Jobs();

        Assert.Equal(PanelTab.Navigation, window.Nav.Tab);

        var headset = Headset();
        mirror.Add(headset.Nav);

        Assert.Equal(PanelTab.Navigation, headset.Nav.Tab);
        Assert.Equal(root, headset.Nav.RootKeyOf(PanelTab.Navigation));

        headset.Dispose();
    }

    private static PanelView Window()
    {
        var window = new PanelView { DataContext = new PanelViewModel() };

        window.EnableRouting(Routing());

        return window;
    }

    private static RoutingSurface Routing() => new(() => NavRoute.None, () => null);

    private static ViewStateStore Store() =>
        new(
            new D47.Core.AppPaths(TempFolders.Create("d47-headset-follows-tests")),
            new DiskFileSystem(),
            NullLogger<ViewStateStore>.Instance);

    private static VrPanelSurface Headset()
    {
        var headset = new VrPanelSurface(
            new PanelViewModel(),
            TestSurface.Settings(),
            _ => null,
            checklists: Checklists(),
            routing: Routing());

        Jobs();

        return headset;
    }

    private static ChecklistService Checklists()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-headset-follows-checklist"));
        paths.EnsureCreated();

        return new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);
    }
}
