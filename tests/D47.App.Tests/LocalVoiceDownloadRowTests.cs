using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// What the local voice's download button does while it is downloading (#101).
/// <para>
/// <b>The report this exists for, 2026-08-28:</b> <em>"It downloaded, but I had no indication that
/// it was working."</em> 350 MB arrived correctly and the surface said nothing at all — no bar, a
/// button that stayed pressable throughout, and a row still reading <em>not downloaded</em>. Every
/// one of those is a property of the drawn page rather than of anything in Core, so this drives the
/// real settings window: a probe of the descriptor would have passed on the day the bug shipped.
/// </para>
/// </summary>
public class LocalVoiceDownloadRowTests
{
    private const string ButtonName = "Press_speech_localVoice";
    private const string BarName = "Progress_speech_localVoice";

    /// <summary>
    /// The button is shut and the bar is drawn while the work runs, and both go back when it ends.
    /// <para>
    /// Held open by a task this test completes, because "while it runs" is the whole assertion and
    /// a fetch that returns immediately cannot be caught in the act.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public async Task TheButtonShutsAndTheBarShowsWhileItRuns()
    {
        var running = new TaskCompletionSource<string?>();
        IProgress<double>? reporting = null;

        var (settings, viewState, paths) = TestSurface.Create(localVoice: (progress, _) =>
        {
            reporting = progress;
            return running.Task;
        });

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        var button = Find<Button>(host.View, ButtonName);
        var bar = Find<ProgressBar>(host.View, BarName);

        // Nothing is happening yet, so the row is a button and nothing else.
        Assert.True(button.IsEnabled);
        Assert.False(bar.IsVisible);

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.False(button.IsEnabled);
        Assert.True(bar.IsVisible);

        // And the fraction it reports is the fraction it draws.
        reporting!.Report(0.42);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0.42, bar.Value, 3);

        running.SetResult(null);
        await running.Task;
        Dispatcher.UIThread.RunJobs();

        Assert.True(button.IsEnabled);
        Assert.False(bar.IsVisible);

        host.Close();
    }

    /// <summary>
    /// A second press while the first is still running does nothing, which is what the Commander
    /// who saw no progress at all would otherwise have done.
    /// </summary>
    [AvaloniaFact]
    public async Task ASecondPressWhileItRunsStartsNothing()
    {
        var running = new TaskCompletionSource<string?>();
        var presses = 0;

        var (settings, viewState, paths) = TestSurface.Create(localVoice: (_, _) =>
        {
            presses++;
            return running.Task;
        });

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);
        var button = Find<Button>(host.View, ButtonName);

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, presses);

        running.SetResult(null);
        await running.Task;
        Dispatcher.UIThread.RunJobs();

        host.Close();
    }

    /// <summary>
    /// What the fetch answers is shown on the row. Null is the ordinary end of a successful
    /// download — the state above the button already says it is installed — and a sentence is how
    /// a failure reaches the Commander without them opening a log.
    /// </summary>
    [AvaloniaFact]
    public void WhatTheFetchAnswersIsShownOnTheRow()
    {
        var (settings, viewState, paths) = TestSurface.Create(
            localVoice: (_, _) => Task.FromResult<string?>("The checksum did not match."));

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        Find<Button>(host.View, ButtonName).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            "The checksum did not match.",
            host.View.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));

        host.Close();
    }

    private static T Find<T>(Visual surface, string name)
        where T : Control =>
        surface.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
}
