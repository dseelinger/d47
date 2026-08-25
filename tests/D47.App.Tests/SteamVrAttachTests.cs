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
/// <para>
/// <b>Nothing here runs while a session is live</b> (<a
/// href="https://github.com/dseelinger/d47/issues/35">#35</a>). That issue offered "skip when a
/// session is live" and dismissed it — <em>loses the coverage exactly where the machine could give
/// it, which is the wrong way round</em> — and the dismissal rested on a premise that turns out not
/// to hold: <b>there was no coverage there to lose.</b> Read what these two did against a live
/// runtime. The first found <c>Started</c>, put the session back, and returned <em>without
/// asserting anything at all</em>. The second asserted that <c>Started</c> is one of three allowed
/// outcomes, which is a live smoke test rather than the regression this file is named for — and it
/// is below, opt-in, where a Commander can ask for it deliberately.
/// </para>
/// <para>
/// So the skip costs nothing and buys back the thing that was expensive: the suite was the biggest
/// SteamVR client on the Commander's machine, 94 connects over two days against d47's own 32, each
/// one taking a headset out of standby for five seconds — and each one a false reading in
/// <c>vrserver.txt</c>, which is the only instrument <a
/// href="https://github.com/dseelinger/d47/issues/18">#18</a> has.
/// </para>
/// <para>
/// The skip is exactly complementary to the coverage: these assert about the path taken when
/// SteamVR is <em>not</em> running, which is the one condition under which they cannot wake
/// anything.
/// </para>
/// </summary>
public class SteamVrAttachTests
{
    /// <summary>
    /// Why this file declined to run, or null when it ran. Asked of the process list and of
    /// nothing else — <b>every OpenVR call that would answer it authoritatively is one that
    /// connects</b>, which is the thing being avoided.
    /// </summary>
    private static string? Live =>
        SteamVrRuntime.SteamVrIsRunning()
            ? "SteamVR is running on this machine, so the attach path has nothing to prove here — "
              + "and connecting would wake the headset (#35). Set D47_VR_LIVE=1 for the live checks."
            : null;

    /// <summary>
    /// Whatever this machine has, a first attempt that produced no session must leave the
    /// one-session slot free — the retry loop calls Start every few seconds forever, and a
    /// slot leaked on the not-ready path would turn the second attempt and every one after it
    /// into a permanent "already running".
    /// </summary>
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

    /// <summary>
    /// And it says which of the two waiting conditions it is. "SteamVR is not running" and "no
    /// headset is switched on" send the Commander to different switches, and the old message
    /// said neither because VR_Init had already been called by then.
    /// </summary>
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

/// <summary>
/// Attaching to a session that is actually there (<a
/// href="https://github.com/dseelinger/d47/issues/35">#35</a>).
/// <para>
/// <b>Opt-in, because running it wakes the headset.</b> This is the one assertion the attach tests
/// above used to make on a live machine, moved to where it costs nothing until it is asked for —
/// the same bargain the TTS and update suites already strike with their own live checks.
/// </para>
/// <para>
/// Worth keeping rather than deleting: it is the only thing in the suite that proves the happy path
/// works end to end against real SteamVR rather than against a description of it.
/// </para>
/// </summary>
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
