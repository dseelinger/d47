namespace D47.Core.Viewing;

/// <summary>
/// Runs work that gives way to the Listener: it starts once the Listener is not busy, and when the Listener becomes busy
/// while it runs it is cancelled and run again from the start once the Listener is done.
/// </summary>
public static class GivesWayToListener
{
    /// <summary>How often the Listener is checked.</summary>
    public static readonly TimeSpan CheckEvery = TimeSpan.FromMilliseconds(50);

    public static async Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> work,
        Func<bool> busy,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(busy);
        ArgumentNullException.ThrowIfNull(time);

        while (true)
        {
            while (busy())
            {
                await Task.Delay(CheckEvery, time, cancellationToken).ConfigureAwait(false);
            }

            using var giveWay = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var watch = time.CreateTimer(_ => GiveWayIfBusy(giveWay, busy), null, CheckEvery, CheckEvery);

            try
            {
                return await work(giveWay.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
        }
    }

    private static void GiveWayIfBusy(CancellationTokenSource giveWay, Func<bool> busy)
    {
        try
        {
            if (busy())
            {
                giveWay.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
