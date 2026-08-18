using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>
/// Where the Commander is, and every way of changing it (list.md Phase 25, "Drill in, and find
/// your way back").
/// <para>
/// Walked here rather than through a surface, which is the point of the arithmetic being in Core:
/// the tab is the root, a tab with several roots keeps a stack per root, and a trail can
/// materialise mid-drill because voice jumps levels. None of that needs a window to be true.
/// </para>
/// </summary>
public class PanelNavigationTests
{
    private static PanelNavigator Furnished()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Transcript, new NavCrumb("conversation", "Conversation"));
        nav.Register(PanelTab.Transcript, new NavCrumb("technical", "Technical"));
        nav.Register(PanelTab.Checklist, new NavCrumb("checklist", "Checklist"));
        nav.Register(PanelTab.Loadout, new NavCrumb("fleet", "Ships"));
        nav.Register(PanelTab.Loadout, new NavCrumb("locker", "Suits and weapons"));

        return nav;
    }

    /// <summary>A tab nobody furnished is a tab this surface does not have.</summary>
    [Fact]
    public void ATabWithNoRootsCannotBeSelected()
    {
        var nav = Furnished();

        Assert.False(nav.Has(PanelTab.Settings));
        Assert.False(nav.Select(PanelTab.Settings));
        Assert.Equal(PanelTab.Transcript, nav.Tab);
    }

    /// <summary>
    /// The tab is the root rather than the first level, so the breadcrumb's first crumb is the
    /// tab and there is nothing above it.
    /// </summary>
    [Fact]
    public void TheTabIsTheRoot()
    {
        var nav = Furnished();

        Assert.True(nav.AtRoot);
        Assert.Equal("Conversation", nav.Root.Word);
        Assert.False(nav.Back());
    }

    /// <summary>Registering the same root twice is one root, not a repeated entry in the control.</summary>
    [Fact]
    public void RegisteringARootTwiceIsIdempotent()
    {
        var nav = Furnished();

        nav.Register(PanelTab.Checklist, new NavCrumb("checklist", "Checklist"));

        Assert.Single(nav.Roots(PanelTab.Checklist));
    }

    /// <summary>
    /// A tab with more than one root keeps a stack per root: leaving Ships halfway down a slot
    /// and coming back arrives where it was left, and going to Suits meanwhile disturbs nothing.
    /// </summary>
    [Fact]
    public void EachRootKeepsItsOwnStack()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Loadout);
        nav.Drill(new NavCrumb("ship:12", "Corsair"));
        nav.Drill(new NavCrumb("slot:3", "Weapon 3"));

        Assert.Equal(["Ships", "Corsair", "Weapon 3"], nav.Trail.Select(crumb => crumb.Word));

        nav.SelectRoot("locker");

        Assert.Equal(["Suits and weapons"], nav.Trail.Select(crumb => crumb.Word));

        nav.SelectRoot("fleet");

        Assert.Equal(["Ships", "Corsair", "Weapon 3"], nav.Trail.Select(crumb => crumb.Word));
    }

    /// <summary>And drill state survives a tab switch, which is the same claim from outside.</summary>
    [Fact]
    public void DrillStateSurvivesATabSwitch()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Loadout);
        nav.Drill(new NavCrumb("ship:12", "Corsair"));

        nav.Select(PanelTab.Checklist);
        Assert.True(nav.AtRoot);

        nav.Select(PanelTab.Loadout);
        Assert.Equal(["Ships", "Corsair"], nav.Trail.Select(crumb => crumb.Word));
    }

    /// <summary>
    /// Pressing an already-selected tab returns to its root — which is what the surface has to
    /// add by hand, because a checked RadioButton pressed again announces nothing.
    /// </summary>
    [Fact]
    public void ReturningToTheRootIsAMoveAndSaysSo()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Loadout);
        nav.Drill(new NavCrumb("ship:12", "Corsair"));

        Assert.True(nav.ToRoot());
        Assert.True(nav.AtRoot);

        // And at the root it is a no-op the caller can tell from a move, so a host can leave the
        // gesture to whatever else wants it.
        Assert.False(nav.ToRoot());
    }

    /// <summary>
    /// Voice jumps levels, so a trail has to materialise mid-drill with a valid chain behind it
    /// rather than only being built by walking.
    /// </summary>
    [Fact]
    public void ATrailCanBeMaterialisedRatherThanWalked()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Loadout);

        Assert.True(nav.GoTo(
            new NavCrumb("ship:12", "Corsair"),
            new NavCrumb("slot:3", "Weapon 3")));

        // Root first, and every crumb between, so the breadcrumb is right the first time it is
        // drawn rather than after the Commander has walked back up it.
        Assert.Equal(["Ships", "Corsair", "Weapon 3"], nav.Trail.Select(crumb => crumb.Word));

        // And back from a jump goes where walking would have: one level, not out of the tab.
        Assert.True(nav.Back());
        Assert.Equal(["Ships", "Corsair"], nav.Trail.Select(crumb => crumb.Word));
    }

    /// <summary>Pressing a crumb goes back to it, and pressing the one you are on does nothing.</summary>
    [Fact]
    public void ACrumbGoesBackToItself()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Loadout);
        nav.GoTo(new NavCrumb("ship:12", "Corsair"), new NavCrumb("slot:3", "Weapon 3"));

        Assert.False(nav.JumpTo(2));
        Assert.True(nav.JumpTo(1));
        Assert.Equal(["Ships", "Corsair"], nav.Trail.Select(crumb => crumb.Word));
    }

    /// <summary>
    /// And by word, because each crumb is a word that can be said as well as pressed. The nearest
    /// match wins, so a word appearing twice takes the Commander up one rather than all the way.
    /// </summary>
    [Fact]
    public void ACrumbCanBeReachedByItsWord()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Loadout);
        nav.GoTo(
            new NavCrumb("ship:12", "Corsair"),
            new NavCrumb("slot:3", "Modules"),
            new NavCrumb("ship:14", "Modules"),
            new NavCrumb("blueprint", "Overcharged"));

        Assert.True(nav.JumpTo("modules"));
        Assert.Equal(4, nav.Trail.Count);
        Assert.Equal("Modules", nav.Trail[^1].Word);
    }

    /// <summary>
    /// A chooser takes the panel until it is dismissed, and while it does <b>no route navigates
    /// away</b> — which is the whole of "drawn as a level, behaving as a modal".
    /// </summary>
    [Fact]
    public void AChooserHoldsThePanel()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Loadout);
        nav.Drill(new NavCrumb("ship:12", "Corsair"));

        Assert.True(nav.Take(new NavCrumb("choose:slot3", "Weapon 3")));
        Assert.True(nav.Modal);

        Assert.False(nav.Select(PanelTab.Checklist));
        Assert.False(nav.SelectRoot("locker"));
        Assert.False(nav.Drill(new NavCrumb("elsewhere", "Elsewhere")));
        Assert.False(nav.GoTo(new NavCrumb("ship:14", "Python")));

        // A chooser opened from a chooser is legitimate — pick the engineer, then the grade — and
        // is the one thing that may push onto a modal.
        Assert.True(nav.Take(new NavCrumb("choose:grade", "Grade")));

        // Dismissing is back, which is the same affordance every other level has.
        Assert.True(nav.Back());
        Assert.True(nav.Back());
        Assert.False(nav.Modal);
        Assert.Equal(["Ships", "Corsair"], nav.Trail.Select(crumb => crumb.Word));
    }

    /// <summary>
    /// Pressing the tab you are on escapes a chooser. A modal that cannot be left by the
    /// affordance a Commander reaches for first is one they close by quitting.
    /// </summary>
    [Fact]
    public void TheTabStillEscapesAChooser()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Loadout);
        nav.Drill(new NavCrumb("ship:12", "Corsair"));
        nav.Take(new NavCrumb("choose:slot3", "Weapon 3"));

        Assert.True(nav.ToRoot());
        Assert.False(nav.Modal);
        Assert.True(nav.AtRoot);
    }

    /// <summary>Nothing that was refused raises the event a surface redraws on.</summary>
    [Fact]
    public void ARefusedMoveSaysNothing()
    {
        var nav = Furnished();
        var raised = 0;

        nav.Changed += (_, _) => raised++;

        Assert.False(nav.Select(PanelTab.Transcript));
        Assert.False(nav.Select(PanelTab.Settings));
        Assert.False(nav.SelectRoot("nonsense"));
        Assert.False(nav.Back());
        Assert.False(nav.JumpTo(9));

        Assert.Equal(0, raised);

        Assert.True(nav.Select(PanelTab.Checklist));
        Assert.Equal(1, raised);
    }

    /// <summary>Drilling into the level already showing is not a level.</summary>
    [Fact]
    public void DrillingIntoWhereYouAlreadyAreIsRefused()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Loadout);

        Assert.True(nav.Drill(new NavCrumb("ship:12", "Corsair")));
        Assert.False(nav.Drill(new NavCrumb("ship:12", "Corsair")));
        Assert.Equal(2, nav.Trail.Count);
    }
}
