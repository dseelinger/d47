using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Transcript readings are named for what a Commander goes there to see rather than for what
/// they are made of (<a href="https://github.com/dseelinger/d47/issues/250">#250</a>).
/// <para>
/// <b>The half that is invisible on the screen it is about</b> is the spoken route. A crumb's
/// label doubles as the phrase that reaches it, so renaming one silently retires whatever the old
/// label was — and "conversation" is a word Commanders reach for. Asserted here rather than left
/// to be noticed in a cockpit.
/// </para>
/// </summary>
public sealed class TheReadingsAreNamedForTheCommanderTests
{
    private static (PanelView Panel, Window Window) Shown()
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = panel, Width = 1180, Height = 800 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }

    /// <summary>The three readings, as the drop-down offers them.</summary>
    [AvaloniaFact]
    public void ThePickerReadsInShipLogFileAndJournalFile()
    {
        var (panel, window) = Shown();

        var offered = panel.GetControl<ComboBox>("ModeBox").ItemsSource as IReadOnlyList<string> ?? [];

        Assert.Equal(["In Ship", "Log File", "Journal File"], offered);

        window.Close();
    }

    /// <summary>
    /// Both words reach the renamed reading: the label a Commander reads today, and the one they
    /// learnt a year ago. Said from the log file rather than from the conversation, because
    /// selecting the root already showing is refused — "you are there" would pass this test while
    /// proving nothing.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("in ship")]
    [InlineData("conversation")]
    [InlineData("thread")]
    public void SayingAnyOfItsWordsReachesTheShipsReading(string said)
    {
        var (panel, window) = Shown();

        PanelModes.Choose(panel, PanelView.LogRoot);
        Assert.Equal(TranscriptPage.Log, panel.Page);

        Assert.Equal("In Ship.", PanelPhrases.Apply(said, panel.Nav));
        Assert.Equal(TranscriptPage.Conversation, panel.Page);

        window.Close();
    }

    /// <summary>
    /// And the journal reading answers to the label it no longer draws, which its existing alias
    /// list covers for free — the reason the rename needed no new word on that side.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("journal file")]
    [InlineData("elite dangerous journal")]
    public void SayingTheOldJournalLabelStillReachesIt(string said)
    {
        var (panel, window) = Shown();

        Assert.Equal("Journal File.", PanelPhrases.Apply(said, panel.Nav));
        Assert.Equal(TranscriptPage.Journal, panel.Page);

        window.Close();
    }
}
