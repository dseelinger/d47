using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Opening the log file says it is working (remediation.md 10, item 5).
/// <para>
/// Reported as nothing happening for long enough to wonder whether the press had landed. The
/// glyph existed and the read was already covered by it; the <em>draw</em> was not, and the draw
/// is the half that runs on the thread that paints — five hundred lines becoming runs and then a
/// layout pass, with the busy window already closed.
/// </para>
/// </summary>
public class TheLogPageSaysItIsWorkingTests
{
    /// <summary>
    /// The glyph is up while the page is being built, not only while the file is being read.
    /// <para>
    /// Driven by holding the read open on its own thread and asking what the glyph is doing, which
    /// is the one moment the two halves can be told apart.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public async Task TheGlyphIsUpForTheDrawAndNotOnlyForTheRead()
    {
        var reading = new TaskCompletionSource();
        var release = new TaskCompletionSource();

        var model = new PanelViewModel
        {
            LogSource = () =>
            {
                reading.TrySetResult();
                release.Task.GetAwaiter().GetResult();

                return string.Join('\n', Enumerable.Range(0, 400).Select(line => $"[12:00:00 INF] line {line}"));
            },
        };

        var view = new PanelView { DataContext = model };
        var window = new Window { Content = view, Width = 900, Height = 600 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var glyph = (view.GetControl<Button>("ModeButton").Content as StackPanel)!
            .Children
            .OfType<BusyGlyph>()
            .Single();

        Assert.True(view.GetControl<Button>("ModeButton").IsVisible, "there was no control to announce on");

        view.Page = TranscriptPage.Log;

        await reading.Task;

        // Waited for rather than slept through. The helper holds off for Busy.Delay before saying
        // anything, and a fixed sleep just past that is a test that passes on this bench and fails
        // on a loaded runner — which is the one thing a green local suite says nothing about.
        Assert.True(
            await Eventually(() => glyph.IsVisible),
            "nothing said the log was being read");
        Assert.False(view.GetControl<Button>("ModeButton").IsEnabled, "the control was still pressable");

        release.TrySetResult();
    }

    /// <summary>
    /// And it is put away again, whatever the read did. A glyph left spinning is worse than one
    /// that never appeared: it says the page is still coming.
    /// </summary>
    [AvaloniaFact]
    public async Task TheGlyphGoesAwayWhenTheReadFails()
    {
        var model = new PanelViewModel
        {
            LogSource = () => throw new IOException("the file is held by something else"),
        };

        var view = new PanelView { DataContext = model };
        var window = new Window { Content = view, Width = 900, Height = 600 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        view.Page = TranscriptPage.Log;

        var glyph = (view.GetControl<Button>("ModeButton").Content as StackPanel)!
            .Children
            .OfType<BusyGlyph>()
            .Single();

        Assert.True(
            await Eventually(() => !glyph.IsVisible && view.GetControl<Button>("ModeButton").IsEnabled),
            "the glyph was left spinning after the read failed");

        Assert.False(glyph.IsVisible);
        Assert.True(view.GetControl<Button>("ModeButton").IsEnabled, "a page that failed once can never be opened again");
    }

    /// <summary>
    /// Pumps the dispatcher until a condition holds, or gives up. A generous ceiling and a short
    /// step, so a slow machine waits longer rather than failing and a broken one still fails
    /// quickly enough to be worth running.
    /// </summary>
    private static async Task<bool> Eventually(Func<bool> held)
    {
        for (var waited = TimeSpan.Zero; waited < TimeSpan.FromSeconds(5); waited += Step)
        {
            Dispatcher.UIThread.RunJobs();

            if (held())
            {
                return true;
            }

            await Task.Delay(Step);
        }

        Dispatcher.UIThread.RunJobs();

        return held();
    }

    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(20);
}
