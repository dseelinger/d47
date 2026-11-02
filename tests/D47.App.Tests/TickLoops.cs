using D47.Core.Ticking;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Tests;

internal static class TickLoops
{
    /// <summary>A loop with one subscriber named <paramref name="broken"/> that has thrown until paused; none when null.</summary>
    public static TickLoop Paused(string? broken)
    {
        var loop = new TickLoop(NullLogger<TickLoop>.Instance);

        if (broken is null)
        {
            return loop;
        }

        loop.Add(broken, _ => throw new InvalidOperationException("broken"));

        for (var tick = 0; tick < 10; tick++)
        {
            loop.Tick(DateTimeOffset.UnixEpoch.AddMilliseconds(100 * tick));
        }

        return loop;
    }
}
