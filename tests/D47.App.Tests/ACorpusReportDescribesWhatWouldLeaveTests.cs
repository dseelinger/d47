using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.Core.Diagnostics.Donation;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The consent step for a corpus, driven through the drawn window
/// (<a href="https://github.com/dseelinger/d47/issues/174">#174</a>).
/// <para>
/// <b>The property under test is not the one the excerpt window has.</b> There, what is shown is
/// what leaves, and a test can compare the two strings. Here the payload is never shown at all —
/// it is hundreds of megabytes — so the thing that has to hold is one step back: <b>the report on
/// screen must describe the range the Save button would write</b>. A report about twelve months
/// sitting above a file containing thirteen is a yes to a document that does not describe what
/// left, and it is the failure this window is shaped to prevent.
/// </para>
/// </summary>
public class ACorpusReportDescribesWhatWouldLeaveTests
{
    private static CorpusDonateWindow Shown(
        Func<CorpusScope, IProgress<int>, CancellationToken, Task<CorpusDonateWindow.CorpusReading>> read,
        Func<Stream, IProgress<int>, CancellationToken, Task>? write = null)
    {
        var window = new CorpusDonateWindow(read, write ?? ((_, _, _) => Task.CompletedTask));

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    /// <summary>By name down the visual tree — a window built in code has no name scope.</summary>
    private static T Control<T>(CorpusDonateWindow window, string name)
        where T : Avalonia.Controls.Control =>
        window.GetVisualDescendants().OfType<T>().Single(found => found.Name == name);

    private static CorpusDonateWindow.CorpusReading Reading(string report) =>
        new(new CorpusSurvey(null, null, 0, 0, new CorpusTally(0, 0, 0, 0, 0, 0), []), report);

    private static async Task PressAsync(CorpusDonateWindow window, string button)
    {
        Control<Button>(window, button).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        // The read runs on a worker and posts back. Let it land, then let the window redraw.
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Nothing has been read, so there is nothing to consent to and nothing to save. The window
    /// opens without touching a single journal file, because reading a full history is seconds of
    /// work and not something to do to somebody who opened a window to look at it.
    /// </summary>
    [AvaloniaFact]
    public void ThereIsNothingToSaveBeforeAnythingHasBeenRead()
    {
        var window = Shown((_, _, _) => Task.FromResult(Reading("unused")));

        Assert.False(Control<Button>(window, "SaveCorpus").IsEnabled);
        Assert.Contains("Nothing has been read yet", Control<SelectableTextBlock>(window, "CorpusReport").Text);
    }

    /// <summary>The pane holds the report, which is the document the yes is given to.</summary>
    [AvaloniaFact]
    public async Task ReadingPutsTheReportOnScreenAndArmsTheSave()
    {
        var window = Shown((_, _, _) => Task.FromResult(Reading("the whole report")));

        await PressAsync(window, "ReadJournals");

        Assert.Equal("the whole report", Control<SelectableTextBlock>(window, "CorpusReport").Text);
        Assert.True(Control<Button>(window, "SaveCorpus").IsEnabled);
    }

    /// <summary>
    /// <b>The one that matters.</b> Changing the scope after reading throws the report away and
    /// disarms the Save — otherwise a Commander reads a report about one range and then writes
    /// another, having consented to neither.
    /// </summary>
    [AvaloniaFact]
    public async Task ChangingTheScopeThrowsTheReportAway()
    {
        var window = Shown((_, _, _) => Task.FromResult(Reading("the whole report")));

        await PressAsync(window, "ReadJournals");
        Assert.True(Control<Button>(window, "SaveCorpus").IsEnabled);

        Control<ComboBox>(window, "Scope").SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        Assert.False(Control<Button>(window, "SaveCorpus").IsEnabled);
        Assert.DoesNotContain("the whole report", Control<SelectableTextBlock>(window, "CorpusReport").Text);
    }

    /// <summary>The scope the Commander chose is the scope that gets read.</summary>
    [AvaloniaFact]
    public async Task TheChosenScopeIsTheOneRead()
    {
        var asked = new List<CorpusScope>();

        var window = Shown((scope, _, _) =>
        {
            asked.Add(scope);
            return Task.FromResult(Reading("read"));
        });

        Control<ComboBox>(window, "Scope").SelectedIndex = 2;
        Dispatcher.UIThread.RunJobs();

        await PressAsync(window, "ReadJournals");

        Assert.Equal(CorpusScope.All[2], Assert.Single(asked));
    }

    /// <summary>
    /// Nothing is written until the Commander picks a file. There is no file picker in a headless
    /// test, so this asserts the honest half: pressing Save without one never reaches the writer.
    /// </summary>
    [AvaloniaFact]
    public async Task NothingIsWrittenWithoutSomewhereToWriteIt()
    {
        var wrote = false;

        var window = Shown(
            (_, _, _) => Task.FromResult(Reading("read")),
            (_, _, _) =>
            {
                wrote = true;
                return Task.CompletedTask;
            });

        await PressAsync(window, "ReadJournals");
        await PressAsync(window, "SaveCorpus");

        Assert.False(wrote);
    }
}
