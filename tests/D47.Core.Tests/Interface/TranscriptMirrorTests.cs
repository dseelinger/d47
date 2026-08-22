using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>
/// One transcript page across both surfaces (list.md Phase 45, "One transcript, both surfaces").
/// <para>
/// Two real navigators rather than a window and a headset, because the rule is arithmetic over
/// navigators: the Transcript root is one choice, tabs and trails are not, the echo stops because
/// the code says so, and a surface whose chooser refused the move is behind rather than a second
/// source. None of that needs a surface to be true, and the two surfaces furnish their navigators
/// through one constructor, so two navigators furnished the same way <em>is</em> the case.
/// </para>
/// </summary>
public class TranscriptMirrorTests
{
    private const string Conversation = "transcript.conversation";
    private const string Technical = "transcript.technical";
    private const string Log = "transcript.log";

    /// <summary>A surface as <c>PanelView</c> furnishes it: all three transcript roots, and a tab to be elsewhere on.</summary>
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

    /// <summary>
    /// The Commander's words: the window's selection is echoed in VR and vice-versa. Either
    /// direction, no preferred surface.
    /// </summary>
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

    /// <summary>
    /// What you are reading is shared; where you are is not. The window going to the checklist,
    /// and drilling into it, leaves the headset where it was — and the transcript still follows
    /// while the surface that moved it is on another tab entirely.
    /// </summary>
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

    /// <summary>
    /// A changes, B is set, B announces, A must not be set again. One <c>Changed</c> per surface
    /// per move, and the handler that would re-enter is the mirror's own.
    /// </summary>
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

        // And a move the headset would have declined anyway — it is already there — is nothing,
        // not a second round.
        Assert.False(headset.SelectRoot(PanelTab.Transcript, Technical));

        Assert.Equal(1, windowChanges);
        Assert.Equal(1, headsetChanges);
    }

    /// <summary>
    /// A chooser holds the headset while the window moves. The headset is behind, not a second
    /// source: dismissing the chooser brings it level rather than dragging the window back.
    /// </summary>
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
    /// A surface built after the Commander has already moved the other arrives agreeing rather
    /// than a step behind.
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
    /// The spoken route is an initiator, not a second mechanism. The host applies a phrase to
    /// every navigator; "technical" is taken by the window at its root, the mirror has moved the
    /// headset before the loop reaches it, and the headset — three levels into a checklist and
    /// answering nothing — is reading Technical when it comes back.
    /// </summary>
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

    /// <summary>
    /// The switch route likewise. <see cref="PanelNavigator.Show"/> on the first surface moves the
    /// second's root through the mirror; arriving on the Transcript <em>tab</em> is still the loop's
    /// job, and the second surface takes that part and declines the rest.
    /// </summary>
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
}
