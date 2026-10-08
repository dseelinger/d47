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

    /// <summary>What you are reading is shared; where you are is not.</summary>
    [Fact]
    public void TabsAndTrailsStayPerSurface()
    {
        var (window, headset, _) = Mirrored();

        Assert.True(window.Select(PanelTab.Commander));
        Assert.True(window.Drill(new NavCrumb("item", "An item")));

        Assert.Equal(PanelTab.Transcript, headset.Tab);
        Assert.True(headset.AtRoot);

        // The window opens the log file from its menu without leaving the checklist.
        Assert.True(window.SelectRoot(PanelTab.Transcript, Log));

        Assert.Equal(Log, headset.RootKeyOf(PanelTab.Transcript));
        Assert.Equal(PanelTab.Transcript, headset.Tab);
        Assert.Equal(PanelTab.Commander, window.Tab);
        Assert.Equal(2, window.Trail.Count);
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
    /// The spoken route is an initiator, not a second mechanism: the mirror carries the reading, and the
    /// phrase then moves each surface's tab as the switch route does (#803).
    /// </summary>
    [Fact]
    public void APhraseAppliedToEverySurfaceIsCarriedOnceAndMovesEachTab()
    {
        var (window, headset, _) = Mirrored();
        var navigators = new[] { window, headset };

        Assert.True(headset.Select(PanelTab.Commander));
        Assert.True(headset.Drill(new NavCrumb("item", "An item")));

        var said = navigators.Select(nav => PanelPhrases.Apply("technical", nav)).ToList();

        Assert.Equal(["Technical.", "Technical."], said);
        Assert.Equal(Technical, headset.RootKeyOf(PanelTab.Transcript));
        Assert.Equal(PanelTab.Transcript, headset.Tab);
        Assert.Null(PanelPhrases.Apply("technical", headset));
    }

    /// <summary>The switch route likewise.</summary>
    [Fact]
    public void ASwitchFlipIsCarriedByTheMirrorAndTheTabByTheLoop()
    {
        var (window, headset, _) = Mirrored();

        Assert.True(headset.Select(PanelTab.Commander));

        Assert.True(window.Show(Log));

        Assert.Equal(Log, headset.RootKeyOf(PanelTab.Transcript));
        Assert.Equal(PanelTab.Commander, headset.Tab);

        Assert.True(headset.Show(Log));

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

    /// <summary>
    /// The Commander's words: "switching to a tab in the main window should ALWAYS affect the
    /// mini-panel — IFF that tab is present on the mini panel."
    /// </summary>
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

    /// <summary>The IFF, and it costs no special case.</summary>
    [Fact]
    public void ATabTheMiniPanelDoesNotHaveIsSimplyNotCarried()
    {
        var (window, mini, _) = Following();

        mini.Select(PanelTab.Commander);

        Assert.True(window.Select(PanelTab.Settings));

        Assert.Equal(PanelTab.Settings, window.Tab);
        Assert.Equal(PanelTab.Commander, mini.Tab);
    }

    /// <summary>One-way, which is the half that protects a Commander in a headset.</summary>
    [Fact]
    public void TheMiniPanelDoesNotDragTheWindow()
    {
        var (window, mini, _) = Following();

        Assert.True(mini.Select(PanelTab.Commander));

        Assert.Equal(PanelTab.Transcript, window.Tab);
    }

    [Fact]
    public void AFollowerKeepsWhereItWasPutUntilTheWindowMovesAgain()
    {
        var (window, mini, _) = Following();

        window.Select(PanelTab.Commander);
        mini.Select(PanelTab.Transcript);

        Assert.Equal(PanelTab.Transcript, mini.Tab);
        Assert.Equal(PanelTab.Commander, window.Tab);

        // The window moving again leads it back, which is the "until".
        window.Select(PanelTab.Transcript);
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

    /// <summary>A follower without the window's root keeps the tab and the root it is on.</summary>
    [Fact]
    public void AFollowerWithoutTheRootGoesAsFarAsTheTab()
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

    /// <summary>A chooser or a dialog opened in the window does not open in the headset.</summary>
    [Fact]
    public void AChooserOrADialogStaysOnTheSurfaceThatOpenedIt()
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
        Assert.True(window.Drill(new NavCrumb("dialog:1", "Confirm") { Local = true }));

        Assert.Equal(["checklist", "item"], mini.Trail.Select(crumb => crumb.Key));
        Assert.Equal(0, miniChanges);
    }

    /// <summary>
    /// The headset moving the transcript reaches the window, and is not then mistaken for a move the
    /// window made.
    /// </summary>
    [Fact]
    public void AFollowersTranscriptMoveIsNotCarriedBackAsTheWindows()
    {
        var (window, mini, _) = Following();

        Assert.True(mini.Select(PanelTab.Commander));
        Assert.True(mini.SelectRoot(PanelTab.Transcript, Log));

        Assert.Equal(Log, window.RootKeyOf(PanelTab.Transcript));

        // A change to a tab the window is not showing is not a move of the window's.
        Assert.True(window.Drill(PanelTab.Commander, new NavCrumb("item", "An item")));

        Assert.Equal(PanelTab.Commander, mini.Tab);
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
