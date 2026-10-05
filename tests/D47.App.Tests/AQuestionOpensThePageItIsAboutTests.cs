using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;
using Xunit;

namespace D47.App.Tests;

/// <summary>A page named by a turn opens on every surface, past whatever each was showing (#575).</summary>
public class AQuestionOpensThePageItIsAboutTests
{
    private static readonly Engineer Farseer = EngineerDirectory.ByName("Felicity Farseer")!;

    private static readonly Engineer Tani = EngineerDirectory.ByName("Hera Tani")!;

    private static readonly FleetEntry Runner =
        new(null, new StoredShip(4, "krait_mkii", "Fixture Runner", "Fixture Nebula Point"), IsActive: false);

    private static PanelNavigator Surface()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Transcript, new NavCrumb("transcript", "Transcript"));
        nav.Register(PanelTab.Assets, new NavCrumb(LoadoutPages.FleetRoot, "Ships"));
        nav.Register(PanelTab.Assets, new NavCrumb(EngineersPages.DirectoryRoot, "Engineers"));

        return nav;
    }

    private static void Open(PageRef page, params PanelNavigator[] navs) =>
        PageTrail.OpenEverywhere(page, navs.Select(nav => (nav, (Action<Action>)(action => action()))), () => [Runner]);

    [Fact]
    public void AnEngineerOpensOnEverySurface()
    {
        var first = Surface();
        var second = Surface();

        Open(PageRef.Engineer(Farseer.Id), first, second);

        Assert.All([first, second], nav =>
        {
            Assert.Equal(PanelTab.Assets, nav.Tab);
            Assert.Equal(EngineersPages.WhoPrefix + Farseer.Id, nav.Trail[^1].Key);
            Assert.Equal(EngineersPages.DirectoryRoot, nav.Root.Key);
        });
    }

    [Fact]
    public void AnotherEngineersPageIsReplaced()
    {
        var nav = Surface();
        nav.Show(EngineersPages.DirectoryRoot);
        nav.Drill(EngineersPages.Crumb(Tani));

        Open(PageRef.Engineer(Farseer.Id), nav);

        Assert.Equal(
            [EngineersPages.DirectoryRoot, EngineersPages.WhoPrefix + Farseer.Id],
            nav.Trail.Select(crumb => crumb.Key));
    }

    [Fact]
    public void AShipOpensItsFleetPage()
    {
        var nav = Surface();

        Open(PageRef.Ship(4), nav);

        Assert.Equal(LoadoutPages.FleetRoot, nav.Root.Key);
        Assert.Equal(LoadoutPages.Ship(Runner).Key, nav.Trail[^1].Key);
    }

    [Fact]
    public void ASurfaceHoldingAModalPageDoesNotMove()
    {
        var held = Surface();
        held.Take(new NavCrumb("chooser", "Chooser"));
        var free = Surface();

        Open(PageRef.Engineer(Farseer.Id), held, free);

        Assert.Equal(PanelTab.Transcript, held.Tab);
        Assert.Equal("chooser", held.Trail[^1].Key);
        Assert.Equal(EngineersPages.WhoPrefix + Farseer.Id, free.Trail[^1].Key);
    }

    [Fact]
    public void AShipNotInTheFleetMovesNothing()
    {
        var nav = Surface();

        Open(PageRef.Ship(99), nav);

        Assert.Equal(PanelTab.Transcript, nav.Tab);
        Assert.True(nav.AtRoot);
    }
}
