using D47.Core.Vr;
using D47.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The real runtime's attach path, exercised on a machine with no headset session.
/// <para>
/// VR_Init <em>starts SteamVR</em> when it is not already running, which turned a switched-off
/// headset into d47 launching SteamVR, SteamVR failing to find a headset, and — on the retry
/// loop — doing it again until SteamVR gave up with a critical error. Attaching is the
/// behaviour; launching is not.
/// </para>
/// </summary>
public class SteamVrAttachTests
{
    /// <summary>
    /// Whatever this machine has, a first attempt that produced no session must leave the
    /// one-session slot free — the retry loop calls Start every few seconds forever, and a
    /// slot leaked on the not-ready path would turn the second attempt and every one after it
    /// into a permanent "already running".
    /// </summary>
    [Fact]
    public void AnAttemptThatFindsNoSessionCanBeRetried()
    {
        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance);

        var first = runtime.Start();

        if (first.Outcome == VrStartOutcome.Started)
        {
            // A real headset session is up on this machine, so the retry path is not the one
            // under test. Put it back the way it was found.
            runtime.Stop();
            return;
        }

        var second = runtime.Start();

        Assert.NotEqual(VrStartOutcome.Failed, second.Outcome);
        Assert.Equal(first.Outcome, second.Outcome);
    }

    /// <summary>
    /// And it says which of the two waiting conditions it is. "SteamVR is not running" and "no
    /// headset is switched on" send the Commander to different switches, and the old message
    /// said neither because VR_Init had already been called by then.
    /// </summary>
    [Fact]
    public void WaitingSaysWhatItIsWaitingFor()
    {
        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance);

        var start = runtime.Start();

        try
        {
            Assert.True(
                start.Outcome is VrStartOutcome.Started
                    or VrStartOutcome.NoRuntime
                    or VrStartOutcome.NotReady,
                $"an attach attempt should not fail outright, but got {start.Outcome}: {start.Detail}");

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
