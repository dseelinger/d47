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
    /// <b>The safety property.</b> Raw Journal is furnished by the window and by nothing else, so a
    /// surface nobody furnished has no such reading. A wall of JSON is there to be selected and
    /// pasted into a bug report, which is an act with no meaning in mid-air — and the headset gets
    /// the sentences, which are readable at a metre.
    /// </summary>
    [AvaloniaFact]
    public void TheHeadsetGetsTheJournalAndNotTheRawOne()
    {
        var (panel, window) = Shown(rawJournal: false);

        var words = panel.Nav.Roots(PanelTab.Transcript).Select(crumb => crumb.Word).ToList();

        Assert.Contains("Journal", words);
        Assert.DoesNotContain("Raw Journal", words);

        window.Close();
    }

    /// <summary>And the window, which furnished it, has both.</summary>
    [AvaloniaFact]
    public void TheWindowGetsBoth()
    {
        var (panel, window) = Shown(rawJournal: true);

        var words = panel.Nav.Roots(PanelTab.Transcript).Select(crumb => crumb.Word).ToList();

        Assert.Contains("Journal", words);
        Assert.Contains("Raw Journal", words);

        window.Close();
    }

    /// <summary>
    /// The three older readings keep their internal keys. They are cited from settings, tests and
    /// the keyword router, and this issue renamed only the word that is drawn and said.
    /// </summary>
    [AvaloniaFact]
    public void TheOlderReadingsKeepTheirKeys()
    {
        var (panel, window) = Shown(rawJournal: true);

        var keys = panel.Nav.Roots(PanelTab.Transcript).Select(crumb => crumb.Key).ToList();

        Assert.Contains("transcript.conversation", keys);
        Assert.Contains("transcript.technical", keys);
        Assert.Contains("transcript.log", keys);

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
}
