using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.Core.Diagnostics.Donation;
using Xunit;

namespace D47.App.Tests;

/// <summary>The review step, which is where the consent actually happens.</summary>
public class WhatIsShownIsWhatLeavesTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    private static HelpImproveWindow Shown(Func<ExcerptRequest, string> build)
    {
        var window = new HelpImproveWindow(Noon, build);

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    /// <summary>By name, down the visual tree.</summary>
    private static T Control<T>(HelpImproveWindow window, string name)
        where T : Avalonia.Controls.Control =>
        window.GetVisualDescendants().OfType<T>().Single(found => found.Name == name);

    /// <summary>One rendering fills the pane and fills the clipboard.</summary>
    [AvaloniaFact]
    public void ThePaneHoldsExactlyWhatWouldBeCopied()
    {
        var window = Shown(_ => "the whole excerpt");

        Assert.Equal("the whole excerpt", Control<SelectableTextBlock>(window, "Excerpt").Text);
        Assert.Equal("the whole excerpt", window.Text);
    }

    /// <summary>And it is never a stale answer to an older question.</summary>
    [AvaloniaFact]
    public void AskingForSomethingElseRedrawsIt()
    {
        var asked = new List<ExcerptRequest>();

        var window = Shown(request =>
        {
            asked.Add(request);
            return $"{request.Before.TotalMinutes:0} back, speech {request.IncludeMySpeech}";
        });

        Assert.Equal("10 back, speech False", window.Text);

        Control<CheckBox>(window, "IncludeMySpeech").IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("10 back, speech True", window.Text);

 // A named span rather than a minute stepper.
        Control<ComboBox>(window, "Span").SelectedItem =
            ExcerptSpan.All.Single(span => span.Name == "The last 12 hours");

        Dispatcher.UIThread.RunJobs();

        Assert.Equal("720 back, speech True", window.Text);
        Assert.Equal(3, asked.Count);
    }

    /// <summary>The default is out.</summary>
    [AvaloniaFact]
    public void TheCommandersOwnSpeechStartsHeldBack()
    {
        var window = Shown(_ => string.Empty);

        Assert.False(Control<CheckBox>(window, "IncludeMySpeech").IsChecked);
    }

    /// <summary>And all of it is readable without going looking for it.</summary>
    [AvaloniaFact]
    public void NothingSitsOffTheRightEdge()
    {
        var window = Shown(_ => string.Empty);

        Assert.Equal(
            TextWrapping.Wrap,
            Control<SelectableTextBlock>(window, "Excerpt").TextWrapping);

 // The other half of it, and the reason is recorded on SpendWindow: a ScrollViewer that may
        // scroll horizontally measures its content with unconstrained width, which makes the wrapping above a
        // no-op.
        Assert.Equal(
            ScrollBarVisibility.Disabled,
            Control<ScrollViewer>(window, "ExcerptScroller").HorizontalScrollBarVisibility);
    }

    /// <summary>The window is anchored on the mark and nothing else.</summary>
    [AvaloniaFact]
    public void TheWindowIsCutAroundTheMark()
    {
        ExcerptRequest? asked = null;

        Shown(request =>
        {
            asked = request;
            return string.Empty;
        });

        Assert.NotNull(asked);
        Assert.Equal(Noon, asked.MarkedAt);
        Assert.Equal(Noon - ExcerptSpan.Default.Before, asked.From);
        Assert.Equal(Noon + ExcerptSpan.Default.After, asked.To);
    }
}
