using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>Waits on <c>Status.json</c> reaching a state. Reads the clock, so it stays out of Core.</summary>
public static class StatusWaits
{
    /// <summary>
    /// True when <paramref name="arrived"/> held within <paramref name="within"/>, false when it never did,
    /// null when the file was never readable or the wait was cancelled.
    /// </summary>
    public static async Task<bool?> AwaitStatus(
        GameStatusReader status,
        Func<GameStatus, bool> arrived,
        TimeSpan within,
        string what,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.Now;
        var deadline = started + within;
        var sawTheFile = false;

        while (DateTimeOffset.Now < deadline)
        {
            var current = status.Current;

            if (current.IsKnown)
            {
                sawTheFile = true;

                if (arrived(current))
                {
                    logger.LogInformation(
                        "Status reached {What} after {Elapsed:0.0}s (GuiFocus {Focus})",
                        what,
                        (DateTimeOffset.Now - started).TotalSeconds,
                        current.GuiFocus);
                    return true;
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        logger.LogInformation(
            "Status never reached {What} within {Seconds:0}s; Status.json {Readable}, GuiFocus {Focus}",
            what,
            within.TotalSeconds,
            sawTheFile ? "readable" : "never readable",
            status.Current.GuiFocus);

        return sawTheFile ? false : null;
    }

    /// <summary>The next status sample, which is what the boost loop watches.</summary>
    public static async Task<GameStatus> NextStatus(GameStatusReader status, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        return status.Current;
    }
}
