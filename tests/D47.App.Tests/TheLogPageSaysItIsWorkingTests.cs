using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>Opening the log file says it is working.</summary>
public class TheLogPageSaysItIsWorkingTests
{
    /// <summary>The glyph is up while the page is being built, not only while the file is being read.</summary>
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

        var glyph = view.GetControl<StackPanel>("ModePicker")
            .Children
            .OfType<BusyGlyph>()
            .Single();

        Assert.True(view.GetControl<StackPanel>("ModePicker").IsVisible, "there was no control to announce on");

        view.Page = TranscriptPage.Log;

        await reading.Task;

        // Waited for rather than slept through.
        Assert.True(
            await Eventually(() => glyph.IsVisible),
            "nothing said the log was being read");
        Assert.False(view.GetControl<ComboBox>("ModeBox").IsEnabled, "the control was still pressable");

        release.TrySetResult();
    }

    /// <summary>The file work runs off the drawing thread; the telling does not.</summary>
    [AvaloniaFact]
    public async Task TheReadTellsThePageOnTheThreadThatDraws()
    {
        var drawing = Environment.CurrentManagedThreadId;
        var raisedOff = new List<string?>();

        var model = new PanelViewModel { LogSource = () => "one line" };

        model.PropertyChanged += (_, e) =>
        {
            if (Environment.CurrentManagedThreadId != drawing)
            {
                raisedOff.Add(e.PropertyName);
            }
        };

        var view = new PanelView { DataContext = model };
        var window = new Window { Content = view, Width = 900, Height = 600 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        view.Page = TranscriptPage.Log;

        Assert.True(await Eventually(() => model.LogText == "one line"), "the read never landed");
        Assert.True(
            raisedOff.Count == 0,
            $"raised off the drawing thread: {string.Join(", ", raisedOff)}");
    }

    /// <summary>And it is put away again, whatever the read did.</summary>
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

        var glyph = view.GetControl<StackPanel>("ModePicker")
            .Children
            .OfType<BusyGlyph>()
            .Single();

        Assert.True(
            await Eventually(() => !glyph.IsVisible && view.GetControl<ComboBox>("ModeBox").IsEnabled),
            "the glyph was left spinning after the read failed");

        Assert.False(glyph.IsVisible);
        Assert.True(view.GetControl<ComboBox>("ModeBox").IsEnabled, "a page that failed once can never be opened again");
    }

    /// <summary>Pumps the dispatcher until a condition holds, or gives up.</summary>
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
