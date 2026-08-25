using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The window leads and the flat overlay follows (change-requests.md 34, and list.md Phase 48).
/// <para>
/// Asked 2026-08-24: <em>"How do I get it to show a different tab? I would have thought that it
/// would track with whatever is the main window's tab."</em> It does — <b>for the tabs it has</b>,
/// which is the transcript and the story and nothing else. These are that sentence, both halves,
/// because the half that does nothing is the half that looks broken.
/// </para>
/// </summary>
public class TheOverlayFollowsTheWindowTests
{
    [AvaloniaFact]
    public void TheWindowsTabCarriesToASurfaceThatHasIt()
    {
        var (window, follower) = Pair(stories: true);

        window.Tab = PanelTab.Adventures;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Adventures, follower.Tab);

        window.Tab = PanelTab.Transcript;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Transcript, follower.Tab);
    }

    /// <summary>
    /// And a tab the follower has not got moves the window alone rather than blanking the strip.
    /// <b>This is the whole of the answer to "why does nothing happen".</b>
    /// </summary>
    [AvaloniaFact]
    public void ATabTheFollowerHasNotGotMovesTheWindowAlone()
    {
        var (window, follower) = Pair(stories: true);

        window.Tab = PanelTab.Adventures;
        Dispatcher.UIThread.RunJobs();

        window.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Checklist, window.Tab);
        Assert.Equal(PanelTab.Adventures, follower.Tab);
    }

    /// <summary>
    /// A follower with no story furnished has one tab, so the window's tab never moves it at all —
    /// which is what a Commander sees before they have accepted an adventure.
    /// </summary>
    [AvaloniaFact]
    public void AFollowerWithOneTabNeverMoves()
    {
        var (window, follower) = Pair(stories: false);

        window.Tab = PanelTab.Adventures;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Adventures, window.Tab);
        Assert.Equal(PanelTab.Transcript, follower.Tab);
    }

    /// <summary>
    /// And the reading within the transcript is shared unconditionally, in both directions — which
    /// is Phase 45 rather than the tab rule, and is why "it tracks" and "it does not track" are
    /// both true depending on what is being changed.
    /// </summary>
    [AvaloniaFact]
    public void TheTranscriptsReadingIsSharedEitherWay()
    {
        var (window, follower) = Pair(stories: false);

        window.Page = TranscriptPage.Technical;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.Technical, follower.Page);

        follower.Page = TranscriptPage.Conversation;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.Conversation, window.Page);
    }

    /// <summary>
    /// The window's furnishing and the overlay's, wired through the same mirror the app wires them
    /// through — the leader flag included, since that is the thing under test.
    /// </summary>
    private static (PanelView Window, PanelView Follower) Pair(bool stories)
    {
        var model = new PanelViewModel();

        var window = new PanelView { DataContext = model };
        var follower = new PanelView { DataContext = model, Mode = PanelMode.Mini };

        window.EnableSettings(() => new TextBlock { Text = "settings" });
        window.Furnish(
            PanelTab.Checklist, _ => new TextBlock { Text = "checklist" },
            new NavCrumb("checklist", "Checklist"));

        var adventures = AdventureFixture.Surface();

        window.EnableAdventures(adventures);

        if (stories)
        {
            follower.EnableAdventures(adventures);
        }

        var mirror = new TranscriptMirror();

        mirror.Lead(window.Nav);
        mirror.Add(follower.Nav);

        return (window, follower);
    }
}
