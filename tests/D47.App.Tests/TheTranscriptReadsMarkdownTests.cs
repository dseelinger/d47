using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The panel reads the model's markdown rather than drawing it.
/// <para>
/// Models write markdown whatever they are told, and the transcript used to show the markers:
/// a reply about a Sidewinder build arrived with <c>**A-rate thrusters**</c> in it, asterisks
/// and all. The parsing is <see cref="TranscriptMarkup"/>'s and is tested in Core; what is
/// tested here is the half that needs a control — that the style reaches a run, that the log
/// file is exempt, and that search and copy see the drawn text rather than the written one.
/// </para>
/// </summary>
public class TheTranscriptReadsMarkdownTests
{
    private const string Reply =
        "A **small-pad combat trainer** build.\n- **A-rate thrusters** where budget allows.";

    [AvaloniaFact]
    public void TheMarkersAreReadRatherThanShown()
    {
        var panel = Laid(Said(Reply));

        var shown = PanelParityTests.Shown(panel.GetControl<SelectableTextBlock>("Transcript"));

        Assert.Equal("A small-pad combat trainer build.\n• A-rate thrusters where budget allows.", shown);
    }

    /// <summary>
    /// What the markers meant, on the run. Bold is the whole reason for reading them rather
    /// than cutting them: a reply that leans on emphasis loses the lean when it is stripped.
    /// </summary>
    [AvaloniaFact]
    public void WhatWasEmphasisedIsDrawnHeavier()
    {
        var panel = Laid(Said(Reply));

        var bold = panel
            .GetControl<SelectableTextBlock>("Transcript")
            .Inlines!
            .OfType<Run>()
            .Where(run => run.FontWeight == FontWeight.Bold)
            .Select(run => run.Text);

        Assert.Equal(["small-pad combat trainer", "A-rate thrusters"], bold);
    }

    /// <summary>
    /// The log file is a file. A line of it holding an asterisk means an asterisk, and a page
    /// opened to read what was written is the last place to reformat anything.
    /// </summary>
    [AvaloniaFact]
    public void TheLogFileIsShownExactlyAsItIsOnDisk()
    {
        var model = new PanelViewModel { LogSource = () => "12:04 **not markdown** at all" };
        var panel = Laid(new PanelView { DataContext = model, Page = TranscriptPage.Log });

        Assert.Equal(
            "12:04 **not markdown** at all",
            PanelParityTests.Shown(panel.GetControl<SelectableTextBlock>("Transcript")));
    }

    /// <summary>
    /// Searching is against what the reader can see. Typing what is on the screen and being
    /// told there are no matches — because the buffer says <c>**A-rate thrusters**</c> — would
    /// be the same defect wearing a different coat.
    /// </summary>
    [AvaloniaFact]
    public void SearchingFindsWhatIsDrawnAndNotWhatWasWritten()
    {
        var panel = Laid(Said(Reply));

        panel.GetControl<TextBox>("SearchInput").Text = "A-rate thrusters where";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("1 of 1", panel.GetControl<TextBlock>("SearchCount").Text);
    }

    private static PanelView Said(string text)
    {
        var model = new PanelViewModel();
        model.Append(text);

        return new PanelView { DataContext = model };
    }

    private static PanelView Laid(PanelView panel)
    {
        var window = new Window { Width = 900, Height = 560, Content = panel };
        window.Show();

        var bounds = new Rect(0, 0, 900, 560);
        window.Measure(bounds.Size);
        window.Arrange(bounds);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return panel;
    }
}
