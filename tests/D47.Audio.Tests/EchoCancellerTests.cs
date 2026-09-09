using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Audio.Tests;

public class EchoCancellerTests
{
    private const int CaptureRate = 48_000;

    private static EchoCanceller Build(RecordingCaptureSink next, FakeRenderTap tap) =>
        new(next, tap, CaptureRate, NullLogger<EchoCanceller>.Instance);

    [Fact]
    public void AnIdleCancellerPassesCaptureStraightThrough()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Build(next, new FakeRenderTap());

        canceller.Write([0.1f, -0.2f, 0.3f]);

        Assert.Equal([0.1f, -0.2f, 0.3f], next.All);
    }

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

    [Fact]
    public void StoppingSomethingThatNeverStartedDoesNothing()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Build(next, new FakeRenderTap());

        canceller.Stop();
        canceller.Stop();

        Assert.False(canceller.IsActive);
        Assert.Null(canceller.Unavailable);

        canceller.Write([0.5f]);
        Assert.Equal([0.5f], next.All);
    }

    /// <summary>Active means no reason, and a reason means not active, loaded or not.</summary>
    [Fact]
    public void AfterStartingItIsEitherRunningOrSaysWhyNot()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Build(next, new FakeRenderTap());

        canceller.Start();

        Assert.Equal(canceller.IsActive, canceller.Unavailable is null);
    }

    [Fact]
    public void CaptureSurvivesAStartThatFailed()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Build(next, new FakeRenderTap());

        canceller.Start();

        // Two whole frames at 48 kHz, so the answer does not depend on framing.
        canceller.Write(new float[960]);

        Assert.Equal(960, next.All.Length);
    }

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

    [Fact]
    public void StartingAfterDisposalThrows()
    {
        var canceller = Build(new RecordingCaptureSink(), new FakeRenderTap());
        canceller.Dispose();

        Assert.Throws<ObjectDisposedException>(canceller.Start);
    }

    [Fact]
    public void WritingAfterDisposalStillPassesThrough()
    {
        var next = new RecordingCaptureSink();
        var canceller = Build(next, new FakeRenderTap());

        canceller.Dispose();
        canceller.Write([0.25f]);

        Assert.Equal([0.25f], next.All);
    }

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
