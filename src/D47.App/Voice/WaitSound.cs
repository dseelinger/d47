using D47.Core.Audio;

namespace D47.App.Voice;

/// <summary>Plays the waiting bed while a page waits on a task that runs past <see cref="Delay"/>.</summary>
/// <param name="cues">Asked for each wait, because the library is rebuilt when the audio folder changes.</param>
/// <param name="enabled">Whether the waiting bed is on, read when the delay ends.</param>
/// <param name="delay">Waits the given time; <see cref="Task.Delay(TimeSpan, CancellationToken)"/> when null.</param>
public sealed class WaitSound(
    AudioArbiter arbiter,
    Func<CueLibrary> cues,
    Func<bool> enabled,
    Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    /// <summary>How long a task runs before the waiting bed starts.</summary>
    public static readonly TimeSpan Delay = TimeSpan.FromSeconds(1);

    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;

    /// <summary>
    /// Plays the waiting bed from <see cref="Delay"/> after the call until <paramref name="running"/> ends.
    /// Nothing plays when the task ends first or the setting is off. Completes without rethrowing the task's
    /// failure or cancellation.
    /// </summary>
    public async Task PlayWhile(Task running)
    {
        ArgumentNullException.ThrowIfNull(running);

        using var cancel = new CancellationTokenSource();
        var waited = _delay(Delay, cancel.Token);

        if (await Task.WhenAny(running, waited).ConfigureAwait(false) == running)
        {
            await cancel.CancelAsync().ConfigureAwait(false);
            return;
        }

        if (!enabled())
        {
            return;
        }

        using (arbiter.OpenWait(cues().Waiting()))
        {
            await Task.WhenAny(running).ConfigureAwait(false);
        }
    }
}
