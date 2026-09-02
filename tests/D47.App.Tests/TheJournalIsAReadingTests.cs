using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The journal as a reading in the Transcript (https://github.com/dseelinger/d47/issues/51).
/// </summary>
public sealed class TheJournalIsAReadingTests
{
    private static JournalEvent Event(string kind, string json, int second = 0) =>
        new(new DateTimeOffset(2026, 8, 27, 12, 3, second, TimeSpan.Zero),
            kind,
            JsonDocument.Parse(json).RootElement);

    private static (PanelView Panel, Window Window) Shown(bool rawJournal, JournalLog? log = null)
    {
        var model = new PanelViewModel();
        var kept = log ?? Filled();

        model.JournalSource = noise => kept.Read(noise);
        model.JournalDocumentSource = noise => kept.Document(noise);

        var panel = new PanelView { DataContext = model };

        if (rawJournal)
        {
            panel.EnableRawJournal();
        }

        var window = new Window { Content = panel, Width = 1180, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }

    private static JournalLog Filled()
    {
        var log = new JournalLog();

        log.Add([
            Event("FSDTarget", """{"event":"FSDTarget","Name":"Kusauts","StarClass":"K"}""", 1),
            Event("ShipLocker", """{"event":"ShipLocker"}""", 2),
            Event("Docked", """{"event":"Docked","StationName":"Jameson Memorial"}""", 3),
        ]);

        return log;
    }

    private static ListBox List(PanelView panel) =>
        panel.GetVisualDescendants().OfType<ListBox>().Single(box => box.Name == "JournalList");

    /// <summary>
    /// A surface nobody furnished the raw reading on has only the sentences — which is now the
    /// only thing that gates it, since the headset is furnished too (#231).
    /// </summary>
    [AvaloniaFact]
    public void ASurfaceWithoutTheRawOneHasOnlyTheSentences()
    {
        var (panel, window) = Shown(rawJournal: false);

        var words = panel.Nav.Roots(PanelTab.Transcript).Select(crumb => crumb.Word).ToList();

        Assert.Contains("Journal File", words);
        Assert.DoesNotContain("Raw Journal", words);

        window.Close();
    }

    /// <summary>
    /// A furnished surface has both, and the raw one is deliberately <em>not</em> in the picker.
    /// <para>
    /// <b>Both halves matter.</b> It stays a registered root so a spoken "raw journal" and a
    /// switch position that names it still arrive; it is kept out of the drop-down because it is
    /// the same events as the reading above it, seen another way, and two entries read as two
    /// subjects. The toggle beside the box is how a Commander crosses between them (#231).
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheRawReadingIsARootButNotAnEntryInThePicker()
    {
        var (panel, window) = Shown(rawJournal: true);

        var words = panel.Nav.Roots(PanelTab.Transcript).Select(crumb => crumb.Word).ToList();

        Assert.Contains("Journal File", words);
        Assert.Contains("Raw Journal", words);

        panel.Page = TranscriptPage.Journal;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var offered = panel.GetControl<Avalonia.Controls.ComboBox>("ModeBox").ItemsSource
            as IReadOnlyList<string> ?? [];

        Assert.Contains("Journal File", offered);
        Assert.DoesNotContain("Raw Journal", offered);

        // The box rather than the switch: the label sits beside the knob and the two show and
        // hide together, so the box is what carries the visibility.
        Assert.True(
            panel.GetControl<Avalonia.Controls.StackPanel>("RawToggleBox").IsVisible,
            "the toggle is the only way across, so it has to be on the journal reading");

        window.Close();
    }

    /// <summary>
    /// The toggle crosses between the two, and says which one is showing.
    /// </summary>
    [AvaloniaFact]
    public void TheToggleCrossesBetweenTheTwoReadings()
    {
        var (panel, window) = Shown(rawJournal: true);

        panel.Page = TranscriptPage.Journal;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var toggle = panel.GetControl<Avalonia.Controls.Primitives.ToggleButton>("RawToggle");

        Assert.False(toggle.IsChecked);

        toggle.IsChecked = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.RawJournal, panel.Page);

        toggle.IsChecked = false;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.Journal, panel.Page);

        window.Close();
    }

    /// <summary>
    /// The surviving readings keep their internal keys. They are cited from settings, tests and
    /// the keyword router, and the renames touched only the word that is drawn and said.
    /// <para>
    /// <b>Details is gone with them</b> (#231): it was never a big enough differentiator once the
    /// log file became a reading of its own. Its key is asserted <em>absent</em> rather than left
    /// unmentioned, because a stored root that no longer resolves is exactly how a Commander
    /// would land on a blank page — SelectRoot declines a root nobody registered, so they fall
    /// back to the conversation instead.
    /// </para>
    /// <para>
    /// <b>Asked through the key rather than through the enum</b> since #260 deleted
    /// <c>TranscriptPage.Technical</c>. That is the road a stored reading actually takes — a
    /// string out of a settings file or a switch position — so this now exercises the fallback
    /// as it happens rather than through a type that can no longer express the fault.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheSurvivingReadingsKeepTheirKeysAndDetailsIsGone()
    {
        var (panel, window) = Shown(rawJournal: true);

        var keys = panel.Nav.Roots(PanelTab.Transcript).Select(crumb => crumb.Key).ToList();

        Assert.Contains("transcript.conversation", keys);
        Assert.Contains("transcript.log", keys);
        Assert.Contains("transcript.journal", keys);
        Assert.DoesNotContain("transcript.technical", keys);

        // And a Commander whose stored reading was Details lands on the conversation rather than
        // on nothing at all.
        panel.Page = TranscriptPage.Log;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(panel.Nav.SelectRoot(PanelTab.Transcript, "transcript.technical"));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.Log, panel.Page);

        window.Close();
    }

    /// <summary>Opening the reading lists what the log holds, newest first and without the noise.</summary>
    [AvaloniaFact]
    public void OpeningItListsWhatHappened()
    {
        var (panel, window) = Shown(rawJournal: true);

        panel.Page = TranscriptPage.Journal;
        Dispatcher.UIThread.RunJobs();

        var lines = List(panel).ItemsSource!.Cast<string>().ToList();

        Assert.Equal(2, lines.Count);
        Assert.Contains("Jameson Memorial", lines[0]);
        Assert.DoesNotContain(lines, line => line.Contains("ShipLocker", StringComparison.Ordinal));

        window.Close();
    }

    /// <summary>
    /// The detail pane is the fields of the selected line — the half that cannot be wrong, which is
    /// why a list with no detail was rejected as "lossy with no way back to the fields".
    /// </summary>
    [AvaloniaFact]
    public void TheFieldsOfTheChosenLineAreShown()
    {
        var (panel, window) = Shown(rawJournal: true);

        panel.Page = TranscriptPage.Journal;
        Dispatcher.UIThread.RunJobs();

        var model = (PanelViewModel)panel.DataContext!;

        Assert.Contains("Jameson Memorial", model.JournalDetailText);

        List(panel).SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Kusauts", model.JournalDetailText);

        window.Close();
    }

    /// <summary>
    /// The noise toggle rebuilds rather than hides: the filter belongs to the log, and applying it
    /// on the way in would make it unswitchable.
    /// </summary>
    [AvaloniaFact]
    public void AskingForTheNoiseShowsIt()
    {
        var (panel, window) = Shown(rawJournal: true);

        panel.Page = TranscriptPage.Journal;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, List(panel).ItemsSource!.Cast<string>().Count());

        panel.ShowJournalNoise(true);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, List(panel).ItemsSource!.Cast<string>().Count());

        window.Close();
    }

    /// <summary>
    /// And the detail pane can be put away, which is what keeps the reading usable in one narrow
    /// column — the Commander's own amendment to the design.
    /// </summary>
    [AvaloniaFact]
    public void TheFieldsCanBePutAway()
    {
        var (panel, window) = Shown(rawJournal: true);

        panel.Page = TranscriptPage.Journal;
        Dispatcher.UIThread.RunJobs();

        var detail = panel.GetVisualDescendants().OfType<ScrollViewer>()
            .Single(view => view.Name == "JournalDetailScroller");

        Assert.True(detail.IsVisible);

        panel.ShowJournalDetail(false);
        Dispatcher.UIThread.RunJobs();

        Assert.False(detail.IsVisible);

        window.Close();
    }
    /// <summary>
    /// <b>The two readings are different readings.</b> Reported after 0.81.0 shipped: they looked
    /// identical, because they shared one pane and differed only in column widths — so both showed
    /// sentences and both showed the same pretty-printed fields, and the raw one was not raw.
    /// </summary>
    [AvaloniaFact]
    public void TheRawReadingIsTheFileAndTheOtherIsNot()
    {
        var (panel, window) = Shown(rawJournal: true);

        panel.Page = TranscriptPage.Journal;
        Dispatcher.UIThread.RunJobs();

        // Journal is the list-and-fields pane.
        Assert.True(panel.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "JournalPane").IsVisible);

        panel.Page = TranscriptPage.RawJournal;
        Dispatcher.UIThread.RunJobs();

        // Raw Journal is not. It is a document, drawn where the log file is drawn.
        Assert.False(panel.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "JournalPane").IsVisible);

        var model = (PanelViewModel)panel.DataContext!;

        // One event per line, as the file holds them - not indented, and not a sentence.
        Assert.Contains("{\"event\":\"Docked\"", model.JournalRawText);
        Assert.DoesNotContain("  \"event\"", model.JournalRawText);
        Assert.DoesNotContain("Undocked from", model.JournalRawText);

        window.Close();
    }

    /// <summary>
    /// And it never goes through the markup parser. A journal carries other players' text verbatim
    /// and JSON full of asterisks, so a Commander who types <c>**</c> must see <c>**</c> — and must
    /// not be able to dress their message up as one of d47's own lines.
    /// </summary>
    [AvaloniaFact]
    public void TheRawReadingIsNotParsedAsMarkup()
    {
        var log = new JournalLog();

        log.Add([Event("ReceiveText", """{"event":"ReceiveText","Message":"**not bold**"}""")]);

        var (panel, window) = Shown(rawJournal: true, log);

        panel.Page = TranscriptPage.RawJournal;
        Dispatcher.UIThread.RunJobs();

        var drawn = panel.GetVisualDescendants().OfType<SelectableTextBlock>()
            .Single(block => block.Name == "Transcript");

        Assert.Contains("**not bold**", drawn.Inlines!.Text);

        window.Close();
    }
}
