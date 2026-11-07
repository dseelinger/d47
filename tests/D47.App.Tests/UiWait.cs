using Avalonia.Threading;
using Xunit;

namespace D47.App.Tests;

internal static class UiWait
{
    /// <summary>Pumps the UI thread until <paramref name="done"/> holds, for work that comes back from the pool.</summary>
    public static void Until(Func<bool> done, double seconds = 5, int pollMs = 5, Func<string>? showing = null)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);

        while (!done())
        {
            Assert.True(
                DateTime.UtcNow < deadline,
                showing is null ? "the page did not settle in time" : $"the page did not settle in time; showing {showing()}");
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(pollMs);
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Runs the UI thread's own loop, which fires its timers as <see cref="Dispatcher.RunJobs"/> does not, until
    /// <paramref name="done"/> holds or <paramref name="seconds"/> pass; true when it held.
    /// </summary>
    public static bool Running(Func<bool> done, double seconds)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        var check = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.Send, (_, _) =>
        {
            if (done())
            {
                stop.Cancel();
            }
        });

        check.Start();

        try
        {
            Dispatcher.UIThread.MainLoop(stop.Token);
        }
        finally
        {
            check.Stop();
        }

        Dispatcher.UIThread.RunJobs();

        return done();
    }
}
