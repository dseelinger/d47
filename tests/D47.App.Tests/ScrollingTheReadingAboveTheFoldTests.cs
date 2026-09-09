using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Ctrl+L scrolls the reading above the top of the view, on every one of the four readings, and deletes
/// nothing.
/// </summary>
public class ScrollingTheReadingAboveTheFoldTests : IDisposable
{
    private readonly string _room =
        Directory.CreateTempSubdirectory("d47-fold-").FullName;

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            Directory.Delete(_room, recursive: true);
        }
        catch (IOException)
        {
        // A temporary directory that outlives the run costs nobody anything.
        }
    }

    private sealed record Fixture(
        PanelView Panel,
        PanelViewModel Model,
        Window Window,
        string LogFile,
        string JournalFile);

    private static JournalEvent Event(string kind, string json, int second) =>
        new(new DateTimeOffset(2026, 9, 7, 12, 3, second, TimeSpan.Zero),
            kind,
            JsonDocument.Parse(json).RootElement);

    /// <summary>A panel with more in every reading than fits the view, and real files behind the two that are files.</summary>
    /// <param name="reading">Where the log page reads from, for the one test that needs a reading which trims its front rather than a file that sits still.</param>
    private Fixture Said(bool searchable = true, Func<string>? reading = null)
    {
        var model = new PanelViewModel();

        model.Append("Fixture One, docked.\n");

        for (var line = 0; line < 60; line++)
        {
            model.Append($"Ship line {line}, holding station.\n");
        }

        var log = Path.Combine(_room, "d47.log");
        var journal = Path.Combine(_room, "journal.log");
        var kept = new JournalLog();

        kept.Add([.. Enumerable.Range(0, 60).Select(second =>
            Event("Docked", $$"""{"event":"Docked","StationName":"Berth {{second}}"}""", second))]);

        File.WriteAllText(
            log,
            string.Join('\n', Enumerable.Range(0, 60).Select(line => $"log line {line}")));

        File.WriteAllText(journal, kept.Document(true));

        model.LogSource = reading ?? (() => File.ReadAllText(log));
        model.JournalSource = noise => kept.Read(noise);
        model.JournalDocumentSource = _ => File.ReadAllText(journal);

        var panel = new PanelView { DataContext = model };
        var window = new Window { Content = panel, Width = 900, Height = 600 };

        // The raw journal is furnished, because it is one of the four readings the press has to work on and
        // an unfurnished surface cannot be sent to it at all.
        panel.EnableRawJournal();
        window.Show();

        if (searchable)
        {
            panel.EnableSearch();
        }

        Dispatcher.UIThread.RunJobs();

        return new Fixture(panel, model, window, log, journal);
    }

    /// <summary>Waits for the reading to be on the page.</summary>
    private static async Task Settled(Fixture fixture)
    {
        if (fixture.Panel.Page == TranscriptPage.Log)
        {
            for (var tries = 0;
                 tries < 200 && !fixture.Model.LogText.Contains("log line 59", StringComparison.Ordinal);
                 tries++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static void ControlL(PanelView panel)
    {
        panel.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.L,
            KeyModifiers = KeyModifiers.Control,
        });

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Every reading, asserted through the drawn panel, which is the only place the answer lives.</summary>
    [AvaloniaTheory]
    [InlineData(TranscriptPage.Conversation)]
    [InlineData(TranscriptPage.Log)]
    [InlineData(TranscriptPage.Journal)]
    [InlineData(TranscriptPage.RawJournal)]
    public async Task ControlLLeavesTheViewEmpty(TranscriptPage page)
    {
        var fixture = Said();

        fixture.Panel.Page = page;
        await Settled(fixture);

        Assert.Equal(page, fixture.Panel.Page);
        Assert.False(fixture.Panel.ReadingIsAboveTheFold, "the reading is on screen to begin with");

        ControlL(fixture.Panel);

        Assert.True(fixture.Panel.ReadingIsAboveTheFold);
    }

    /// <summary>The menu does the same act as the key, which is what makes the shortcut on it true.</summary>
    [AvaloniaFact]
    public void TheMenuScrollsItToo()
    {
        var fixture = Said();

        Assert.True(fixture.Panel.ScrollPastReading());
        Dispatcher.UIThread.RunJobs();

        Assert.True(fixture.Panel.ReadingIsAboveTheFold);
    }

    /// <summary>
 /// What keeps the gesture from losing anything: the text is above the fold, not gone, so scrolling back up
    /// brings it into view — and every run is still in the model underneath.
    /// </summary>
    [AvaloniaFact]
    public void ScrollingBackUpShowsItAgain()
    {
        var fixture = Said();

        var shown = fixture.Panel.TranscriptShown;

        ControlL(fixture.Panel);

        Assert.True(fixture.Panel.ReadingIsAboveTheFold);

        Assert.Equal(PanelScrollOutcome.Moved, fixture.Panel.Scroll(PanelScrollStep.PageUp));
        Dispatcher.UIThread.RunJobs();

        Assert.False(fixture.Panel.ReadingIsAboveTheFold);

        // And it is the same text it was: the record was never the thing being scrolled.
        Assert.Equal(shown, fixture.Panel.TranscriptShown);
        Assert.Contains("Fixture One", fixture.Model.TranscriptText, StringComparison.Ordinal);
        Assert.NotEmpty(fixture.Model.Segments(TranscriptPage.Conversation));
    }

    /// <summary>The record d47 keeps of the conversation is left alone.</summary>
    [AvaloniaFact]
    public void TheRunsUnderneathStay()
    {
        var fixture = Said();

        ControlL(fixture.Panel);

        Assert.Contains(fixture.Model.Segments(TranscriptPage.Conversation),
            segment => segment.Text.Length > 0);
        Assert.Contains("Fixture One", fixture.Model.TranscriptText, StringComparison.Ordinal);
    }

    /// <summary>And the files on disk are files d47 only reads, before the press and after it.</summary>
    [AvaloniaTheory]
    [InlineData(TranscriptPage.Log)]
    [InlineData(TranscriptPage.Journal)]
    [InlineData(TranscriptPage.RawJournal)]
    public async Task TheFilesOnDiskAreUntouched(TranscriptPage page)
    {
        var fixture = Said();

        fixture.Panel.Page = page;
        await Settled(fixture);

        var log = File.ReadAllBytes(fixture.LogFile);
        var journal = File.ReadAllBytes(fixture.JournalFile);

        ControlL(fixture.Panel);

        Assert.True(fixture.Panel.ReadingIsAboveTheFold);
        Assert.Equal(log, File.ReadAllBytes(fixture.LogFile));
        Assert.Equal(journal, File.ReadAllBytes(fixture.JournalFile));
    }

    /// <summary>
    /// New content lands in the emptied space, as it does in a terminal, rather than pushing the old
    /// view back into sight.
    /// </summary>
    [AvaloniaFact]
    public void ALineAppendedAfterwardsAppearsInTheEmptyView()
    {
        var fixture = Said();
        var scroller = fixture.Panel.GetControl<ScrollViewer>("TranscriptScroller");

        ControlL(fixture.Panel);

        var fold = scroller.Offset.Y;

        fixture.Model.Append("The next thing the ship said.\n");
        Dispatcher.UIThread.RunJobs();

        // In view, because it arrived below the fold line.
        Assert.False(fixture.Panel.ReadingIsAboveTheFold);

        // And the fold has not moved, so what was there before is still above it.
        Assert.Equal(fold, scroller.Offset.Y, 1);
    }

    /// <summary>
 /// And on a reading that trims its front, which is the one a Commander would use this on.
    /// </summary>
    [AvaloniaFact]
    public async Task LinesArriveOnAReadingThatTrimsItsFront()
    {
        var lines = new List<string>(
            Enumerable.Range(0, 60).Select(line => $"log line {line}"));

        // The last sixty, whatever has been written: LogTail's own cap, in miniature.
        var fixture = Said(reading: () => string.Join('\n', lines.TakeLast(60)));

        fixture.Panel.Page = TranscriptPage.Log;
        await Settled(fixture);

        ControlL(fixture.Panel);

        Assert.True(fixture.Panel.ReadingIsAboveTheFold);

        for (var tick = 0; tick < 10; tick++)
        {
            lines.Add($"log line {60 + tick}");
            await fixture.Panel.RefreshLogNow();
            Dispatcher.UIThread.RunJobs();
        }

        Assert.False(
            fixture.Panel.ReadingIsAboveTheFold,
            "ten lines arrived after the fold and none of them is on screen");
    }

    /// <summary>
    /// It works on a surface with no search box, because folding the page is not a search affordance —
    /// the headset is exactly that surface.
    /// </summary>
    [AvaloniaFact]
    public void ItWorksWhereThereIsNoSearch()
    {
        var fixture = Said(searchable: false);

        ControlL(fixture.Panel);

        Assert.True(fixture.Panel.ReadingIsAboveTheFold);
    }

 /// <summary>Why In Ship looked broken, and the press that now works.</summary>
    [AvaloniaFact]
    public void TheFirstPressFromAColdWindowFoldsTheReading()
    {
        var fixture = Said();

        Assert.Null(fixture.Window.FocusManager?.GetFocusedElement());

        fixture.Window.KeyPress(Key.L, RawInputModifiers.Control, PhysicalKey.L, "l");
        Dispatcher.UIThread.RunJobs();

        Assert.True(fixture.Panel.ReadingIsAboveTheFold);

        // And with the caret in the ask box: the same act, so the second press is not a different gesture from the first.
        Assert.Equal(PanelScrollOutcome.Moved, fixture.Panel.Scroll(PanelScrollStep.PageUp));
        fixture.Panel.FocusAsk();
        Dispatcher.UIThread.RunJobs();

        fixture.Window.KeyPress(Key.L, RawInputModifiers.Control, PhysicalKey.L, "l");
        Dispatcher.UIThread.RunJobs();

        Assert.True(fixture.Panel.ReadingIsAboveTheFold);
    }

    /// <summary>
    /// And it leaves the chord alone where there is no reading to fold, so the window's <c>Focus the
 /// ask box</c> hotkey still has somewhere to be.
    /// </summary>
    [AvaloniaFact]
    public void ATabWithNoReadingOnItDoesNotTakeTheChord()
    {
        var fixture = Said();

        fixture.Panel.Furnish(
            PanelTab.Checklist,
            _ => new TextBlock { Text = "checklist" },
            new NavCrumb("checklist", "Checklist"));

        fixture.Panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        Assert.NotEqual(PanelTab.Transcript, fixture.Panel.Tab);
        Assert.False(fixture.Panel.ScrollPastReading());
    }

 /// <summary>Offered on all four readings.</summary>
    [AvaloniaTheory]
    [InlineData(TranscriptPage.Conversation)]
    [InlineData(TranscriptPage.Log)]
    [InlineData(TranscriptPage.Journal)]
    [InlineData(TranscriptPage.RawJournal)]
    public void TheMenuItemIsOfferedOnEveryReading(TranscriptPage page)
    {
        var fixture = Said();

        fixture.Panel.Page = page;
        Dispatcher.UIThread.RunJobs();

        Assert.True(fixture.Panel.GetControl<MenuItem>("ScrollPastReadingItem").IsEnabled);
    }

    /// <summary>
    /// The menu item exists, says the shortcut out loud, and no longer promises to empty anything.
    /// </summary>
    [AvaloniaFact]
    public void TheMenuSaysWhatItDoes()
    {
        var fixture = Said();

        var item = Menu(fixture.Panel).Single(entry => entry.Name == "ScrollPastReadingItem");

        Assert.Equal("Scroll what is shown above the fold", item.Header);
        Assert.Equal("Ctrl+L", item.InputGesture?.ToString());
    }

 /// <summary>And Copy is on it.</summary>
    [AvaloniaFact]
    public void TheMenuCopiesTheSelection()
    {
        var fixture = Said();

        var copy = Menu(fixture.Panel).Single(entry => entry.Name == "CopySelectionItem");

        Assert.Equal("Copy", copy.Header);
        Assert.Equal("Ctrl+C", copy.InputGesture?.ToString());

        // It comes first: one reads the page and the other moves it, and reading is what a hand lands on
        // first.
        Assert.True(
            Menu(fixture.Panel).ToList().IndexOf(copy)
            < Menu(fixture.Panel).ToList().FindIndex(entry => entry.Name == "ScrollPastReadingItem"));
    }

 /// <summary>Greyed with nothing selected, and lit by a selection.</summary>
    [AvaloniaFact]
    public void CopyFollowsTheSelection()
    {
        var fixture = Said();

        // Whichever block the page is drawn in.
        var transcript = fixture.Panel.TranscriptBlocks[0];
        var copy = Menu(fixture.Panel).Single(entry => entry.Name == "CopySelectionItem");

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
