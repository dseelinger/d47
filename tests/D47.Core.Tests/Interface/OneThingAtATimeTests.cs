using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>
/// Choosing a sibling replaces the one you were on (remediation.md 11, item 5).
/// <para>
/// Reported with a picture: <c>Ships › Tulimiekka › Reaper › Cartage</c>. A wide panel shows the
/// level you are on beside the one above it, so the fleet list is still on screen while a ship is
/// open — and pressing another ship there pushed it on top rather than in place of. Three ships
/// were open at once, which is not a thing a trail can mean.
/// </para>
/// </summary>
public class OneThingAtATimeTests
{
    private const string Ship = "ship";

    private static PanelNavigator Fleet()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Loadout, new NavCrumb("fleet", "Ships"));
        nav.Select(PanelTab.Loadout);

        return nav;
    }

    private static IReadOnlyList<string> Words(PanelNavigator nav) =>
        [.. nav.Trail.Select(crumb => crumb.Word)];

    [Fact]
    public void AnotherShipTakesTheFirstOnesPlace()
    {
        var nav = Fleet();

        nav.Drill(new NavCrumb("ship:1", "Tulimiekka") { Level = Ship });
        nav.Drill(new NavCrumb("ship:2", "Reaper") { Level = Ship });
        nav.Drill(new NavCrumb("ship:3", "Cartage") { Level = Ship });

        Assert.Equal(["Ships", "Cartage"], Words(nav));
    }

    /// <summary>
    /// And it takes whatever was open underneath it with it. A slot of the Tulimiekka is not a
    /// slot of the Reaper, so leaving it on the trail would point at a thing that is not there.
    /// </summary>
    [Fact]
    public void ChangingShipDropsWhatWasOpenBelowIt()
    {
        var nav = Fleet();

        nav.Drill(new NavCrumb("ship:1", "Tulimiekka") { Level = Ship });
        nav.Drill(new NavCrumb("slot:1|large1", "LargeHardpoint1") { Level = "slot" });

        Assert.Equal(["Ships", "Tulimiekka", "LargeHardpoint1"], Words(nav));

        nav.Drill(new NavCrumb("ship:2", "Reaper") { Level = Ship });

        Assert.Equal(["Ships", "Reaper"], Words(nav));
    }

    /// <summary>
    /// A deeper level still nests, because a slot of a ship really is inside that ship. Replacing
    /// is about levels that are alternatives to each other, not about depth.
    /// </summary>
    [Fact]
    public void ADeeperLevelStillNests()
    {
        var nav = Fleet();

        nav.Drill(new NavCrumb("ship:1", "Tulimiekka") { Level = Ship });
        nav.Drill(new NavCrumb("slot:1|large1", "LargeHardpoint1") { Level = "slot" });

        Assert.Equal(3, nav.Trail.Count);
    }

    /// <summary>
    /// And one slot replaces another, which is the same rule one level down.
    /// </summary>
    [Fact]
    public void OneSlotReplacesAnother()
    {
        var nav = Fleet();

        nav.Drill(new NavCrumb("ship:1", "Tulimiekka") { Level = Ship });
        nav.Drill(new NavCrumb("slot:1|large1", "LargeHardpoint1") { Level = "slot" });
        nav.Drill(new NavCrumb("slot:1|large2", "LargeHardpoint2") { Level = "slot" });

        Assert.Equal(["Ships", "Tulimiekka", "LargeHardpoint2"], Words(nav));
    }

    /// <summary>
    /// A crumb that names no level nests as it always did. Every trail that does not opt in is
    /// untouched, which is what keeps this from being a change to every tab at once.
    /// </summary>
    [Fact]
    public void ACrumbWithNoLevelIsUnchanged()
    {
        var nav = Fleet();

        nav.Drill(new NavCrumb("a", "A"));
        nav.Drill(new NavCrumb("b", "B"));
        nav.Drill(new NavCrumb("c", "C"));

        Assert.Equal(["Ships", "A", "B", "C"], Words(nav));
    }

    /// <summary>
    /// Pressing the ship you are already on is still a no-op rather than a rebuild.
    /// </summary>
    [Fact]
    public void TheShipYouAreOnIsNotPushedAgain()
    {
        var nav = Fleet();

        nav.Drill(new NavCrumb("ship:1", "Tulimiekka") { Level = Ship });

        Assert.False(nav.Drill(new NavCrumb("ship:1", "Tulimiekka") { Level = Ship }));
        Assert.Equal(["Ships", "Tulimiekka"], Words(nav));
    }

    /// <summary>
    /// Back still walks out one level at a time, so replacing a sibling has not turned the trail
    /// into something with no way out of it.
    /// </summary>
    [Fact]
    public void BackStillWalksOut()
    {
        var nav = Fleet();

        nav.Drill(new NavCrumb("ship:2", "Reaper") { Level = Ship });
        nav.Drill(new NavCrumb("slot:2|large1", "LargeHardpoint1") { Level = "slot" });

        Assert.True(nav.Back());
        Assert.Equal(["Ships", "Reaper"], Words(nav));

        Assert.True(nav.Back());
        Assert.Equal(["Ships"], Words(nav));

        Assert.False(nav.Back());
    }
}
