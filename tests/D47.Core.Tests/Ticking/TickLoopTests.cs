using D47.Core.Ticking;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ticking;

/// <summary>
/// The loop, driven by hand. Every test here calls <see cref="TickLoop.Tick"/> directly with a
/// time it chose — which is the whole reason the loop owns neither a thread nor a clock
/// (architecture.md §4, §8). Nothing in this file waits for anything.
/// </summary>
public class TickLoopTests
{
    private static TickLoop NewLoop() => new(NullLogger<TickLoop>.Instance);

    [Fact]
    public void SubscribersRunInRegistrationOrder()
    {
        var order = new List<string>();
        var loop = NewLoop();

        loop.Add("first", _ => order.Add("first"));
        loop.Add("second", _ => order.Add("second"));
        loop.Add("third", _ => order.Add("third"));

        loop.Tick(DateTimeOffset.UnixEpoch);

        // Load-bearing, not incidental: the journal is polled before anything that reads game
        // state, so a callout sees this tick's events rather than the previous tick's.
        Assert.Equal(["first", "second", "third"], order);
    }

    [Fact]
    public void AThrowingSubscriberDoesNotStopTheOnesAfterIt()
    {
        var reached = false;
        var loop = NewLoop();

        loop.Add("broken", _ => throw new InvalidOperationException("callout bug"));
        loop.Add("after", _ => reached = true);

        loop.Tick(DateTimeOffset.UnixEpoch);

        // One broken callout must not take push-to-talk down with it.
        Assert.True(reached);
    }

    [Fact]
    public void AThrowingSubscriberStillRunsOnLaterTicks()
    {
        var attempts = 0;
        var loop = NewLoop();

        loop.Add("broken", _ =>
        {
            attempts++;
            throw new InvalidOperationException("still broken");
        });

        loop.Tick(DateTimeOffset.UnixEpoch);
        loop.Tick(DateTimeOffset.UnixEpoch.AddMilliseconds(100));

        // Failing is not unsubscribing. A subscriber that throws on a transient condition —
        // a journal file mid-rotation — has to be given the next tick.
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void TheFirstTickIsMarkedAsSuch()
    {
        var marks = new List<bool>();
        var loop = NewLoop();

        loop.Add("watcher", context => marks.Add(context.IsFirst));

        loop.Tick(DateTimeOffset.UnixEpoch);
        loop.Tick(DateTimeOffset.UnixEpoch.AddMilliseconds(100));

        // This is how the startup priming tick is told apart from a live one, which is what
        // keeps a journal backlog from being announced as though it had just happened.
        Assert.Equal([true, false], marks);
    }

    [Fact]
    public void ElapsedIsZeroOnTheFirstTickAndTheGapAfterThat()
    {
        var elapsed = new List<TimeSpan>();
        var start = DateTimeOffset.UnixEpoch;
        var loop = NewLoop();

        loop.Add("watcher", context => elapsed.Add(context.Since));

        loop.Tick(start);
        loop.Tick(start.AddMilliseconds(100));
        loop.Tick(start.AddMilliseconds(350));

        Assert.Equal(
            [TimeSpan.Zero, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250)],
            elapsed);
    }

    [Fact]
    public void TimeComesFromTheCallerNotTheClock()
    {
        var seen = new List<DateTimeOffset>();
        var loop = NewLoop();

        // A journal fixture's timestamps, replayed. The machine's clock never enters into it,
        // which is what lets the harness run this at 100x and get identical results.
        var fixture = new[]
        {
            DateTimeOffset.Parse("3311-01-01T00:00:00Z"),
            DateTimeOffset.Parse("3311-01-01T00:00:10Z"),
        };

        loop.Add("watcher", context => seen.Add(context.Now));

        foreach (var moment in fixture)
        {
            loop.Tick(moment);
        }

        Assert.Equal(fixture, seen);
    }

    [Fact]
    public void TheTickCountIsTheNumberOfTicksRun()
    {
        var loop = NewLoop();

        Assert.Equal(0, loop.Count);

        loop.Tick(DateTimeOffset.UnixEpoch);
        loop.Tick(DateTimeOffset.UnixEpoch.AddMilliseconds(100));

        Assert.Equal(2, loop.Count);
    }

    [Fact]
    public void ASubscriberAddedLaterJoinsTheNextTick()
    {
        var late = 0;
        var loop = NewLoop();

        loop.Tick(DateTimeOffset.UnixEpoch);
        loop.Add("late", _ => late++);
        loop.Tick(DateTimeOffset.UnixEpoch.AddMilliseconds(100));

        // Phase 9's overlay state machine registers when SteamVR appears, not at composition.
        Assert.Equal(1, late);
    }
}
