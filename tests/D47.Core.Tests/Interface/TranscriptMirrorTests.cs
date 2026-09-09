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
        nav.Register(PanelTab.Checklist, new NavCrumb("checklist", "Checklist"));

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

        Assert.True(window.Select(PanelTab.Checklist));
        Assert.True(window.Drill(new NavCrumb("item", "An item")));

        Assert.Equal(PanelTab.Transcript, headset.Tab);
        Assert.True(headset.AtRoot);

        // The window opens the log file from its menu without leaving the checklist.
        Assert.True(window.SelectRoot(PanelTab.Transcript, Log));

        Assert.Equal(Log, headset.RootKeyOf(PanelTab.Transcript));
        Assert.Equal(PanelTab.Transcript, headset.Tab);
        Assert.Equal(PanelTab.Checklist, window.Tab);
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

    /// <summary>The spoken route is an initiator, not a second mechanism.</summary>
    [Fact]
    public void APhraseAppliedToEverySurfaceIsCarriedOnceAndDeclinedOnce()
    {
        var (window, headset, _) = Mirrored();
        var navigators = new[] { window, headset };

        Assert.True(headset.Select(PanelTab.Checklist));
        Assert.True(headset.Drill(new NavCrumb("item", "An item")));

        var said = navigators.Select(nav => PanelPhrases.Apply("technical", nav)).ToList();

        Assert.Equal(["Technical.", null], said);
        Assert.Equal(Technical, headset.RootKeyOf(PanelTab.Transcript));
        Assert.Equal(PanelTab.Checklist, headset.Tab);
    }

    /// <summary>The switch route likewise.</summary>
    [Fact]
    public void ASwitchFlipIsCarriedByTheMirrorAndTheTabByTheLoop()
    {
        var (window, headset, _) = Mirrored();

        Assert.True(headset.Select(PanelTab.Checklist));

        Assert.True(window.Show(Log));

        Assert.Equal(Log, headset.RootKeyOf(PanelTab.Transcript));
        Assert.Equal(PanelTab.Checklist, headset.Tab);

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

        Assert.True(window.Select(PanelTab.Checklist));

        Assert.Equal(PanelTab.Checklist, mini.Tab);
    }

    /// <summary>
    /// And the view of the tab with it, which is the other half of the request: the window changing
    /// only which root of a tab it is on still carries.
    /// </summary>
    [Fact]
    public void TheViewOfTheTabCarriesToo()
    {
        var (window, mini, _) = Following();

        window.Select(PanelTab.Checklist);
        window.Select(PanelTab.Transcript);
        Assert.True(window.SelectRoot(PanelTab.Transcript, Log));

        Assert.Equal(Log, mini.RootKeyOf(PanelTab.Transcript));
    }

    /// <summary>The IFF, and it costs no special case.</summary>
    [Fact]
    public void ATabTheMiniPanelDoesNotHaveIsSimplyNotCarried()
    {
        var (window, mini, _) = Following();

        mini.Select(PanelTab.Checklist);

        Assert.True(window.Select(PanelTab.Settings));

        Assert.Equal(PanelTab.Settings, window.Tab);
        Assert.Equal(PanelTab.Checklist, mini.Tab);
    }

    /// <summary>One-way, which is the half that protects a Commander in a headset.</summary>
    [Fact]
    public void TheMiniPanelDoesNotDragTheWindow()
    {
        var (window, mini, _) = Following();

        Assert.True(mini.Select(PanelTab.Checklist));

        Assert.Equal(PanelTab.Transcript, window.Tab);
    }

    [Fact]
    public void AFollowerKeepsWhereItWasPutUntilTheWindowMovesAgain()
    {
        var (window, mini, _) = Following();

        window.Select(PanelTab.Checklist);
        mini.Select(PanelTab.Transcript);

        Assert.Equal(PanelTab.Transcript, mini.Tab);
        Assert.Equal(PanelTab.Checklist, window.Tab);

        // The window moving again leads it back, which is the "until".
        window.Select(PanelTab.Transcript);
        window.Select(PanelTab.Checklist);

        Assert.Equal(PanelTab.Checklist, mini.Tab);
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
