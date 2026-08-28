using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>
/// How often SteamVR's own account of an overlay reaches the log (Phase 9).
/// <para>
/// The read-back is what settles "the quad is placed correctly and I still cannot see it", which
/// is the question that cost a day in Phase 12. Writing it every frame is what makes it useless:
/// <c>Serve</c> runs at 10 Hz over three surfaces, and the line worth reading is the one where
/// the answer <em>changed</em>.
/// </para>
/// <para>
/// This lived inside <c>SteamVrRuntime.Serve</c>, five lines between an overlay submit and a
/// compositor call, and could not be reached without a headset — while being arithmetic over a
/// string and two timestamps (Phase 19).
/// </para>
/// </summary>
public class TheReadbackSaysWhenItChangesTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    private const string Visible = "visible=true width=1.20m at (0.00, 0.00, -1.00)";
    private const string Hidden = "visible=false width=1.20m at (0.00, 0.00, -1.00)";

    /// <summary>The first sighting is always worth a line: it is what says the surface exists.</summary>
    [Fact]
    public void TheFirstSightingIsAlwaysWritten()
    {
        var plan = RuntimeReadback.Plan(held: null, Visible, Start);

        Assert.True(plan.Write);
        Assert.Equal(Visible, plan.Held.Text);
        Assert.Equal(Start, plan.Held.When);
    }

    /// <summary>The very next frame, a tenth of a second later, says nothing.</summary>
    [Fact]
    public void TheNextFrameSaysNothing()
    {
        var first = RuntimeReadback.Plan(null, Visible, Start);

        var second = RuntimeReadback.Plan(first.Held, Visible, Start.AddMilliseconds(100));

        Assert.False(second.Write);
    }

    /// <summary>
    /// An unchanged description eventually writes anyway. The heartbeat is the only thing that
    /// says the session is still being served at all, which a reader scrolling back through a log
    /// has no other way to establish.
    /// </summary>
    [Fact]
    public void AnUnchangedDescriptionStillBeatsOnTheHeartbeat()
    {
        var held = new SurfaceReport(Visible, Start);

        Assert.False(RuntimeReadback.Plan(held, Visible, Start + RuntimeReadback.Every - TimeSpan.FromSeconds(1)).Write);
        Assert.True(RuntimeReadback.Plan(held, Visible, Start + RuntimeReadback.Every).Write);
    }

    /// <summary>
    /// A change waits only the floor, not the heartbeat. Five minutes is the right delay for
    /// "nothing is happening" and completely the wrong one for "the overlay just went invisible".
    /// </summary>
    [Fact]
    public void AChangeWaitsTheFloorRatherThanTheHeartbeat()
    {
        var held = new SurfaceReport(Visible, Start);

        Assert.False(RuntimeReadback.Plan(held, Hidden, Start.AddMilliseconds(900)).Write);
        Assert.True(RuntimeReadback.Plan(held, Hidden, Start + RuntimeReadback.AtMost).Write);
    }

    /// <summary>
    /// <b>A change suppressed by the floor is not lost.</b> The held text stays as it was, so the
    /// next frame still sees a change and writes it the moment the floor passes. Advancing the
    /// text without writing would swallow the change permanently — which is the one outcome a
    /// read-back must not produce, because the whole point of it is the moment things change.
    /// </summary>
    [Fact]
    public void AChangeHeldBackByTheFloorIsStillWrittenAfterwards()
    {
        var held = new SurfaceReport(Visible, Start);

        var suppressed = RuntimeReadback.Plan(held, Hidden, Start.AddMilliseconds(500));

        Assert.False(suppressed.Write);

        // The old text, deliberately: the change has not been reported yet.
        Assert.Equal(Visible, suppressed.Held.Text);
        Assert.Equal(Start, suppressed.Held.When);

        var later = RuntimeReadback.Plan(suppressed.Held, Hidden, Start.AddMilliseconds(1100));

        Assert.True(later.Write);
        Assert.Equal(Hidden, later.Held.Text);
    }

    /// <summary>
    /// And a blip that reverts inside the floor costs nothing. By the time the floor passes the
    /// description matches what was last written, so there is no change left to report — which is
    /// the jitter suppression the floor exists for, falling out of the same rule rather than
    /// needing one of its own.
    /// </summary>
    [Fact]
    public void ABlipThatRevertsInsideTheFloorIsNeverWritten()
    {
        var held = new SurfaceReport(Visible, Start);

        var blip = RuntimeReadback.Plan(held, Hidden, Start.AddMilliseconds(400));
        Assert.False(blip.Write);

        var back = RuntimeReadback.Plan(blip.Held, Visible, Start.AddMilliseconds(1200));

        Assert.False(back.Write);
        Assert.Equal(Start, back.Held.When);
    }

    /// <summary>The held state never moves on a frame that wrote nothing.</summary>
    [Fact]
    public void TheHeldStateAdvancesOnlyWhenSomethingIsWritten()
    {
        var held = new SurfaceReport(Visible, Start);

        var quiet = RuntimeReadback.Plan(held, Visible, Start.AddSeconds(30));

        Assert.False(quiet.Write);
        Assert.Equal(held, quiet.Held);
    }

    /// <summary>
    /// Compared ordinally, so a description differing only in case is a change. These are one
    /// machine's own strings compared against themselves a tenth of a second apart, and anything
    /// cleverer would be a slower way to reach the same answer — and a wrong one the day a
    /// description carries a number.
    /// </summary>
    [Fact]
    public void ACaseOnlyDifferenceCountsAsAChange()
    {
        var held = new SurfaceReport("visible=true", Start);

        Assert.True(RuntimeReadback.Plan(held, "Visible=true", Start.AddSeconds(2)).Write);
    }

    /// <summary>
    /// An hour of a session with nothing happening, at the rate <c>Serve</c> actually runs. This
    /// is the fault the throttle exists for, stated as a number: the old five-second interval
    /// wrote seven hundred identical lines an hour <em>per surface</em> and buried everything
    /// else in the file.
    /// </summary>
    [Fact]
    public void AnHourOfAQuietSessionIsTwelveLinesRatherThanThirtySixThousand()
    {
        var written = Frames(TimeSpan.FromHours(1), _ => Visible);

        // One an hour would be too few to prove the session is alive; a line every five minutes
        // is twelve, and the unthrottled count at 10 Hz is thirty-six thousand.
        Assert.Equal(12, written);
    }

    /// <summary>
    /// A description that changes on every single frame is still floored to about one line a
    /// second rather than ten. A value that jitters is the case where "write on change" alone
    /// would be worse than writing on a timer.
    /// <para>
    /// Asserted as the bound rather than as a count, because the exact number falls out of an
    /// interaction worth knowing about and not worth pinning: an alternating value settles into a
    /// 1.1-second cycle, not a 1.0-second one. At the moment the floor passes, the value happens
    /// to have flipped back to what was last written — so there is no change to report on that
    /// frame, and the write lands on the next one instead.
    /// </para>
    /// </summary>
    [Fact]
    public void AValueThatJittersEveryFrameIsStillAboutOneLineASecond()
    {
        var written = Frames(TimeSpan.FromMinutes(1), frame => $"width={frame % 2}");

        // Sixty seconds of frames, so one a second is the ceiling. Unthrottled it would be 600.
        Assert.InRange(written, 30, 61);
    }

    /// <summary>
    /// The case the heartbeat and the floor have to agree about: something changing slowly. Once
    /// every ten seconds is a change every time, and every one of them is worth a line — the
    /// floor never suppresses it and the heartbeat never has to.
    /// </summary>
    [Fact]
    public void SomethingChangingEveryTenSecondsIsWrittenEveryTime()
    {
        var written = Frames(TimeSpan.FromMinutes(1), frame => $"width={frame / 100}");

        // Six hundred frames: the first, then a change at each of frames 100 to 500.
        Assert.Equal(6, written);
    }

    /// <summary>
    /// Drives one surface at 10 Hz for a stretch of session and counts the lines, which is the
    /// only way to state any of this in the units the log is actually read in.
    /// </summary>
    private static int Frames(TimeSpan over, Func<int, string> description)
    {
        var tick = TimeSpan.FromMilliseconds(100);
        var frames = (int)(over / tick);

        SurfaceReport? held = null;
        var written = 0;

        for (var frame = 0; frame < frames; frame++)
        {
            var plan = RuntimeReadback.Plan(held, description(frame), Start + (tick * frame));

            held = plan.Held;
            written += plan.Write ? 1 : 0;
        }

        return written;
    }
}
