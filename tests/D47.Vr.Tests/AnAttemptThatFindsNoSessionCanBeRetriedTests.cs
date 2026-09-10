using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// The retry loop calls Start every few seconds forever, so an attempt that produced no session has
/// to leave the one-session slot free. A leaked slot makes every later attempt a permanent failure.
/// </summary>
public class AnAttemptThatFindsNoSessionCanBeRetriedTests
{
    [Fact]
    public void SteamVrNotRunningYetIsWaitedForRatherThanFailed()
    {
        var openVr = new FakeOpenVr { Running = false };
        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance, openVr);

        try
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var start = runtime.Start();

                Assert.Equal(VrStartOutcome.NotReady, start.Outcome);
                Assert.NotNull(start.Detail);
                Assert.Contains("attach when", start.Detail);
            }

            Assert.Equal(5, openVr.Count(nameof(FakeOpenVr.SessionIsRunning)));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void NoHeadsetSwitchedOnYetIsWaitedForToo()
    {
        var openVr = new FakeOpenVr { HeadsetPresent = false };
        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance, openVr);

        try
        {
            Assert.Equal(VrStartOutcome.NotReady, runtime.Start().Outcome);
            Assert.Equal(VrStartOutcome.NotReady, runtime.Start().Outcome);
        }
        finally
        {
            runtime.Stop();
        }
    }

    /// <summary>And the attempt after the wait is the one that gets in.</summary>
    [Fact]
    public void TheAttemptAfterTheWaitStarts()
    {
        var openVr = new FakeOpenVr { Running = false };
        var runtime = new SteamVrRuntime([new FakeSurface()], NullLogger<SteamVrRuntime>.Instance, openVr);

        try
        {
            Assert.Equal(VrStartOutcome.NotReady, runtime.Start().Outcome);

            openVr.Running = true;

            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);
        }
        finally
        {
            runtime.Stop();
        }
    }

    /// <summary>A machine with no runtime at all says so, and says it every time.</summary>
    [Fact]
    public void AMachineWithNoRuntimeSaysSoEveryTime()
    {
        var openVr = new FakeOpenVr { Installed = false };
        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance, openVr);

        try
        {
            Assert.Equal(VrStartOutcome.NoRuntime, runtime.Start().Outcome);
            Assert.Equal(VrStartOutcome.NoRuntime, runtime.Start().Outcome);

            // Never got as far as asking about a session.
            Assert.Equal(0, openVr.Count(nameof(FakeOpenVr.Init)));
        }
        finally
        {
            runtime.Stop();
        }
    }
}
