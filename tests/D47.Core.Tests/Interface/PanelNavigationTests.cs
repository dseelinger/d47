using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>
/// Where the Commander is, and every way of changing it (Phase 25, "Drill in, and find
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
    /// <summary>
    /// The root is never supplied to <see cref="PanelNavigator.GoTo(NavCrumb[])"/> — and a caller
    /// that supplies it anyway does not get a trail with the root in it twice, which is a trail no
    /// strip can draw: at three panes the same page would be hosted in two of them. Found by a
    /// generated adventure being offered on a wide window, 2026-08-22.
    /// </summary>
    [Fact]
    public void ASuppliedRootIsNotDoubled()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Loadout);

        Assert.True(nav.GoTo(new NavCrumb("fleet", "Ships"), new NavCrumb("ship:12", "Corsair")));
        Assert.Equal(["fleet", "ship:12"], nav.Trail.Select(crumb => crumb.Key));

        Assert.True(nav.GoTo(new NavCrumb("fleet", "Ships")));
        Assert.Equal(["fleet"], nav.Trail.Select(crumb => crumb.Key));

        // Only the root is dropped; a level that happens to repeat lower down is the caller's.
        Assert.True(nav.GoTo(new NavCrumb("ship:12", "Corsair"), new NavCrumb("ship:12", "Corsair")));
        Assert.Equal(["fleet", "ship:12", "ship:12"], nav.Trail.Select(crumb => crumb.Key));
    }

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

    /// <summary>
    /// The destination vocabulary is the roots, in bar order, and nothing else (Phase 46) —
    /// derived from what was registered rather than kept as a second list.
    /// </summary>
    [Fact]
    public void EveryRootOfEveryTabIsADestinationInBarOrder()
    {
        var nav = Furnished();

        Assert.Equal(
            new[] { "conversation", "technical", "checklist", "fleet", "locker" },
            nav.Destinations.Select(page => page.Root.Key));

        Assert.Equal(PanelTab.Loadout, nav.Destinations[^1].Tab);
        Assert.Equal("Suits and weapons (Loadout)", nav.Destinations[^1].Describe());
        Assert.Equal("Checklist", nav.Destinations[2].Describe());
    }

    /// <summary>
    /// Showing a root arrives on its tab and its mode, and showing the one already showing is
    /// refused — which is the <em>are you already there</em> a switch asks, answered exactly.
    /// </summary>
    [Fact]
    public void ShowingARootArrivesOnItsTabAndItsMode()
    {
        var nav = Furnished();

        Assert.True(nav.Show("locker"));
        Assert.Equal(PanelTab.Loadout, nav.Tab);
        Assert.Equal("locker", nav.Root.Key);

        Assert.False(nav.Show("locker"));

        // On the tab showing already, only the mode moves.
        Assert.True(nav.Show("fleet"));
        Assert.Equal(PanelTab.Loadout, nav.Tab);
        Assert.Equal("fleet", nav.Root.Key);
    }

    /// <summary>A root nobody furnished is declined, the way a tab nobody furnished is.</summary>
    [Fact]
    public void ShowingARootNobodyFurnishedIsDeclined()
    {
        var nav = Furnished();

        Assert.False(nav.Show("settings"));
        Assert.Equal(PanelTab.Transcript, nav.Tab);
    }

    [Fact]
    public void AChooserHoldsThePanelAgainstAShowToo()
    {
        var nav = Furnished();

        nav.Take(new NavCrumb("pick", "Pick one"));

        Assert.False(nav.Show("checklist"));
        Assert.Equal(PanelTab.Transcript, nav.Tab);
    }

    /// <summary>
    /// <b>Help is declared on a crumb and inherited downwards</b> (asked for 2026-08-22). Saying
    /// it once on the root covers a whole tab; a level says so again only where the subject really
    /// changes, which is what a per-tab table could not express for a tab like Routing that is
    /// three readings of one thing.
    /// </summary>
    [Fact]
    public void HelpIsTheDeepestThingOnTheTrailThatClaimsOne()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Engineers, new NavCrumb("directory", "Directory") { Help = "engineers" });
        nav.Select(PanelTab.Engineers);

        Assert.Equal("engineers", nav.Help);

        // An engineer inherits the root's, because an engineer's page is still Engineers.
        nav.Drill(new NavCrumb("who:1", "Farseer"));
        Assert.Equal("engineers", nav.Help);

        // A level whose subject really is something else says so, and wins while it is open.
        nav.Drill(new NavCrumb("blueprint", "Dirty Drive Tuning") { Help = "engineering" });
        Assert.Equal("engineering", nav.Help);

        nav.Back();
        Assert.Equal("engineers", nav.Help);
    }

    /// <summary>A tab nobody wrote help for claims none, rather than claiming the last one.</summary>
    [Fact]
    public void ATabThatClaimsNoHelpHasNone()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Engineers, new NavCrumb("directory", "Directory") { Help = "engineers" });
        nav.Register(PanelTab.Utilities, new NavCrumb("clocks", "Clocks"));

        nav.Select(PanelTab.Engineers);
        Assert.Equal("engineers", nav.Help);

        nav.Select(PanelTab.Utilities);
        Assert.Null(nav.Help);
    }

    /// <summary>
    /// Help itself declares none, so asking while it is open answers with the page underneath
    /// rather than with help about help.
    /// </summary>
    [Fact]
    public void AHelpLevelDoesNotClaimHelpOfItsOwn()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Engineers, new NavCrumb("directory", "Directory") { Help = "engineers" });
        nav.Select(PanelTab.Engineers);

        Assert.True(nav.Take(new NavCrumb("help:engineers", "Help")));

        Assert.True(nav.Modal);
        Assert.Equal("engineers", nav.Help);
    }

    /// <summary>
    /// <b>Help can be asked for out loud</b> (asked for 2026-08-22). The mark in the corner needs
    /// a ray, and the Commander this feature exists for is wearing a headset with their hands on
    /// a stick — so a route that needs pointing at is a route they do not have.
    /// </summary>
    [Fact]
    public void HelpOpensBySayingSo()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Engineers, new NavCrumb("directory", "Directory") { Help = "engineers" });
        nav.Select(PanelTab.Engineers);

        Assert.Equal("Help.", PanelPhrases.Apply("help", nav));
        Assert.True(nav.Modal);
        Assert.Equal("help:engineers", nav.Trail[^1].Key);

        // And back out again by the word that backs out of anything.
        Assert.Equal("Back to Directory.", PanelPhrases.Apply("back", nav));
        Assert.False(nav.Modal);
    }

    /// <summary>
    /// <b>Bare "help" is safe here and refused by the keyword router</b>, and the difference is
    /// the matching rule rather than an inconsistency: this matches a whole utterance, so a
    /// sentence that merely contains the word cannot hijack the panel mid-flight.
    /// </summary>
    [Fact]
    public void ASentenceContainingTheWordDoesNotOpenHelp()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Engineers, new NavCrumb("directory", "Directory") { Help = "engineers" });
        nav.Select(PanelTab.Engineers);

        Assert.Null(PanelPhrases.Apply("help me plot a route to Deciat", nav));
        Assert.Null(PanelPhrases.Apply("who can help with thrusters", nav));
        Assert.False(nav.Modal);
    }

    /// <summary>
    /// A level whose page has no band opens the index instead of answering nothing.
    /// <para>
    /// This asserted a fall-through until the index was written, on the reasoning that answering
    /// "there is none" to somebody asking for help is worse than letting the model try. Opening
    /// one level broader is better than either.
    /// </para>
    /// </summary>
    [Fact]
    public void SayingItWhereNothingIsWrittenOpensTheIndex()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Utilities, new NavCrumb("clocks", "Clocks") { Help = "no-such-page" });
        nav.Select(PanelTab.Utilities);

        Assert.Equal("Help.", PanelPhrases.Apply("help", nav));
        Assert.Equal("help:help", nav.Trail[^1].Key);
    }

    /// <summary>Asking again while it is open is refused rather than stacking a second copy.</summary>
    [Fact]
    public void AskingTwiceDoesNotStackHelpOnHelp()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Engineers, new NavCrumb("directory", "Directory") { Help = "engineers" });
        nav.Select(PanelTab.Engineers);

        Assert.Equal("Help.", PanelPhrases.Apply("help", nav));

        var depth = nav.Trail.Count;

        Assert.Null(PanelPhrases.Apply("help", nav));
        Assert.Equal(depth, nav.Trail.Count);
    }
}
