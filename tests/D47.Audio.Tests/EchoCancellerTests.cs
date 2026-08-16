using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Audio.Tests;

/// <summary>
/// The echo canceller's lifecycle and its pass-through (list.md Phase 13).
/// <para>
/// None of this needs a microphone, a speaker or a running game — which is the reason it is
/// worth having. The class sits between capture and the listening gate on the one path where a
/// mistake is silent: <b>failure here is the capability being off, not a crash</b>, and a
/// canceller that quietly stopped passing audio through would present as d47 having gone deaf
/// with nothing in the log.
/// </para>
/// <para>
/// Everything below runs without the native WebRTC module loading. The framing that needs it is
/// in <see cref="EchoCancellerFramingTests"/>, which skips when it is absent.
/// </para>
/// </summary>
public class EchoCancellerTests
{
    private const int CaptureRate = 48_000;

    private static EchoCanceller Build(RecordingCaptureSink next, FakeRenderTap tap) =>
        new(next, tap, CaptureRate, NullLogger<EchoCanceller>.Instance);

    /// <summary>
    /// Before anything is started, every sample reaches the gate untouched. This is exactly what
    /// happened before the class existed, and it is what a machine that cannot load the native
    /// library has to keep doing.
    /// </summary>
    [Fact]
    public void AnIdleCancellerPassesCaptureStraightThrough()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Build(next, new FakeRenderTap());

        canceller.Write([0.1f, -0.2f, 0.3f]);

        Assert.Equal([0.1f, -0.2f, 0.3f], next.All);
    }

    /// <summary>
    /// And it passes them through <em>whole</em> rather than holding a partial frame. An inactive
    /// canceller that buffered would delay speech by up to 10 ms for no benefit, and would lose
    /// the tail of an utterance entirely.
    /// </summary>
    [Fact]
    public void AnIdleCancellerHoldsNothingBack()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Build(next, new FakeRenderTap());

        // Far short of the 480-sample frame the module would want.
        canceller.Write(new float[7]);

        Assert.Single(next.Writes);
        Assert.Equal(7, next.Writes[0].Length);
    }

    [Fact]
    public void AnIdleCancellerIsNotSubscribedToTheRenderTap()
    {
        var tap = new FakeRenderTap();
        using var canceller = Build(new RecordingCaptureSink(), tap);

        Assert.False(tap.Subscribed);
    }

    /// <summary>
    /// A reference frame arriving with nothing started is ignored rather than throwing. The tap
    /// is the mixer's, it runs whether or not cancellation is on, and the render thread must not
    /// take an exception from a subscriber that is not listening.
    /// </summary>
    [Fact]
    public void AReferenceFrameWithNothingRunningIsIgnored()
    {
        var tap = new FakeRenderTap();
        using var canceller = Build(new RecordingCaptureSink(), tap);

        tap.Raise(new RenderReferenceFrame(0, new byte[960], new AudioFormat(48_000, 2)));
    }

    [Fact]
    public void ResetReachesTheGateBelow()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Build(next, new FakeRenderTap());

        canceller.Reset();

        Assert.Equal(1, next.Resets);
    }

    /// <summary>
    /// Stopping something that was never started is a no-op. <c>ApplyListeningSettings</c> calls
    /// <c>Stop</c> on every listening row when the switch is off, including the first time.
    /// </summary>
    [Fact]
    public void StoppingSomethingThatNeverStartedDoesNothing()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Build(next, new FakeRenderTap());

        canceller.Stop();
        canceller.Stop();

        Assert.False(canceller.IsActive);
        Assert.Null(canceller.Unavailable);

        // And it still passes audio afterwards, which is the part that matters.
        canceller.Write([0.5f]);
        Assert.Equal([0.5f], next.All);
    }

    /// <summary>
    /// The invariant the panel reads, and it holds whether or not the native library is present:
    /// active means no reason, and a reason means not active. <c>ApplyListeningSettings</c> takes
    /// <see cref="EchoCanceller.IsActive"/> immediately after <c>Start</c> and hands it to the
    /// gate as "d47's own voice is being subtracted" — the belief that decides whether hands-free
    /// listening stays open while d47 is speaking. A state where both were true would keep the
    /// gate open against an uncancelled speaker.
    /// </summary>
    [Fact]
    public void AfterStartingItIsEitherRunningOrSaysWhyNot()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Build(next, new FakeRenderTap());

        canceller.Start();

        Assert.Equal(canceller.IsActive, canceller.Unavailable is null);
    }

    /// <summary>
    /// Whichever way <c>Start</c> went, capture still reaches the gate. A canceller that failed
    /// to start must not also take away the microphone.
    /// </summary>
    [Fact]
    public void CaptureSurvivesAStartThatFailed()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Build(next, new FakeRenderTap());

        canceller.Start();

        // Two whole frames at 48 kHz, so the answer does not depend on framing: whether the
        // module is running or not, 960 samples in is 960 samples out.
        canceller.Write(new float[960]);

        Assert.Equal(960, next.All.Length);
    }

    /// <summary>
    /// Stopping clears the reason as well as the state. The row can be switched back on, and a
    /// stale "could not start" sentence beside a canceller nobody has tried to start yet is a
    /// fault the Commander has already cleared.
    /// </summary>
    [Fact]
    public void StoppingClearsTheReason()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Build(next, new FakeRenderTap());

        canceller.Start();
        canceller.Stop();

        Assert.False(canceller.IsActive);
        Assert.Null(canceller.Unavailable);
    }

    /// <summary>
    /// Start is idempotent. Applying an unrelated listening row must not cycle a converged
    /// filter — throwing one away makes d47 audible to itself for about a second, every time.
    /// </summary>
    [Fact]
    public void StartingTwiceLeavesTheFirstOneAlone()
    {
        var next = new RecordingCaptureSink();
        var tap = new FakeRenderTap();
        using var canceller = Build(next, tap);

        canceller.Start();
        var running = canceller.IsActive;
        var subscribed = tap.Subscribed;

        canceller.Start();

        Assert.Equal(running, canceller.IsActive);
        Assert.Equal(subscribed, tap.Subscribed);
    }

    /// <summary>
    /// A stop-start cycle leaves the tap with exactly one subscription rather than two. A second
    /// one would feed every reference frame in twice, which is not a no-op to an adaptive filter
    /// — it is a filter being told the room is twice as loud as it is.
    /// </summary>
    [Fact]
    public void CyclingDoesNotLeaveTwoSubscriptionsOnTheTap()
    {
        var next = new RecordingCaptureSink();
        var tap = new FakeRenderTap();
        using var canceller = Build(next, tap);

        canceller.Start();
        canceller.Stop();

        Assert.False(tap.Subscribed);

        canceller.Start();
        canceller.Stop();

        Assert.False(tap.Subscribed);
    }

    [Fact]
    public void DisposingLetsGoOfTheTap()
    {
        var tap = new FakeRenderTap();
        var canceller = Build(new RecordingCaptureSink(), tap);

        canceller.Start();
        canceller.Dispose();

        Assert.False(tap.Subscribed);
        Assert.False(canceller.IsActive);
    }

    [Fact]
    public void DisposingTwiceIsFine()
    {
        var canceller = Build(new RecordingCaptureSink(), new FakeRenderTap());

        canceller.Dispose();
        canceller.Dispose();
    }

    /// <summary>
    /// Starting a disposed canceller is a programming error and says so, rather than quietly
    /// reporting itself unavailable — which would read as a machine problem.
    /// </summary>
    [Fact]
    public void StartingAfterDisposalThrows()
    {
        var canceller = Build(new RecordingCaptureSink(), new FakeRenderTap());
        canceller.Dispose();

        Assert.Throws<ObjectDisposedException>(canceller.Start);
    }

    /// <summary>
    /// Writing to a disposed canceller still passes audio down rather than throwing. It is called
    /// from the WASAPI capture thread, which is torn down independently, so the ordering is not
    /// something this class gets to insist on.
    /// </summary>
    [Fact]
    public void WritingAfterDisposalStillPassesThrough()
    {
        var next = new RecordingCaptureSink();
        var canceller = Build(next, new FakeRenderTap());

        canceller.Dispose();
        canceller.Write([0.25f]);

        Assert.Equal([0.25f], next.All);
    }

    /// <summary>
    /// The noise-suppression row is read when the module is configured, so it can be set before
    /// starting and is not lost by a stop-start cycle.
    /// </summary>
    [Fact]
    public void NoiseSuppressionIsRememberedAcrossACycle()
    {
        using var canceller = Build(new RecordingCaptureSink(), new FakeRenderTap());

        canceller.SuppressNoise = false;
        canceller.Start();
        canceller.Stop();

        Assert.False(canceller.SuppressNoise);
    }
}
