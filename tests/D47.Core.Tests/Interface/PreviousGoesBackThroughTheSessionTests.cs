using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>
/// <see cref="PanelNavigator.Previous"/> goes back through the moves made this session, across tabs, roots and
/// drills, and never to a form that was cancelled or a level that was taken away.
/// </summary>
public class PreviousGoesBackThroughTheSessionTests
{
    private const string Ship = "ship";

    private static PanelNavigator Furnished()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Transcript, new NavCrumb("conversation", "Conversation"));
        nav.Register(PanelTab.Transcript, new NavCrumb("technical", "Technical"));
        nav.Register(PanelTab.Commander, new NavCrumb("checklist", "Checklist"));
        nav.Register(PanelTab.Assets, new NavCrumb("fleet", "Ships"));
        nav.Register(PanelTab.Assets, new NavCrumb("locker", "Suits and weapons"));

        return nav;
    }

    private static string Place(PanelNavigator nav) => $"{nav.Tab}:{string.Join('>', nav.Trail.Select(crumb => crumb.Key))}";

    private static NavCrumb ShipCrumb(int id) => new($"ship:{id}", $"Ship {id}") { Level = Ship };

    /// <summary>Tab, sub-tab, drill, drill: four steps back return to each place in turn, the last across tabs.</summary>
    [Fact]
    public void EachEarlierPlaceComesBackInTurn()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);
        nav.SelectRoot("locker");
        nav.Drill(new NavCrumb("suit:1", "Maverick"));
        nav.Drill(new NavCrumb("weapon:2", "Karma C-44"));

        Assert.True(nav.Previous());
        Assert.Equal("Assets:locker>suit:1", Place(nav));
        Assert.True(nav.Previous());
        Assert.Equal("Assets:locker", Place(nav));
        Assert.True(nav.Previous());
        Assert.Equal("Assets:fleet", Place(nav));
        Assert.True(nav.Previous());
        Assert.Equal("Transcript:conversation", Place(nav));
        Assert.False(nav.Previous());
        Assert.False(nav.CanGoPrevious);
    }

    /// <summary>A step along the bar is remembered as a tab switch is.</summary>
    [Fact]
    public void AStepAlongTheBarIsRemembered()
    {
        var nav = Furnished();

        nav.Step(1);
        nav.Step(1);

        Assert.Equal(PanelTab.Assets, nav.Tab);
        Assert.True(nav.Previous());
        Assert.Equal(PanelTab.Commander, nav.Tab);
        Assert.True(nav.Previous());
        Assert.Equal(PanelTab.Transcript, nav.Tab);
    }

    /// <summary>A destination on another tab is one move, so one step back undoes it.</summary>
    [Fact]
    public void ShowingADestinationIsOneMove()
    {
        var nav = Furnished();

        nav.Show("locker");

        Assert.Equal("Assets:locker", Place(nav));
        Assert.True(nav.Previous());
        Assert.Equal("Transcript:conversation", Place(nav));
        Assert.False(nav.Previous());
    }

    /// <summary>A sub-page shown on the root already showing is remembered, and a sub-page that will not open is skipped.</summary>
    [Fact]
    public void ASubPageIsRememberedAndSkippedWhenItCannotOpen()
    {
        var nav = Furnished();
        var open = true;

        nav.Declare("fleet", new NavCrumb("shipyard", "Shipyard"), () => open);
        nav.Select(PanelTab.Assets);
        nav.Show("shipyard");
        nav.Select(PanelTab.Commander);

        open = false;

        Assert.True(nav.Previous());
        Assert.Equal("Assets:fleet", Place(nav));
        Assert.True(nav.Previous());
        Assert.Equal("Transcript:conversation", Place(nav));
    }

    /// <summary>A voice jump is remembered.</summary>
    [Fact]
    public void AVoiceJumpIsRemembered()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);
        nav.GoTo(ShipCrumb(1), new NavCrumb("slot:3", "Weapon 3"));

        Assert.True(nav.Previous());
        Assert.Equal("Assets:fleet", Place(nav));
    }

    /// <summary>A second ship replaces the first in the trail, and one step back shows the first ship.</summary>
    [Fact]
    public void AnotherShipIsAMoveBackToTheFirst()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);
        nav.Drill(ShipCrumb(1));
        nav.Drill(ShipCrumb(2));

        Assert.Equal("Assets:fleet>ship:2", Place(nav));
        Assert.True(nav.Previous());
        Assert.Equal("Assets:fleet>ship:1", Place(nav));
        Assert.True(nav.Previous());
        Assert.Equal("Assets:fleet", Place(nav));
    }

    /// <summary>A crumb swapped in place replaces where the panel is rather than adding a step back to it.</summary>
    [Fact]
    public void ACrumbSwappedInPlaceIsNotAStep()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);
        nav.Drill(new NavCrumb("plan", "Plan to Colonia"));
        nav.Drill(new NavCrumb("plan", "Plan to Sol"));

        Assert.True(nav.Previous());
        Assert.Equal("Assets:fleet", Place(nav));
    }

    /// <summary>A drill to a level already lower in the trail cuts back, and the level cut away is not returned to.</summary>
    [Fact]
    public void ADrillThatCutsBackReplacesWhereThePanelIs()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);
        nav.Drill(ShipCrumb(1));
        nav.Drill(new NavCrumb("slot:3", "Weapon 3"));
        nav.Drill(ShipCrumb(1));

        Assert.Equal("Assets:fleet>ship:1", Place(nav));
        Assert.True(nav.Previous());
        Assert.Equal("Assets:fleet", Place(nav));
    }

    /// <summary>A form drilled into and cancelled is not shown again by the next step back.</summary>
    [Fact]
    public void ACancelledFormIsNotShownAgain()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);
        nav.Drill(ShipCrumb(1));
        nav.Drill(new NavCrumb("bookmark.form", "New bookmark"));
        nav.Back();

        Assert.True(nav.Previous());
        Assert.Equal("Assets:fleet", Place(nav));
        Assert.True(nav.Previous());
        Assert.Equal("Transcript:conversation", Place(nav));
    }

    /// <summary>Going to the root replaces where the panel is, so a step back leaves the tab.</summary>
    [Fact]
    public void GoingToTheRootReplacesWhereThePanelIs()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);
        nav.Drill(ShipCrumb(1));
        nav.ToRoot();

        Assert.True(nav.Previous());
        Assert.Equal("Transcript:conversation", Place(nav));
    }

    /// <summary>A breadcrumb press replaces the level it left, so that level is not a step back; the levels before it still are.</summary>
    [Fact]
    public void ABreadcrumbPressReplacesWhereThePanelIs()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);
        nav.Drill(ShipCrumb(1));
        nav.Drill(new NavCrumb("slot:3", "Weapon 3"));
        nav.JumpTo(0);

        Assert.True(nav.Previous());
        Assert.Equal("Assets:fleet>ship:1", Place(nav));
        Assert.True(nav.Previous());
        Assert.Equal("Assets:fleet", Place(nav));
        Assert.True(nav.Previous());
        Assert.Equal("Transcript:conversation", Place(nav));
    }

    /// <summary>A chooser opened and dismissed adds nothing to go back through.</summary>
    [Fact]
    public void AChooserIsNotRemembered()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);
        nav.Take(new NavCrumb("choose.ship", "Choose a ship"));
        nav.Back();

        Assert.True(nav.Previous());
        Assert.Equal("Transcript:conversation", Place(nav));
    }

    /// <summary>While a chooser holds the panel, a step back is refused and changes nothing.</summary>
    [Fact]
    public void AChooserRefusesAStepBack()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);
        nav.Take(new NavCrumb("choose.ship", "Choose a ship"));

        var raised = 0;
        nav.Changed += (_, _) => raised++;

        Assert.False(nav.CanGoPrevious);
        Assert.False(nav.Previous());
        Assert.Equal("Assets:fleet>choose.ship", Place(nav));
        Assert.Equal(0, raised);
    }

    /// <summary>A ship cut from a trail is dropped from what can be gone back to.</summary>
    [Fact]
    public void AShipCutFromTheTrailIsNeverShownAgain()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);
        nav.Drill(ShipCrumb(1));
        nav.Select(PanelTab.Commander);

        Assert.True(nav.ToRootIfAny(PanelTab.Assets, crumb => crumb.Key == "ship:1"));

        Assert.True(nav.Previous());
        Assert.Equal("Assets:fleet", Place(nav));
        Assert.True(nav.Previous());
        Assert.Equal("Transcript:conversation", Place(nav));
        Assert.False(nav.Previous());
    }

    /// <summary>A matching level is forgotten from the tab's remembered places even when the trail showing does not hold it.</summary>
    [Fact]
    public void AMatchingLevelIsForgottenWhereverItWasRemembered()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);
        nav.Drill(ShipCrumb(1));
        nav.SelectRoot("locker");

        Assert.False(nav.ToRootIfAny(PanelTab.Assets, crumb => crumb.Key == "ship:1"));

        Assert.True(nav.Previous());
        Assert.Equal("Assets:fleet", Place(nav));
        Assert.True(nav.Previous());
        Assert.Equal("Transcript:conversation", Place(nav));
    }

    /// <summary>A root hidden while away from it is dropped from what can be gone back to.</summary>
    [Fact]
    public void AHiddenRootIsNeverShownAgain()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);
        nav.SelectRoot("locker");
        nav.Drill(new NavCrumb("suit:1", "Maverick"));
        nav.Select(PanelTab.Commander);

        Assert.True(nav.SetVisible(PanelTab.Assets, "locker", false));

        Assert.True(nav.Previous());
        Assert.Equal("Assets:fleet", Place(nav));
        Assert.True(nav.Previous());
        Assert.Equal("Transcript:conversation", Place(nav));
        Assert.False(nav.Previous());
    }

    /// <summary>A step back restores the trail, root and tab together and raises one change.</summary>
    [Fact]
    public void AStepBackIsOneChange()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);
        nav.Drill(ShipCrumb(1));
        nav.Select(PanelTab.Commander);
        nav.Select(PanelTab.Assets);
        nav.ToRoot();
        nav.Select(PanelTab.Transcript);

        var raised = 0;
        nav.Changed += (_, _) => raised++;

        Assert.True(nav.Previous());
        Assert.Equal("Assets:fleet", Place(nav));
        Assert.Equal(1, raised);
    }

    /// <summary>A step back is not itself remembered, so steps back keep going further back.</summary>
    [Fact]
    public void StepsBackDoNotGoRoundInACircle()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Commander);
        nav.Select(PanelTab.Assets);

        Assert.True(nav.Previous());
        Assert.Equal(PanelTab.Commander, nav.Tab);
        Assert.True(nav.Previous());
        Assert.Equal(PanelTab.Transcript, nav.Tab);
        Assert.False(nav.Previous());
    }

    /// <summary>One hundred places are remembered; the next move drops the oldest.</summary>
    [Fact]
    public void TheOldestPlaceIsDroppedPastOneHundred()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);

        for (var id = 1; id <= 101; id++)
        {
            nav.Drill(ShipCrumb(id));
        }

        var steps = 0;

        while (nav.Previous())
        {
            steps++;
        }

        Assert.Equal(100, steps);
        Assert.Equal("Assets:fleet>ship:1", Place(nav));
    }

    /// <summary>A place inside Settings is a step: going back raises the place left, and the first place is only a baseline.</summary>
    [Fact]
    public void APlaceInsideATabComesBackAsAStep()
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Transcript, new NavCrumb("conversation", "Conversation"));
        nav.Register(PanelTab.Settings, new NavCrumb("settings", "Settings"));
        nav.Select(PanelTab.Settings);

        var restored = new List<(PanelTab Tab, string Place)>();
        nav.PlaceRestored += (tab, place) => restored.Add((tab, place));

        nav.SetPlace(PanelTab.Settings, "audio");
        nav.SetPlace(PanelTab.Settings, "voices");
        nav.SetPlace(PanelTab.Settings, "keys");

        Assert.True(nav.Previous());
        Assert.True(nav.Previous());
        Assert.Equal([(PanelTab.Settings, "voices"), (PanelTab.Settings, "audio")], restored);

        Assert.True(nav.Previous());
        Assert.Equal(PanelTab.Transcript, nav.Tab);
        Assert.Equal(2, restored.Count);
    }

    /// <summary>A place noted without recording, or set to what is showing, adds no step.</summary>
    [Fact]
    public void ANotedPlaceAddsNoStep()
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Settings, new NavCrumb("settings", "Settings"));
        nav.Select(PanelTab.Settings);

        nav.SetPlace(PanelTab.Settings, "audio");
        nav.SetPlace(PanelTab.Settings, "voices", record: false);
        nav.SetPlace(PanelTab.Settings, "voices");

        Assert.False(nav.CanGoPrevious);
    }
}
