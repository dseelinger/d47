using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>The panel reads the model's markdown rather than drawing it.</summary>
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

    /// <summary>What the markers meant, on the run.</summary>
    [AvaloniaFact]
    public void WhatWasEmphasisedIsDrawnHeavier()
    {
        var panel = Laid(Said(Reply));

        var bold = panel.TranscriptRuns
            .Where(run => run.FontWeight == FontWeight.Bold)
            .Select(run => run.Text);

        Assert.Equal(["small-pad combat trainer", "A-rate thrusters"], bold);
    }

    [AvaloniaFact]
    public void TheLogFileIsShownExactlyAsItIsOnDisk()
    {
        var model = new PanelViewModel { LogSource = () => "12:04 **not markdown** at all" };
        var panel = Laid(new PanelView { DataContext = model, Page = TranscriptPage.Log });

        Assert.Equal("12:04 **not markdown** at all", panel.TranscriptShown);
    }

    /// <summary>Searching is against what the reader can see.</summary>
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

 /// <summary>The flake, made to happen every time.</summary>
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

 // **And then wait for the read that is still running**.
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
