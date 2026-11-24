using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>One transcript page across both surfaces.</summary>
public class TranscriptMirrorTests
{
    private const string Conversation = "transcript.conversation";
    private const string Technical = "transcript.technical";
    private const string Log = "transcript.log";

    /// <summary>
    /// A surface as <c>PanelView</c> furnishes it: all three transcript roots, and a tab to be
    /// elsewhere on.
    /// </summary>
    private static PanelNavigator Surface()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Transcript, new NavCrumb(Conversation, "Conversation"));
        nav.Register(PanelTab.Transcript, new NavCrumb(Technical, "Technical"));
        nav.Register(PanelTab.Transcript, new NavCrumb(Log, "Log file"));
        nav.Register(PanelTab.Commander, new NavCrumb("checklist", "Checklist"));
        nav.Register(PanelTab.Commander, new NavCrumb("standing", "Standing"));

        return nav;
    }

    private static (PanelNavigator Window, PanelNavigator Headset, TranscriptMirror Mirror) Mirrored()
    {
        var window = Surface();
        var headset = Surface();
        var mirror = new TranscriptMirror();

        mirror.Add(window);
        mirror.Add(headset);

        return (window, headset, mirror);
    }

    /// <summary>The Commander's words: the window's selection is echoed in VR and vice-versa.</summary>
    [Fact]
    public void TheWindowMovesTheHeadsetAndTheHeadsetMovesTheWindow()
    {
        var (window, headset, mirror) = Mirrored();

        Assert.True(window.SelectRoot(PanelTab.Transcript, Log));

        Assert.Equal(Log, headset.RootKeyOf(PanelTab.Transcript));
        Assert.Equal(Log, mirror.Root);

        Assert.True(headset.SelectRoot(PanelTab.Transcript, Technical));

        Assert.Equal(Technical, window.RootKeyOf(PanelTab.Transcript));
        Assert.Equal(Technical, mirror.Root);
    }

    /// <summary>What you are reading is shared, and so is where you are (#2739).</summary>
    [Fact]
    public void TabsAndTrailsAreSharedToo()
    {
        var (window, headset, _) = Mirrored();

        Assert.True(window.Select(PanelTab.Commander));
        Assert.True(window.Drill(new NavCrumb("item", "An item")));

        Assert.Equal(PanelTab.Commander, headset.Tab);
        Assert.Equal(["checklist", "item"], headset.Trail.Select(crumb => crumb.Key));

        // The window opens the log file from its menu without leaving the checklist.
        Assert.True(window.SelectRoot(PanelTab.Transcript, Log));

        Assert.Equal(Log, headset.RootKeyOf(PanelTab.Transcript));
        Assert.Equal(PanelTab.Commander, headset.Tab);
        Assert.Equal(PanelTab.Commander, window.Tab);
        Assert.Equal(2, window.Trail.Count);
        Assert.Equal(2, headset.Trail.Count);
    }

    /// <summary>A changes, B is set, B announces, A must not be set again.</summary>
    [Fact]
    public void TheEchoIsStopped()
    {
        var (window, headset, _) = Mirrored();
        var windowChanges = 0;
        var headsetChanges = 0;

        window.Changed += (_, _) => windowChanges++;
        headset.Changed += (_, _) => headsetChanges++;

        Assert.True(window.SelectRoot(PanelTab.Transcript, Technical));

        Assert.Equal(1, windowChanges);
        Assert.Equal(1, headsetChanges);

        // And a move the headset would have declined anyway — it is already there — is nothing, not a second
        // round.
        Assert.False(headset.SelectRoot(PanelTab.Transcript, Technical));

        Assert.Equal(1, windowChanges);
        Assert.Equal(1, headsetChanges);
    }

    /// <summary>A chooser holds the headset while the window moves.</summary>
    [Fact]
    public void ASurfaceHeldByAChooserCatchesUpRatherThanDraggingTheOtherBack()
    {
        var (window, headset, mirror) = Mirrored();

        Assert.True(headset.Take(new NavCrumb("pick", "Pick one")));
        Assert.True(headset.Modal);

        Assert.True(window.SelectRoot(PanelTab.Transcript, Log));

        Assert.Equal(Conversation, headset.RootKeyOf(PanelTab.Transcript));
        Assert.Equal(Log, mirror.Root);

        Assert.True(headset.Back());

        Assert.Equal(Log, headset.RootKeyOf(PanelTab.Transcript));
        Assert.Equal(Log, window.RootKeyOf(PanelTab.Transcript));
    }

    /// <summary>
    /// A surface built after the Commander has already moved the other arrives agreeing rather than a
    /// step behind.
    /// </summary>
    [Fact]
    public void ALateSurfaceArrivesOnTheSharedRoot()
    {
        var window = Surface();
        var mirror = new TranscriptMirror();

        mirror.Add(window);
        Assert.True(window.SelectRoot(PanelTab.Transcript, Technical));

        var headset = Surface();
        mirror.Add(headset);

        Assert.Equal(Technical, headset.RootKeyOf(PanelTab.Transcript));
    }

    /// <summary>
    /// The spoken route is an initiator, not a second mechanism: a phrase applied to one surface moves it, and the
    /// mirror carries the reading and the tab to the other (#803, #2739).
    /// </summary>
    [Fact]
    public void APhraseAppliedToOneSurfaceMovesBoth()
    {
        var (window, headset, _) = Mirrored();

        Assert.True(headset.Select(PanelTab.Commander));
        Assert.True(headset.Drill(new NavCrumb("item", "An item")));

        Assert.Equal("Technical.", PanelPhrases.Apply("technical", window));

        Assert.Equal(Technical, headset.RootKeyOf(PanelTab.Transcript));
        Assert.Equal(PanelTab.Transcript, headset.Tab);
        Assert.Null(PanelPhrases.Apply("technical", headset));
    }

    /// <summary>The switch route likewise: the window's tab comes with the reading.</summary>
    [Fact]
    public void ASwitchFlipOnOneSurfaceIsCarriedWithItsTab()
    {
        var (window, headset, _) = Mirrored();

        Assert.True(headset.Select(PanelTab.Commander));

        Assert.True(window.Show(Log));

        Assert.Equal(Log, headset.RootKeyOf(PanelTab.Transcript));
        Assert.Equal(PanelTab.Transcript, headset.Tab);
        Assert.False(headset.Show(Log));
    }

    // ------------------------------------------- the window leads

    /// <summary>A follower that furnishes what the window does, plus one tab the window has not.</summary>
    private static (PanelNavigator Window, PanelNavigator Mini, TranscriptMirror Mirror) Following()
    {
        var window = Surface();
        window.Register(PanelTab.Settings, new NavCrumb("settings", "Settings"));

        var mini = Surface();

        var mirror = new TranscriptMirror();
        mirror.Lead(window);
        mirror.Add(mini);

        return (window, mini, mirror);
    }

    /// <summary>Switching to a tab in the main window always affects the mini panel (#2738).</summary>
    [Fact]
    public void TheWindowsTabCarriesToTheMiniPanel()
    {
        var (window, mini, _) = Following();

        Assert.True(window.Select(PanelTab.Commander));

        Assert.Equal(PanelTab.Commander, mini.Tab);
    }

    /// <summary>
    /// And the view of the tab with it, which is the other half of the request: the window changing
    /// only which root of a tab it is on still carries.
    /// </summary>
    [Fact]
    public void TheViewOfTheTabCarriesToo()
    {
        var (window, mini, _) = Following();

        window.Select(PanelTab.Commander);
        window.Select(PanelTab.Transcript);
        Assert.True(window.SelectRoot(PanelTab.Transcript, Log));

        Assert.Equal(Log, mini.RootKeyOf(PanelTab.Transcript));
    }

    /// <summary>A tab the mini panel has not got is carried as an address it stands in for, and left with the window (#2738).</summary>
    [Fact]
    public void ATabTheMiniPanelDoesNotHaveIsStoodInFor()
    {
        var (window, mini, _) = Following();

        mini.Select(PanelTab.Commander);

        Assert.True(window.Select(PanelTab.Settings));

        Assert.Equal(PanelTab.Settings, mini.StandingIn?.Tab);
        Assert.Equal(["settings"], mini.StandingIn!.Trail.Select(crumb => crumb.Key));
        Assert.Equal(window.DestinationKey, mini.DestinationKey);

        Assert.True(window.Select(PanelTab.Transcript));

        Assert.Null(mini.StandingIn);
        Assert.Equal(PanelTab.Transcript, mini.Tab);
    }

    /// <summary>Both ways: the window leads only at start-up (#2739).</summary>
    [Fact]
    public void TheMiniPanelMovesTheWindow()
    {
        var (window, mini, _) = Following();

        Assert.True(mini.Select(PanelTab.Commander));

        Assert.Equal(PanelTab.Commander, window.Tab);

        Assert.True(mini.Drill(new NavCrumb("item", "An item")));

        Assert.Equal(["checklist", "item"], window.Trail.Select(crumb => crumb.Key));
    }

    [Fact]
    public void TheLastMoveOnEitherSurfaceIsWhereBothAre()
    {
        var (window, mini, _) = Following();

        window.Select(PanelTab.Commander);
        mini.Select(PanelTab.Transcript);

        Assert.Equal(PanelTab.Transcript, mini.Tab);
        Assert.Equal(PanelTab.Transcript, window.Tab);

        window.Select(PanelTab.Commander);

        Assert.Equal(PanelTab.Commander, mini.Tab);
    }

    /// <summary>A second root of a tab the window is already on, on a tab other than the transcript (#948).</summary>
    [Fact]
    public void TheViewOfAnyTabCarries()
    {
        var (window, mini, _) = Following();

        Assert.True(window.Select(PanelTab.Commander));
        Assert.True(window.SelectRoot(PanelTab.Commander, "standing"));

        Assert.Equal(PanelTab.Commander, mini.Tab);
        Assert.Equal("standing", mini.RootKeyOf(PanelTab.Commander));
    }

    [Fact]
    public void ADrillInTheWindowIsCarriedAndSoIsGoingBack()
    {
        var (window, mini, _) = Following();

        window.Select(PanelTab.Commander);
        Assert.True(window.Drill(new NavCrumb("item", "An item")));
        Assert.True(window.Drill(new NavCrumb("step", "A step")));

        Assert.Equal(["checklist", "item", "step"], mini.Trail.Select(crumb => crumb.Key));

        Assert.True(window.Back());

        Assert.Equal(["checklist", "item"], mini.Trail.Select(crumb => crumb.Key));

        Assert.True(window.ToRoot());

        Assert.True(mini.AtRoot);
    }

    [Fact]
    public void AJumpBackInTheWindowIsCarried()
    {
        var (window, mini, _) = Following();

        window.Select(PanelTab.Commander);
        window.Drill(new NavCrumb("item", "An item"));
        window.Drill(new NavCrumb("step", "A step"));

        Assert.True(window.JumpTo(1));

        Assert.Equal(["checklist", "item"], mini.Trail.Select(crumb => crumb.Key));
    }

    /// <summary>A follower without the window's root goes as far as the tab and stands in for the rest of the address (#2738).</summary>
    [Fact]
    public void AFollowerWithoutTheRootGoesAsFarAsTheTabAndStandsInForTheAddress()
    {
        var window = Surface();
        var mini = new PanelNavigator();
        mini.Register(PanelTab.Transcript, new NavCrumb(Conversation, "Conversation"));
        mini.Register(PanelTab.Commander, new NavCrumb("checklist", "Checklist"));

        var mirror = new TranscriptMirror();
        mirror.Lead(window);
        mirror.Add(mini);

        window.Select(PanelTab.Commander);
        window.SelectRoot(PanelTab.Commander, "standing");
        window.Drill(new NavCrumb("rank", "A rank"));

        Assert.Equal(PanelTab.Commander, mini.Tab);
        Assert.Equal(["checklist"], mini.Trail.Select(crumb => crumb.Key));
        Assert.Equal(PanelTab.Commander, mini.StandingIn?.Tab);
        Assert.Equal(["standing", "rank"], mini.StandingIn!.Trail.Select(crumb => crumb.Key));
        Assert.Equal(2, mini.StandingIn.RootCount);
        Assert.Equal(window.DestinationKey, mini.DestinationKey);
    }

    [Fact]
    public void AFollowerAddedAfterTheWindowMovedArrivesWhereTheWindowIs()
    {
        var window = Surface();
        var mirror = new TranscriptMirror();
        mirror.Lead(window);

        window.Select(PanelTab.Commander);
        window.SelectRoot(PanelTab.Commander, "standing");
        window.Drill(new NavCrumb("rank", "A rank"));

        var mini = Surface();
        mirror.Add(mini);

        Assert.Equal(PanelTab.Commander, mini.Tab);
        Assert.Equal(["standing", "rank"], mini.Trail.Select(crumb => crumb.Key));
    }

    [Fact]
    public void AWindowNamedLeaderAfterItsFollowersBringsThemLevel()
    {
        var window = Surface();
        var mini = Surface();
        var mirror = new TranscriptMirror();

        mirror.Add(mini);

        window.Select(PanelTab.Commander);
        window.SelectRoot(PanelTab.Commander, "standing");

        mirror.Lead(window);

        Assert.Equal(PanelTab.Commander, mini.Tab);
        Assert.Equal("standing", mini.RootKeyOf(PanelTab.Commander));
    }

    /// <summary>The window's reading is the one kept, not the follower's that was there first.</summary>
    [Fact]
    public void AWindowNamedLeaderAfterItsFollowersKeepsItsOwnTranscript()
    {
        var window = Surface();
        var mini = Surface();
        var mirror = new TranscriptMirror();

        mirror.Add(mini);
        window.SelectRoot(PanelTab.Transcript, Log);

        mirror.Lead(window);

        Assert.Equal(Log, window.RootKeyOf(PanelTab.Transcript));
        Assert.Equal(Log, mini.RootKeyOf(PanelTab.Transcript));
        Assert.Equal(Log, mirror.Root);
    }

    /// <summary>
    /// A chooser opened in the window stays on the window; a dialog is carried as an address the follower stands in
    /// for over the page under it, and leaving it returns the follower to that page (#2738).
    /// </summary>
    [Fact]
    public void AChooserStaysOnTheSurfaceThatOpenedItAndADialogIsStoodInFor()
    {
        var (window, mini, _) = Following();

        window.Select(PanelTab.Commander);
        window.Drill(new NavCrumb("item", "An item"));

        var miniChanges = 0;
        mini.Changed += (_, _) => miniChanges++;

        Assert.True(window.Take(new NavCrumb("pick", "Pick one")));

        Assert.False(mini.Modal);
        Assert.Equal(0, miniChanges);

        Assert.True(window.Back());
        Assert.Null(mini.StandingIn);

        Assert.True(window.Drill(new NavCrumb("dialog:1", "Confirm") { Local = true }));

        Assert.Equal(["checklist", "item"], mini.Trail.Select(crumb => crumb.Key));
        Assert.Equal(["checklist", "item", "dialog:1"], mini.StandingIn?.Trail.Select(crumb => crumb.Key));
        Assert.Equal(window.DestinationKey, mini.DestinationKey);

        Assert.True(window.Back());

        Assert.Null(mini.StandingIn);
        Assert.Equal(["checklist", "item"], mini.Trail.Select(crumb => crumb.Key));
    }

    /// <summary>A move the follower makes on the tab it stands in for is its own, and ends the stand-in.</summary>
    [Fact]
    public void AFollowersOwnMoveEndsItsStandIn()
    {
        var (window, mini, _) = Following();

        window.Select(PanelTab.Commander);
        window.Drill(new NavCrumb("item", "An item"));
        window.Drill(new NavCrumb("dialog:1", "Confirm") { Local = true });

        Assert.NotNull(mini.StandingIn);

        Assert.True(mini.Back());

        Assert.Null(mini.StandingIn);
        Assert.Equal(["checklist"], mini.Trail.Select(crumb => crumb.Key));
    }

    /// <summary>The transcript reading caught up on a follower standing in for another tab leaves the stand-in in place.</summary>
    [Fact]
    public void ATranscriptReadingCarriedUnderAStandInKeepsIt()
    {
        var (window, mini, _) = Following();

        Assert.True(window.Select(PanelTab.Settings));
        Assert.True(window.SelectRoot(PanelTab.Transcript, Log));

        Assert.Equal(Log, mini.RootKeyOf(PanelTab.Transcript));
        Assert.Equal(PanelTab.Settings, mini.StandingIn?.Tab);
    }

    /// <summary>A declared sub-page the follower lacks is stood in for under the window's destination key.</summary>
    [Fact]
    public void ASubPageTheFollowerLacksReportsTheWindowsDestination()
    {
        var (window, mini, _) = Following();

        Assert.True(window.Declare("standing", new NavCrumb("ranks", "Ranks")));
        Assert.True(window.Show("ranks"));

        Assert.Equal("ranks", window.DestinationKey);
        Assert.Equal("ranks", mini.DestinationKey);
        Assert.Equal(["standing", "ranks"], mini.StandingIn?.Trail.Select(crumb => crumb.Key));
    }

    /// <summary>
    /// The mini panel moving the transcript reaches the window, and the window being moved is not then mistaken for a
    /// move of its own and carried back.
    /// </summary>
    [Fact]
    public void ACarriedTranscriptMoveIsNotCarriedBack()
    {
        var (window, mini, _) = Following();

        Assert.True(mini.Select(PanelTab.Commander));

        var miniChanges = 0;
        mini.Changed += (_, _) => miniChanges++;

        Assert.True(mini.SelectRoot(PanelTab.Transcript, Log));

        Assert.Equal(Log, window.RootKeyOf(PanelTab.Transcript));
        Assert.Equal(PanelTab.Commander, window.Tab);
        Assert.Equal(1, miniChanges);

        // A change to a tab the window is not showing is not a move of the window's address.
        Assert.True(window.Drill(PanelTab.Transcript, new NavCrumb("entry", "An entry")));

        Assert.Equal(PanelTab.Commander, mini.Tab);
        Assert.Equal([Log], mini.TrailOf(PanelTab.Transcript).Select(crumb => crumb.Key));
        Assert.Equal(1, miniChanges);
    }

    /// <summary>Three navigators and none named leader: a move on any one puts the other two on its address, once (#2739).</summary>
    [Fact]
    public void AMoveOnAnyOfThreeIsCarriedToTheOtherTwoWithoutAnEcho()
    {
        var navigators = new[] { Surface(), Surface(), Surface() };
        var mirror = new TranscriptMirror();
        var changes = new int[navigators.Length];

        for (var at = 0; at < navigators.Length; at++)
        {
            var index = at;
            mirror.Add(navigators[at]);
            navigators[at].Changed += (_, _) => changes[index]++;
        }

        void Moves(PanelNavigator mover, Func<PanelNavigator, bool> move)
        {
            var before = changes.ToArray();

            Assert.True(move(mover));
            Assert.All(navigators, nav => Assert.Equal(mover.Tab, nav.Tab));
            Assert.All(navigators, nav => Assert.Equal(mover.Trail.Select(crumb => crumb.Key), nav.Trail.Select(crumb => crumb.Key)));
            for (var at = 0; at < navigators.Length; at++)
            {
                if (ReferenceEquals(navigators[at], mover))
                {
                    Assert.Equal(before[at] + 1, changes[at]);
                }
                else
                {
                    Assert.True(changes[at] > before[at]);
                }
            }
        }

        Moves(navigators[2], nav => nav.Select(PanelTab.Commander));
        Moves(navigators[1], nav => nav.Drill(new NavCrumb("item", "An item")));
        Moves(navigators[0], nav => nav.SelectRoot(PanelTab.Commander, "standing"));
        Moves(navigators[2], nav => nav.Select(PanelTab.Transcript));
        Moves(navigators[1], nav => nav.Previous());
        Moves(navigators[0], nav => nav.Next());
    }

    /// <summary>Back and a history step on either surface go where they would on the other, because a carried move is in its history too.</summary>
    [Fact]
    public void AHistoryStepOnEitherSurfaceGoesToTheSameAddress()
    {
        var (window, mini, _) = Following();

        window.Select(PanelTab.Commander);
        window.Drill(new NavCrumb("item", "An item"));
        window.Drill(new NavCrumb("step", "A step"));

        Assert.True(mini.Back());

        Assert.Equal(["checklist", "item"], window.Trail.Select(crumb => crumb.Key));

        Assert.True(window.Previous());

        Assert.Equal(["checklist"], mini.Trail.Select(crumb => crumb.Key));

        Assert.True(mini.Previous());

        Assert.Equal(PanelTab.Transcript, window.Tab);
        Assert.Equal(PanelTab.Transcript, mini.Tab);

        Assert.True(window.Next());
        Assert.True(mini.Next());

        Assert.Equal(PanelTab.Commander, window.Tab);
        Assert.Equal(["checklist", "item"], window.Trail.Select(crumb => crumb.Key));
        Assert.Equal(["checklist", "item"], mini.Trail.Select(crumb => crumb.Key));
    }

    /// <summary>A chooser holds its surface where it is while the others move, and once it is gone that surface goes to them.</summary>
    [Fact]
    public void ASurfaceHeldByAChooserGoesToTheOthersAddressWhenItIsDismissed()
    {
        var (window, mini, _) = Following();

        Assert.True(mini.Take(new NavCrumb("pick", "Pick one")));
        Assert.False(mini.Select(PanelTab.Commander));

        Assert.True(window.Select(PanelTab.Commander));
        Assert.True(window.Drill(new NavCrumb("item", "An item")));

        Assert.Equal(PanelTab.Transcript, mini.Tab);
        Assert.True(mini.Modal);

        Assert.True(mini.Back());

        Assert.Equal(PanelTab.Commander, mini.Tab);
        Assert.Equal(["checklist", "item"], mini.Trail.Select(crumb => crumb.Key));
        Assert.Equal(["checklist", "item"], window.Trail.Select(crumb => crumb.Key));

        Assert.True(mini.Previous());

        Assert.Equal(["checklist"], window.Trail.Select(crumb => crumb.Key));
    }

    /// <summary>A surface standing in for a page it cannot draw steps back from that page, not from where it was left.</summary>
    [Fact]
    public void APreviousOnASurfaceStandingInGoesBackFromTheAddress()
    {
        var (window, mini, _) = Following();

        window.Select(PanelTab.Commander);
        window.Drill(new NavCrumb("item", "An item"));
        window.Select(PanelTab.Settings);

        Assert.Equal(PanelTab.Settings, mini.StandingIn?.Tab);

        Assert.True(mini.Previous());

        Assert.Null(mini.StandingIn);
        Assert.Equal(PanelTab.Commander, window.Tab);
        Assert.Equal(["checklist", "item"], window.Trail.Select(crumb => crumb.Key));
        Assert.Equal(["checklist", "item"], mini.Trail.Select(crumb => crumb.Key));
    }

    /// <summary>A surface added after another moved arrives where that one is, leader or not.</summary>
    [Fact]
    public void ALateSurfaceArrivesWhereTheLastMoveWas()
    {
        var (window, mini, mirror) = Following();

        mini.Select(PanelTab.Commander);
        mini.SelectRoot(PanelTab.Commander, "standing");

        var late = Surface();
        mirror.Add(late);

        Assert.Equal(PanelTab.Commander, late.Tab);
        Assert.Equal("standing", late.RootKeyOf(PanelTab.Commander));
        Assert.Equal(PanelTab.Commander, window.Tab);
    }

    /// <summary>The transcript half is untouched and still symmetrical.</summary>
    [Fact]
    public void TheTranscriptIsStillMirroredBothWays()
    {
        var (window, mini, _) = Following();

        Assert.True(mini.SelectRoot(PanelTab.Transcript, Technical));

        Assert.Equal(Technical, window.RootKeyOf(PanelTab.Transcript));
    }
}
