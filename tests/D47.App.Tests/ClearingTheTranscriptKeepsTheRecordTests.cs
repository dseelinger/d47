using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Clearing the page, and only the page (remediation.md 11, item 14).
/// <para>
/// Asked for as a context menu or Ctrl+L that clears the transcript text — "not necessarily the
/// record, just what is shown". Nothing here is the record: the model's own history lives in the
/// turn loop and is what a follow-up is answered against, and the log file on disk is Serilog's.
/// </para>
/// </summary>
public class ClearingTheTranscriptKeepsTheRecordTests
{
    private static (PanelView Panel, PanelViewModel Model) Said()
    {
        var model = new PanelViewModel();

        model.Append("Fixture One, docked.\n");

        var panel = new PanelView { DataContext = model };
        var window = new Window { Content = panel, Width = 900, Height = 600 };

        // The raw journal is furnished, because it is one of the readings that has to refuse and
        // an unfurnished surface cannot be sent to it at all.
        panel.EnableRawJournal();
        window.Show();
        panel.EnableSearch();
        Dispatcher.UIThread.RunJobs();

        return (panel, model);
    }

    private static string Shown(PanelView panel) => panel.TranscriptShown;

    [AvaloniaFact]
    public void TheMenuClearsWhatIsShown()
    {
        var (panel, _) = Said();

        Assert.Contains("Fixture One", Shown(panel), StringComparison.Ordinal);

        Assert.True(panel.ClearTranscript());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(string.Empty, Shown(panel));
    }

    [AvaloniaFact]
    public void TheRunsUnderneathGoWithIt()
    {
        var (panel, model) = Said();

        panel.ClearTranscript();
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(model.Segments(TranscriptPage.Conversation), segment => segment.Text.Length > 0);
        Assert.Equal(string.Empty, model.TranscriptText);
    }

    /// <summary>Ctrl+L, which is where a reader's hands already are.</summary>
    [AvaloniaFact]
    public void ControlLClearsIt()
    {
        var (panel, _) = Said();

        panel.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.L,
            KeyModifiers = KeyModifiers.Control,
        });

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(string.Empty, Shown(panel));
    }

    /// <summary>
    /// And it works on a surface with no search box, because clearing the page is not a search
    /// affordance — the headset is exactly that surface.
    /// </summary>
    [AvaloniaFact]
    public void ItWorksWhereThereIsNoSearch()
    {
        var model = new PanelViewModel();

        model.Append("Fixture One, docked.\n");

        var panel = new PanelView { DataContext = model };
        var window = new Window { Content = panel, Width = 900, Height = 600 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.L,
            KeyModifiers = KeyModifiers.Control,
        });

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(string.Empty, model.TranscriptText);
    }

    /// <summary>
    /// Every reading that is a file on disk refuses, and the conversation it is not showing is
    /// left alone (<a href="https://github.com/dseelinger/d47/issues/261">#261</a>).
    /// <para>
    /// <b>The log was the only one of these asserted, and the only one the code checked.</b> The
    /// two journal readings arrived after that rule was written and were never added to it, so
    /// pressing Clear on either emptied the conversation three doors away — and nothing on screen
    /// changed, because a journal reading is drawn from Elite's file. A Commander found out on
    /// going back to In Ship.
    /// </para>
    /// </summary>
    [AvaloniaTheory]
    [InlineData(TranscriptPage.Log)]
    [InlineData(TranscriptPage.Journal)]
    [InlineData(TranscriptPage.RawJournal)]
    public void AReadingThatIsAFileIsNotCleared(TranscriptPage page)
    {
        var (panel, model) = Said();

        model.LogSource = () => "today's log";
        panel.Page = page;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(page, panel.Page);
        Assert.False(panel.ClearTranscript());
        Assert.Contains("Fixture One", model.TranscriptText, StringComparison.Ordinal);
    }

    /// <summary>
    /// And Ctrl+L is refused there too, not merely the menu item (#261). It is the gesture a
    /// reader's hands reach for, so a fix that only covered the drawn control would leave the
    /// fault exactly where it was found.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(TranscriptPage.Log)]
    [InlineData(TranscriptPage.Journal)]
    [InlineData(TranscriptPage.RawJournal)]
    public void ControlLIsRefusedOnAReadingThatIsAFile(TranscriptPage page)
    {
        var (panel, model) = Said();

        model.LogSource = () => "today's log";
        panel.Page = page;
        Dispatcher.UIThread.RunJobs();

        panel.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.L,
            KeyModifiers = KeyModifiers.Control,
        });

        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Fixture One", model.TranscriptText, StringComparison.Ordinal);
    }

    /// <summary>
    /// The refusal is drawn as well as obeyed (#261) — greyed where it would do nothing, the way
    /// Copy beside it is greyed with nothing selected. A control that silently does nothing is
    /// indistinguishable from one that failed.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(TranscriptPage.Conversation, true)]
    [InlineData(TranscriptPage.Log, false)]
    [InlineData(TranscriptPage.Journal, false)]
    [InlineData(TranscriptPage.RawJournal, false)]
    public void TheMenuItemIsGreyedWhereItWouldRefuse(TranscriptPage page, bool enabled)
    {
        var (panel, _) = Said();

        panel.Page = page;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            enabled,
            panel.GetControl<Avalonia.Controls.MenuItem>("ClearTranscriptItem").IsEnabled);
    }

    /// <summary>The menu item exists, and says the shortcut out loud.</summary>
    [AvaloniaFact]
    public void TheMenuSaysTheShortcut()
    {
        var (panel, _) = Said();

        var item = Menu(panel).Single(entry => entry.Name == "ClearTranscriptItem");

        Assert.Equal("Clear what is shown", item.Header);
        Assert.Equal("Ctrl+L", item.InputGesture?.ToString());
    }

    /// <summary>
    /// And Copy is on it (remediation.md 14, item 9).
    /// <para>
    /// Declaring a menu replaces the one <c>SelectableTextBlock</c> comes with, so Copy left the
    /// place a reader looks for it when Clear arrived. Ctrl+C never stopped working; nothing said
    /// it did.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheMenuCopiesTheSelection()
    {
        var (panel, _) = Said();

        var copy = Menu(panel).Single(entry => entry.Name == "CopySelectionItem");

        Assert.Equal("Copy", copy.Header);
        Assert.Equal("Ctrl+C", copy.InputGesture?.ToString());

        // It comes before Clear: one reads the page and the other empties it, and the destructive
        // one is not what a hand lands on first.
        Assert.True(
            Menu(panel).ToList().IndexOf(copy)
            < Menu(panel).ToList().FindIndex(entry => entry.Name == "ClearTranscriptItem"));
    }

    /// <summary>
    /// Greyed with nothing selected, and lit by a selection (remediation.md 14, item 9).
    /// <para>
    /// A Copy that copies nothing is the same complaint as a search box on a page that cannot
    /// search. It follows the selection rather than the menu opening, because
    /// <c>ContextMenu.Opening</c> does not fire when the menu is opened in code — measured — so a
    /// rule hung there is one nothing can assert.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void CopyFollowsTheSelection()
    {
        var (panel, _) = Said();

        // Whichever block the page is drawn in. On the conversation that is the first bubble,
        // and a selection is made in one turn rather than across the page.
        var transcript = panel.TranscriptBlocks[0];
        var copy = Menu(panel).Single(entry => entry.Name == "CopySelectionItem");

        Assert.Empty(transcript.SelectedText ?? string.Empty);
        Assert.False(copy.IsEnabled);

        transcript.SelectionStart = 0;
        transcript.SelectionEnd = 7;
        Dispatcher.UIThread.RunJobs();

        Assert.NotEmpty(transcript.SelectedText ?? string.Empty);
        Assert.True(copy.IsEnabled, "a selection lights Copy");

        // And a selection collapsed by a click puts it back.
        transcript.SelectionEnd = 0;
        Dispatcher.UIThread.RunJobs();

        Assert.False(copy.IsEnabled);
    }

    private static IEnumerable<MenuItem> Menu(PanelView panel) =>
        panel.GetControl<SelectableTextBlock>("Transcript").ContextMenu!.Items.OfType<MenuItem>();
}
