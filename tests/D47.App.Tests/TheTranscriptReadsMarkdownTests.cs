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

        Assert.Equal(
            "A small-pad combat trainer build.\n• A-rate thrusters where budget allows.",
            panel.TranscriptShown);
    }

    /// <summary>
    /// What the markers meant, on the run. Bold is the whole reason for reading them rather
    /// than cutting them: a reply that leans on emphasis loses the lean when it is stripped.
    /// </summary>
    [AvaloniaFact]
    public void WhatWasEmphasisedIsDrawnHeavier()
    {
        var panel = Laid(Said(Reply));

        var bold = panel.TranscriptRuns
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

        Assert.Equal("12:04 **not markdown** at all", panel.TranscriptShown);
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

    /// <summary>
    /// The flake, made to happen every time (GitHub issue 43).
    /// <para>
    /// <c>TheLogFileIsShownExactlyAsItIsOnDisk</c> failed once in CI on 2026-08-25 with
    /// <c>TranscriptShown</c> empty, and passed on a re-run of the same commit with nothing
    /// changed. The empty string was the tell: not the wrong text, but <em>nothing</em> — a page
    /// asserted on before it had been drawn. The log page reads its file on a worker and draws in
    /// the continuation, and the helper pumped the dispatcher once and took silence for an answer.
    /// </para>
    /// <para>
    /// <b>A slow read is the same thing a loaded CI machine is.</b> Reproduced at 1,500 ms and
    /// kept at 250, which was measured to outlast the single pump the helper used to do — so this
    /// fails without the fix rather than merely being able to.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void ASlowLogReadIsStillDrawnBeforeTheAssertion()
    {
        var model = new PanelViewModel
        {
            LogSource = () =>
            {
                Thread.Sleep(250);

                return "12:04 **not markdown** at all";
            },
        };

        var panel = Laid(new PanelView { DataContext = model, Page = TranscriptPage.Log });

        Assert.Equal("12:04 **not markdown** at all", panel.TranscriptShown);
    }

    private static PanelView Laid(PanelView panel)
    {
        var window = new Window { Width = 900, Height = 560, Content = panel };
        window.Show();

        var bounds = new Rect(0, 0, 900, 560);
        window.Measure(bounds.Size);
        window.Arrange(bounds);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // **And then wait for the read that is still running** (GitHub issue 43). The log page
        // reads its file on a worker and draws in the continuation, so one pump of the dispatcher
        // asks "has it landed yet" and takes silence for a no. That is right almost always and
        // wrong under load — it failed once in CI with the page empty and passed on a re-run of
        // the same commit — and a test that is right almost always is not a test.
        //
        // Pumped rather than blocked on, because the continuation needs this very thread:
        // GetAwaiter().GetResult() here would wait for a job only this loop can run.
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!panel.Reading.IsCompleted && DateTime.UtcNow < until)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return panel;
    }
}
