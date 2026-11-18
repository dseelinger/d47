using Avalonia.Controls;
using D47.App.Voice;

namespace D47.App.Controls;

/// <summary>Declares a wait: the mark once the work has run <see cref="Delay"/>, the control shut and the waiting bed.</summary>
public static class Waiting
{
    /// <summary>How long the work runs before the mark is shown.</summary>
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(120);

    /// <summary>How long a shown mark stays up when the work succeeds.</summary>
    public static readonly TimeSpan Minimum = TimeSpan.FromMilliseconds(600);

    /// <summary>
    /// Runs <paramref name="work"/> with <paramref name="shut"/> disabled, showing <paramref name="shown"/> once it has
    /// run <see cref="Delay"/> and hiding it when it ends, and plays the waiting bed through <paramref name="sound"/>.
    /// The work's failure or cancellation is rethrown.
    /// </summary>
    /// <param name="delay">Waits the given time; <see cref="Task.Delay(TimeSpan)"/> when null.</param>
    public static Task While(
        WaitingState shown,
        WaitSound? sound,
        Func<Task> work,
        Control? shut = null,
        Func<TimeSpan, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(shown);

        return Run(() => shown.IsVisible = true, () => shown.IsVisible = false, hideOnSuccess: true, sound, work, shut, delay);
    }

    /// <summary>
    /// As <see cref="While(WaitingState, WaitSound?, Func{Task}, Control?, Func{TimeSpan, Task}?)"/>, with the mark and
    /// <paramref name="sentence"/> in <paramref name="line"/>. After work that succeeds the line keeps them until the
    /// caller's <see cref="StatusLine.Say"/>, <see cref="StatusLine.Fail"/> or <see cref="StatusLine.Clear"/> replaces
    /// them; work that fails clears the line before the failure is rethrown.
    /// </summary>
    public static Task While(
        StatusLine line,
        string sentence,
        WaitSound? sound,
        Func<Task> work,
        Control? shut = null,
        Func<TimeSpan, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(line);

        return Run(() => line.Wait(sentence), line.Clear, hideOnSuccess: false, sound, work, shut, delay);
    }

    private static async Task Run(Action show, Action hide, bool hideOnSuccess, WaitSound? sound, Func<Task> work, Control? shut, Func<TimeSpan, Task>? delay)
    {
        delay ??= span => Task.Delay(span);

        if (shut is not null)
        {
            shut.IsEnabled = false;
        }

        var succeeded = false;

        try
        {
            var running = work();

            if (sound is not null)
            {
                _ = sound.PlayWhile(running);
            }

            // Raced, so work that ends first is not held up by the delay.
            if (await Task.WhenAny(running, delay(Delay)) == running)
            {
                await running;
                succeeded = true;
                return;
            }

            show();

            var kept = delay(Minimum);

            await running;
            succeeded = true;
            await kept;
        }
        finally
        {
            if (hideOnSuccess || !succeeded)
            {
                hide();
            }

            if (shut is not null)
            {
                shut.IsEnabled = true;
            }
        }
    }
}
