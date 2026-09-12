using D47.Core.Ticking;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Ticking;

/// <summary>
/// The loop's answer to a subscriber that fails every time: stop calling it, say so once, and try it
/// again on a slow clock (https://github.com/dseelinger/d47/issues/58).
/// </summary>
public class ASubscriberThatKeepsThrowingIsPausedTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    private static DateTimeOffset At(int tick) => Start.AddMilliseconds(100 * tick);

    [Fact]
    public void TenFailuresRunningStopItBeingCalled()
    {
        var logger = new RecordingLogger<TickLoop>();
        var loop = new TickLoop(logger);
        var attempts = 0;

        loop.Add("macros", _ =>
        {
            attempts++;
            throw new InvalidOperationException("the calling thread cannot access this object");
        });

        for (var tick = 0; tick < 20; tick++)
        {
            loop.Tick(At(tick));
        }

        // The eleventh tick does not reach it, nor do the nine after that.
        Assert.Equal(10, attempts);
        Assert.Equal(["macros"], loop.Paused);

        var warnings = logger.Entries.Where(entry => entry.Level == LogLevel.Warning).ToList();

        Assert.Single(warnings);
        Assert.Contains("macros", warnings[0].Message);
        Assert.Contains("10", warnings[0].Message);
    }

    [Fact]
    public void AMinuteLaterItIsTriedAgainAndASuccessResumesIt()
    {
        var logger = new RecordingLogger<TickLoop>();
        var loop = new TickLoop(logger);
        var attempts = 0;

        loop.Add("journal", _ =>
        {
            attempts++;

            if (attempts <= 10)
            {
                throw new IOException("the journal folder went away");
            }
        });

        for (var tick = 0; tick < 20; tick++)
        {
            loop.Tick(At(tick));
        }

        Assert.Equal(10, attempts);

        // A minute after the failure that paused it, measured on the clock the caller supplies.
        loop.Tick(At(9) + TimeSpan.FromMinutes(1));

        Assert.Equal(11, attempts);
        Assert.Empty(loop.Paused);

        Assert.Single(
            logger.Entries,
            entry => entry.Level == LogLevel.Information && entry.Message.Contains("journal"));
    }

    [Fact]
    public void ARetryThatFailsLeavesItPausedAndSaysNothing()
    {
        var logger = new RecordingLogger<TickLoop>();
        var loop = new TickLoop(logger);
        var attempts = 0;

        loop.Add("macros", _ =>
        {
            attempts++;
            throw new InvalidOperationException("still broken");
        });

        for (var tick = 0; tick < 20; tick++)
        {
            loop.Tick(At(tick));
        }

        var saidSoFar = logger.Entries.Count;

        // The retry, then a further minute of ticks on top of it.
        var retriedAt = At(9) + TimeSpan.FromMinutes(1);

        for (var tick = 0; tick < 60; tick++)
        {
            loop.Tick(retriedAt.AddSeconds(tick));
        }

        Assert.Equal(11, attempts);
        Assert.Equal(["macros"], loop.Paused);
        Assert.Equal(saidSoFar, logger.Entries.Count);
    }

    [Fact]
    public void OneThatFailsEveryThirdTickIsNeverPaused()
    {
        var loop = new TickLoop(new RecordingLogger<TickLoop>());
        var attempts = 0;

        loop.Add("callouts", _ =>
        {
            if (++attempts % 3 == 0)
            {
                throw new InvalidOperationException("an occasional bad event");
            }
        });

        for (var tick = 0; tick < 100; tick++)
        {
            loop.Tick(At(tick));
        }

        // Consecutive, not cumulative: 33 failures here, and none of them next to ten others.
        Assert.Equal(100, attempts);
        Assert.Empty(loop.Paused);
    }

    [Fact]
    public void APausedSubscriberDoesNotStopTheOnesAfterIt()
    {
        var loop = new TickLoop(new RecordingLogger<TickLoop>());
        var reached = 0;

        loop.Add("broken", _ => throw new InvalidOperationException("callout bug"));
        loop.Add("after", _ => reached++);

        for (var tick = 0; tick < 20; tick++)
        {
            loop.Tick(At(tick));
        }

        Assert.Equal(20, reached);
    }
}
