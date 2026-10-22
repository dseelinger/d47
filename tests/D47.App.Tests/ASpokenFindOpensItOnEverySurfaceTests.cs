using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A spoken Find phrase opens its result on every routed surface, each through its own view (#1473).</summary>
[Trait("Category", "Integration")]
public sealed class ASpokenFindOpensItOnEverySurfaceTests
{
    private static void Jobs()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class Shown(PanelView view, Window window, Func<SettingsView?> page, SettingsView strip)
    {
        public PanelView View => view;

        public SettingsView? Page => page();

        public SettingsView Strip => strip;

        public void Close() => window.Close();
    }

    private static (SettingsService Settings, ViewStateStore ViewState) Surface()
    {
        var (settings, viewState, _) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        return (settings, viewState);
    }

    private static Shown Show(SettingsService settings, ViewStateStore viewState)
    {
        var root = TempFolders.Create("d47-spoken-find-tests");

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => null);

        var view = new PanelView { DataContext = new PanelViewModel() };
        var strip = new SettingsView();
        SettingsView? page = null;

        strip.Attach(settings, viewState, tabPlaceId: "fleet-ships");

        view.EnableSettings(() =>
        {
            page = new SettingsView();
            page.Attach(settings, viewState);
            return page;
        });

        view.EnableLoadout(ships, checklists, () => null, settingsStrip: () => strip);

        var window = new Window { Content = view, Width = 1000, Height = 800 };
        window.Show();
        Jobs();

        return new Shown(view, window, () => page, strip);
    }

    private static (PanelRouting Routing, Shown Window, Shown Headset, PanelNavigator Mini) Routed()
    {
        var (settings, viewState) = Surface();
        var window = Show(settings, viewState);
        var headset = Show(settings, viewState);

        var mini = new PanelNavigator();
        mini.Register(PanelTab.Transcript, new NavCrumb("conversation", "Conversation"));
        mini.Register(PanelTab.Assets, new NavCrumb("loadout.ships", "Ships"));

        var routing = new PanelRouting(() => false, () => []);
        var ui = Dispatcher.UIThread;

        routing.RouteNavigation(window.View.Nav, move => ui.Post(move), leads: true, openFound: window.View.OpenFound);
        routing.RouteNavigation(headset.View.Nav, move => ui.Post(move), openFound: headset.View.OpenFound);
        routing.RouteNavigation(mini, move => ui.Post(move));
        routing.RouteFind(new PanelFind(window.View.Nav, settings, window.View.TabWords));

        return (routing, window, headset, mini);
    }

    [AvaloniaFact]
    public void TheFuelSettingLandsOnItsRowOnEverySurfaceAndSaysWhereItIs()
    {
        using var look = AppLook.Put();
        var (routing, window, headset, mini) = Routed();

        var said = routing.Find("where is the fuel setting");
        Jobs();
        Jobs();

        Assert.NotNull(said);
        Assert.StartsWith("Settings › ", said, StringComparison.Ordinal);
        Assert.EndsWith(" › Fuel and range.", said, StringComparison.Ordinal);

        foreach (var surface in new[] { window, headset })
        {
            Assert.Equal(PanelTab.Settings, surface.View.Nav.Tab);
            Assert.NotNull(surface.Page);
            Assert.Same(surface.Page.FrameFor(CalloutCapability.FuelKey), surface.Page.LandedOn);
        }

        Assert.Equal(PanelTab.Transcript, mini.Tab);

        window.Close();
        headset.Close();
    }

    [AvaloniaFact]
    public void TheHullPicturesSettingOpensEachSurfacesOwnStrip()
    {
        using var look = AppLook.Put();
        var (routing, window, headset, _) = Routed();

        Assert.Equal("Asset Mgmt › Ships › Hull pictures.", routing.Find("show me the hull pictures setting"));
        Jobs();
        Jobs();

        foreach (var surface in new[] { window, headset })
        {
            Assert.Equal(PanelTab.Assets, surface.View.Nav.Tab);
            Assert.Equal("loadout.ships", surface.View.Nav.RootKeyOf(PanelTab.Assets));
            Assert.StartsWith("▾ SETTINGS ", ((Button)surface.Strip.BarTool!).Content as string, StringComparison.Ordinal);
            Assert.Same(surface.Strip.FrameFor("ships.art"), surface.Strip.LandedOn);
        }

        window.Close();
        headset.Close();
    }

    [AvaloniaFact]
    public void APageResultAlsoMovesASurfaceWithNoViewOfItsOwn()
    {
        using var look = AppLook.Put();
        var (routing, window, headset, mini) = Routed();

        Assert.Equal("Asset Mgmt › Ships.", routing.Find("show me the ships page"));
        Jobs();

        Assert.Equal("loadout.ships", window.View.Nav.RootKeyOf(PanelTab.Assets));
        Assert.Equal(PanelTab.Assets, headset.View.Nav.Tab);
        Assert.Equal(PanelTab.Assets, mini.Tab);

        window.Close();
        headset.Close();
    }

    [AvaloniaTheory]
    [InlineData("where is my Anaconda")]
    [InlineData("find the nearest signal")]
    [InlineData("where do I turn off my shields")]
    [InlineData("open the ships tab")]
    public void APhraseAboutTheGameIsLeftForTheTurn(string spoken)
    {
        using var look = AppLook.Put();
        var (routing, window, headset, _) = Routed();

        Assert.Null(routing.Find(spoken));
        Jobs();

        Assert.Equal(PanelTab.Transcript, window.View.Nav.Tab);

        window.Close();
        headset.Close();
    }
}
