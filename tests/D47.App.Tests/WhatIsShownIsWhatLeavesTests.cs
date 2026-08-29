using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.Core.Diagnostics.Donation;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The review step, which is where the consent actually happens
/// (<a href="https://github.com/dseelinger/d47/issues/160">#160</a>).
/// <para>
/// The property under test is the one the issue calls load-bearing: the Commander reads the
/// scrubbed excerpt and says yes to <em>that</em>. A preview assembled by one code path and a
/// payload assembled by another are two artefacts, and they only ever read one of them.
/// </para>
/// </summary>
public class WhatIsShownIsWhatLeavesTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    private static DonateExcerptWindow Shown(Func<ExcerptRequest, string> build)
    {
        var window = new DonateExcerptWindow(Noon, build);

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    /// <summary>
    /// By name, down the visual tree. A window built in code has no name scope to look one up in,
    /// which is the same route <c>CoverageWindow</c> and <c>SpendWindow</c>'s tests already take.
    /// </summary>
    private static T Control<T>(DonateExcerptWindow window, string name)
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

    /// <summary>
    /// And it is never a stale answer to an older question. Every control here changes what would
    /// leave, so every control here re-renders.
    /// </summary>
    [AvaloniaFact]
    public void AskingForSomethingElseRedrawsIt()
    {
        var asked = new List<ExcerptRequest>();

        var window = Shown(request =>
        {
            asked.Add(request);
            return $"{request.Before.TotalMinutes} back, speech {request.IncludeMySpeech}";
        });

        Assert.Equal("5 back, speech False", window.Text);

        Control<CheckBox>(window, "IncludeMySpeech").IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("5 back, speech True", window.Text);

        Control<NumericUpDown>(window, "MinutesBefore").Value = 12;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("12 back, speech True", window.Text);
        Assert.Equal(3, asked.Count);
    }

    /// <summary>
    /// The default is out. Sometimes the exact words are the bug and sometimes they are nobody's
    /// business, and the Commander is the only one who can tell which — per incident, and not once
    /// for all time.
    /// </summary>
    [AvaloniaFact]
    public void TheCommandersOwnSpeechStartsHeldBack()
    {
        var window = Shown(_ => string.Empty);

        Assert.False(Control<CheckBox>(window, "IncludeMySpeech").IsChecked);
    }

    /// <summary>
    /// The window is anchored on the mark and nothing else. The outburst that placed it is already
    /// gone by the time this opens — only the instant travels.
    /// </summary>
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
        Assert.Equal(Noon.AddMinutes(-5), asked.From);
        Assert.Equal(Noon.AddMinutes(1), asked.To);
    }
}
