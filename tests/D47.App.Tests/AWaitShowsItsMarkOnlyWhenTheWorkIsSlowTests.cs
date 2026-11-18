using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Voice;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary><see cref="Waiting.While(WaitingState, WaitSound?, Func{Task}, Control?, Func{TimeSpan, Task}?)"/>, on a delay the test ends by hand.</summary>
public class AWaitShowsItsMarkOnlyWhenTheWorkIsSlowTests
{
    private readonly Delays _delays = new();

    /// <summary>Whether the mark was ever made visible, not merely whether it is now.</summary>
    private sealed class Watch
    {
        public Watch(Control mark) =>
            mark.PropertyChanged += (_, change) =>
            {
                if (change.Property == Visual.IsVisibleProperty && change.NewValue is true)
                {
                    Shown = true;
                }
            };

        public bool Shown { get; private set; }
    }

    /// <summary>Each delay asked for, ended when the test says.</summary>
    private sealed class Delays
    {
        private readonly List<(TimeSpan Span, TaskCompletionSource Done)> _asked = [];

        public Task Wait(TimeSpan span)
        {
            var done = new TaskCompletionSource();
            _asked.Add((span, done));
            return done.Task;
        }

        public void End(TimeSpan span)
        {
            _asked.First(asked => asked.Span == span && !asked.Done.Task.IsCompleted).Done.SetResult();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public async Task WorkThatEndsBeforeTheDelayNeverShowsTheMark()
    {
        var mark = new WaitingState { IsVisible = false };
        var watch = new Watch(mark);
        var button = new Button();

        await Waiting.While(mark, null, () => Task.CompletedTask, button, _delays.Wait);

        Assert.False(watch.Shown, "fast work flashed the mark on and off");
        Assert.True(button.IsEnabled);
    }

    /// <summary>Work just past the delay must not put the mark up for a moment, which is the same flash at the other end.</summary>
    [AvaloniaFact]
    public void SlowWorkShowsTheMarkAndKeepsItUpForTheMinimum()
    {
        var mark = new WaitingState { IsVisible = false };
        var button = new Button();
        var work = new TaskCompletionSource();

        var waiting = Waiting.While(mark, null, () => work.Task, button, _delays.Wait);
        Dispatcher.UIThread.RunJobs();

        Assert.False(mark.IsVisible);
        Assert.False(button.IsEnabled, "the control could be pressed a second time while it worked");

        _delays.End(Waiting.Delay);

        Assert.True(mark.IsVisible, "slow work ran with no mark");

        work.SetResult();
        Dispatcher.UIThread.RunJobs();

        Assert.True(mark.IsVisible, "the mark went before its minimum");
        Assert.False(waiting.IsCompleted);

        _delays.End(Waiting.Minimum);

        Assert.False(mark.IsVisible);
        Assert.True(button.IsEnabled);
        Assert.True(waiting.IsCompletedSuccessfully);
    }

    /// <summary>A control that failed once and stayed shut could never be tried again.</summary>
    [AvaloniaFact]
    public async Task FailedWorkIsReportedAndPutsTheMarkAway()
    {
        var mark = new WaitingState { IsVisible = false };
        var button = new Button();
        var work = new TaskCompletionSource();

        var waiting = Waiting.While(mark, null, () => work.Task, button, _delays.Wait);
        _delays.End(Waiting.Delay);

        Assert.True(mark.IsVisible);

        work.SetException(new InvalidOperationException("the host refused"));
        Dispatcher.UIThread.RunJobs();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => waiting);
        Assert.Equal("the host refused", failure.Message);
        Assert.False(mark.IsVisible);
        Assert.True(button.IsEnabled);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Waiting.While(mark, null, () => throw new InvalidOperationException("refused at once"), button, _delays.Wait));

        Assert.True(button.IsEnabled);
    }

    [AvaloniaFact]
    public void TheWaitingBedIsStartedForTheWork()
    {
        var asked = TimeSpan.Zero;
        var sound = new WaitSound(
            new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance),
            static () => throw new InvalidOperationException("the bed is never reached in this test"),
            () => true,
            (length, _) =>
            {
                asked = length;
                return new TaskCompletionSource().Task;
            });

        var work = new TaskCompletionSource();

        _ = Waiting.While(new WaitingState { IsVisible = false }, sound, () => work.Task, delay: _delays.Wait);

        Assert.Equal(WaitSound.Delay, asked);

        work.SetResult();
    }

    /// <summary>The status line's form: the sentence beside the mark while it waits, gone once the result replaces it.</summary>
    [AvaloniaFact]
    public void AStatusLineWaitShowsItsSentenceUntilTheResultReplacesIt()
    {
        const string sentence = "Reading the markets nearby…";

        var line = new StatusLine();
        var work = new TaskCompletionSource();

        var waiting = Waiting.While(line, sentence, null, () => work.Task, delay: _delays.Wait);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(line.Text);

        _delays.End(Waiting.Delay);

        var mark = line.GetLogicalDescendants().OfType<WaitingState>().Single();

        Assert.Equal(sentence, line.Text);
        Assert.True(mark.IsVisible);
        Assert.Equal(sentence, AutomationProperties.GetName(mark));

        work.SetResult();
        _delays.End(Waiting.Minimum);

        Assert.True(waiting.IsCompletedSuccessfully);
        Assert.Equal(sentence, line.Text);

        line.Say("Three stations.");

        Assert.Equal("Three stations.", line.Text);
        Assert.False(mark.IsVisible);
        Assert.DoesNotContain(line.GetLogicalDescendants().OfType<TextBlock>(), block => block.Text == sentence);

        line.Wait(sentence);
        line.Fail("The market service did not answer.");

        Assert.Equal("The market service did not answer.", line.Text);
        Assert.False(mark.IsVisible);
    }

    /// <summary>A failed wait leaves no mark stepping on the line for the caller to forget.</summary>
    [AvaloniaFact]
    public async Task AStatusLineWaitThatFailsClearsTheLine()
    {
        var line = new StatusLine();
        var work = new TaskCompletionSource();

        var waiting = Waiting.While(line, "Reading the markets nearby…", null, () => work.Task, delay: _delays.Wait);
        _delays.End(Waiting.Delay);

        Assert.NotNull(line.Text);

        work.SetException(new HttpRequestException("no answer"));
        Dispatcher.UIThread.RunJobs();

        await Assert.ThrowsAsync<HttpRequestException>(() => waiting);
        Assert.Null(line.Text);
        Assert.False(line.GetLogicalDescendants().OfType<WaitingState>().Single().IsVisible);
    }

    [AvaloniaFact]
    public void AMarkWithNoSentenceIsNamedWaiting()
    {
        var mark = new WaitingState();

        Assert.Equal("Waiting", AutomationProperties.GetName(mark));
    }
}
