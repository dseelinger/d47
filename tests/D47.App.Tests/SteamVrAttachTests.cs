using D47.Core.Vr;
using D47.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The real runtime's attach path, exercised on a machine with no headset session.</summary>
public class SteamVrAttachTests
{
    /// <summary>Why this file declined to run, or null when it ran.</summary>
    private static string? Live =>
        SteamVrRuntime.SteamVrIsRunning()
            ? "SteamVR is running on this machine, so the attach path has nothing to prove here — "
              + "and connecting would wake the headset (#35). Set D47_VR_LIVE=1 for the live checks."
            : null;

    /// <summary>A first attempt that produced no session must leave the one-session slot free: the retry loop calls Start every few seconds forever, and a leaked slot makes every later attempt a permanent "already running".</summary>
    [Fact]
    public void AnAttemptThatFindsNoSessionCanBeRetried()
    {
        Assert.SkipWhen(Live is not null, Live ?? string.Empty);

        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance);

        var first = runtime.Start();

        Assert.NotEqual(VrStartOutcome.Started, first.Outcome);

        var second = runtime.Start();

        Assert.NotEqual(VrStartOutcome.Failed, second.Outcome);
        Assert.Equal(first.Outcome, second.Outcome);
    }

    /// <summary>And it says which of the two waiting conditions it is: "SteamVR is not running" and "no headset is switched on" send the Commander to different switches.</summary>
    [Fact]
    public void WaitingSaysWhatItIsWaitingFor()
    {
        Assert.SkipWhen(Live is not null, Live ?? string.Empty);

        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance);

        var start = runtime.Start();

        try
        {
            Assert.True(
                start.Outcome is VrStartOutcome.NoRuntime or VrStartOutcome.NotReady,
                $"with no session up, an attach attempt should wait, but got {start.Outcome}: {start.Detail}");

            if (start.Outcome == VrStartOutcome.NotReady)
            {
                Assert.NotNull(start.Detail);
                Assert.Contains("attach when", start.Detail);
            }
        }
        finally
        {
            runtime.Stop();
        }
    }
}

/// <summary>Attaching to a session that is actually there.</summary>
public class SteamVrLiveTests
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("D47_VR_LIVE") == "1";

    [Fact]
    public void ARealSessionIsAttachedToRatherThanRefused()
    {
        Assert.SkipUnless(Enabled, "set D47_VR_LIVE=1 to run tests that connect to SteamVR and wake the headset");
        Assert.SkipUnless(SteamVrRuntime.SteamVrIsRunning(), "SteamVR is not running on this machine");

        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance);

        var start = runtime.Start();

        try
        {
            Assert.NotEqual(VrStartOutcome.Failed, start.Outcome);
        }
        finally
        {
            // Put the session back the way it was found, whatever happened.
            runtime.Stop();
        }
    }
}
