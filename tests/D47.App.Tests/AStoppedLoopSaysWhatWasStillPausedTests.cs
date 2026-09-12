using D47.App.Ticking;
using D47.Core.Ticking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A subscriber paused for the rest of the session is a feature that stopped running, so the shutdown
/// names it rather than leaving the loss to be spotted in an hour of log
/// (https://github.com/dseelinger/d47/issues/58).
/// </summary>
public class AStoppedLoopSaysWhatWasStillPausedTests
{
    private static TickLoop Loop(string? broken)
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

    private static IReadOnlyList<string> Stop(TickLoop loop)
    {
        var logger = new CapturingLogger();

        using (var driver = new TickDriver(loop, logger))
        {
            driver.Start();
        }

        return logger.Warnings;
    }

    [Fact]
    public void ThePausedSubscriberIsNamedOnTheWayOut()
    {
        var warning = Assert.Single(Stop(Loop("journal")));

        Assert.Contains("journal", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void ALoopThatLostNothingSaysNothing() => Assert.Empty(Stop(Loop(broken: null)));

    private sealed class CapturingLogger : ILogger<TickDriver>
    {
        private readonly List<string> _warnings = [];

        public IReadOnlyList<string> Warnings
        {
            get
            {
                lock (_warnings)
                {
                    return [.. _warnings];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel < LogLevel.Warning)
            {
                return;
            }

            lock (_warnings)
            {
                _warnings.Add(formatter(state, exception));
            }
        }
    }
}
