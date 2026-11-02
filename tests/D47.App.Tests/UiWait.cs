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
}
