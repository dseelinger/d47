using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>
/// <em>Order agnostic Overlay</em> (Phase 9), driven with no headset in the room.
/// <para>
/// This is the test the whole seam exists for. SteamVR starting after d47, exiting under it,
/// and restarting mid-session are three orderings a Commander should never have to know about,
/// and all three are decided by arithmetic over a clock the test supplies. What is left on the
/// other side of <see cref="IVrRuntime"/> is interop, which no test can reach without hardware
/// — so almost nothing is left there (architecture.md §8).
/// </para>
/// </summary>
public class VrLifecycleTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 12, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AMachineWithNoRuntimeSettlesIntoUnavailableAndKeepsAsking()
    {
        var runtime = new FakeRuntime { Next = new VrStart(VrStartOutcome.NoRuntime, "No runtime.") };
        var lifecycle = Build(runtime);

        lifecycle.Tick(Start);

        Assert.Equal(VrState.Unavailable, lifecycle.State);
        Assert.Equal("No runtime.", lifecycle.Reason);
        Assert.Equal(1, runtime.Starts);

        // Still asking. A runtime can be installed while d47 is running, and a state that
        // stopped asking would need something to tell it to start again.
        lifecycle.Tick(Start + VrLifecycle.RetryInterval);
        Assert.Equal(2, runtime.Starts);
    }

    [Fact]
    public void SteamVrStartingAfterD47IsJustTheNextAttempt()
    {
        var runtime = new FakeRuntime { Next = new VrStart(VrStartOutcome.NotReady, "Not running.") };
        var lifecycle = Build(runtime);

        lifecycle.Tick(Start);
        Assert.Equal(VrState.Connecting, lifecycle.State);

        // Two minutes of a Commander finding their headset. No session, and no growth in
        // anything: the loop is a comparison against a clock.
        for (var second = 1; second <= 120; second++)
        {
            lifecycle.Tick(Start + TimeSpan.FromSeconds(second));
        }

        Assert.Equal(VrState.Connecting, lifecycle.State);
        Assert.Equal(25, runtime.Starts);

        runtime.Next = VrStart.Started;
        lifecycle.Tick(Start + TimeSpan.FromSeconds(125));

        Assert.Equal(VrState.Active, lifecycle.State);
        Assert.Null(lifecycle.Reason);
        Assert.Equal(1, lifecycle.Sessions);
    }

    [Fact]
    public void AttemptsAreSpacedByTheRetryIntervalRatherThanMadeEveryTick()
    {
        var runtime = new FakeRuntime { Next = new VrStart(VrStartOutcome.NotReady) };
        var lifecycle = Build(runtime);

        // 10 Hz, which is the tick loop's real rate. Without the interval this would be 300
        // failing VR_Init calls in thirty seconds.
        for (var tick = 0; tick < 300; tick++)
        {
            lifecycle.Tick(Start + TimeSpan.FromMilliseconds(tick * 100));
        }

        // Thirty seconds of ticks, six attempts: t=0 and every five seconds after.
        Assert.Equal(6, runtime.Starts);
    }

    [Fact]
    public void SteamVrExitingUnderD47DropsBackToConnectingAndRecovers()
    {
        var runtime = new FakeRuntime { Next = VrStart.Started };
        var lifecycle = Build(runtime);

        lifecycle.Tick(Start);
        Assert.Equal(VrState.Active, lifecycle.State);

        runtime.ServeAnswers = false;
        lifecycle.Tick(Start + TimeSpan.FromSeconds(1));

        Assert.Equal(VrState.Connecting, lifecycle.State);
        Assert.Equal(1, runtime.Stops);

        // Rebuilt rather than repaired: a session that has gone away leaves every handle
        // stale, and the next attempt starts from nothing.
        runtime.ServeAnswers = true;
        lifecycle.Tick(Start + TimeSpan.FromSeconds(1) + VrLifecycle.RetryInterval);

        Assert.Equal(VrState.Active, lifecycle.State);
        Assert.Equal(2, lifecycle.Sessions);
        Assert.Equal(2, runtime.Starts);
    }

    [Fact]
    public void AnotherCopyOfD47OwningTheOverlaysIsSaidPlainly()
    {
        var runtime = new FakeRuntime
        {
            Next = new VrStart(VrStartOutcome.AlreadyOwned),
        };

        var lifecycle = Build(runtime);
        lifecycle.Tick(Start);

        Assert.Equal(VrState.Connecting, lifecycle.State);
        Assert.Contains("Another copy of D47", lifecycle.Reason ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryTransitionIsAnnouncedOnceAndRepeatsAreNot()
    {
        var runtime = new FakeRuntime { Next = new VrStart(VrStartOutcome.NotReady) };
        var lifecycle = Build(runtime);

        var seen = new List<VrState>();
        lifecycle.Changed += seen.Add;

        lifecycle.Tick(Start);
        lifecycle.Tick(Start + VrLifecycle.RetryInterval);
        lifecycle.Tick(Start + VrLifecycle.RetryInterval + VrLifecycle.RetryInterval);

        // Already Connecting at construction, so three failed attempts are no transitions.
        Assert.Empty(seen);

        runtime.Next = VrStart.Started;
        lifecycle.Tick(Start + TimeSpan.FromMinutes(1));

        Assert.Equal([VrState.Active], seen);
    }

    [Fact]
    public void ALiveSessionIsServedOnEveryTickRatherThanOnTheRetryInterval()
    {
        var runtime = new FakeRuntime { Next = VrStart.Started };
        var lifecycle = Build(runtime);

        for (var tick = 0; tick < 20; tick++)
        {
            lifecycle.Tick(Start + TimeSpan.FromMilliseconds(tick * 100));
        }

        Assert.Equal(VrState.Active, lifecycle.State);
        Assert.Equal(19, runtime.Serves);
    }

    private static VrLifecycle Build(IVrRuntime runtime) =>
        new(runtime, NullLogger<VrLifecycle>.Instance);

    private sealed class FakeRuntime : IVrRuntime
    {
        public VrStart Next { get; set; } = VrStart.Started;

        public bool ServeAnswers { get; set; } = true;

        public int Starts { get; private set; }

        public int Serves { get; private set; }

        public int Stops { get; private set; }

        public VrStart Start()
        {
            Starts++;
            return Next;
        }

        public bool Serve(DateTimeOffset now)
        {
            Serves++;
            return ServeAnswers;
        }

        public void Stop() => Stops++;
    }
}
