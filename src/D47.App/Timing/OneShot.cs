using Avalonia.Threading;

namespace D47.App.Timing;

/// <summary>Starts a timer that runs <paramref name="fire"/> once after <paramref name="delay"/>; disposing it cancels.</summary>
internal delegate IDisposable StartOneShot(TimeSpan delay, Action fire);

internal static class OneShot
{
    /// <summary>The timer UI code starts; tests replace it with a manual one.</summary>
    public static StartOneShot Dispatcher { get; set; } = OnDispatcher;

    /// <summary>Fires on a thread-pool thread.</summary>
    public static IDisposable OnThreadPool(TimeSpan delay, Action fire) =>
        new Timer(_ => fire(), null, delay, Timeout.InfiniteTimeSpan);

    /// <summary>Fires on the UI thread.</summary>
    public static IDisposable OnDispatcher(TimeSpan delay, Action fire)
    {
        var timer = new DispatcherTimer { Interval = delay };

        timer.Tick += (_, _) =>
        {
            timer.Stop();
            fire();
        };

        timer.Start();

        return new Cancel(timer);
    }

    private sealed class Cancel(DispatcherTimer timer) : IDisposable
    {
        public void Dispose() => timer.Stop();
    }
}
