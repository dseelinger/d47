using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>How often SteamVR's own account of an overlay reaches the log.</summary>
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

    /// <summary>An unchanged description eventually writes anyway.</summary>
    [Fact]
    public void AnUnchangedDescriptionStillBeatsOnTheHeartbeat()
    {
        var held = new SurfaceReport(Visible, Start);

        Assert.False(RuntimeReadback.Plan(held, Visible, Start + RuntimeReadback.Every - TimeSpan.FromSeconds(1)).Write);
        Assert.True(RuntimeReadback.Plan(held, Visible, Start + RuntimeReadback.Every).Write);
    }

    [Fact]
    public void AChangeWaitsTheFloorRatherThanTheHeartbeat()
    {
        var held = new SurfaceReport(Visible, Start);

        Assert.False(RuntimeReadback.Plan(held, Hidden, Start.AddMilliseconds(900)).Write);
        Assert.True(RuntimeReadback.Plan(held, Hidden, Start + RuntimeReadback.AtMost).Write);
    }

    /// <summary>A change suppressed by the floor is not lost.</summary>
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

    /// <summary>Compared ordinally, so a description differing only in case is a change.</summary>
    [Fact]
    public void ACaseOnlyDifferenceCountsAsAChange()
    {
        var held = new SurfaceReport("visible=true", Start);

        Assert.True(RuntimeReadback.Plan(held, "Visible=true", Start.AddSeconds(2)).Write);
    }

    /// <summary>An hour of a session with nothing happening, at the rate <c>Serve</c> actually runs.</summary>
    [Fact]
    public void AnHourOfAQuietSessionIsTwelveLinesRatherThanThirtySixThousand()
    {
        var written = Frames(TimeSpan.FromHours(1), _ => Visible);

        // One an hour would be too few to prove the session is alive; a line every five minutes is twelve,
        // and the unthrottled count at 10 Hz is thirty-six thousand.
        Assert.Equal(12, written);
    }

    /// <summary>
    /// A description that changes on every single frame is still floored to about one line a second
    /// rather than ten.
    /// </summary>
    [Fact]
    public void AValueThatJittersEveryFrameIsStillAboutOneLineASecond()
    {
        var written = Frames(TimeSpan.FromMinutes(1), frame => $"width={frame % 2}");

        // Sixty seconds of frames, so one a second is the ceiling.
        Assert.InRange(written, 30, 61);
    }

    /// <summary>The case the heartbeat and the floor have to agree about: something changing slowly.</summary>
    [Fact]
    public void SomethingChangingEveryTenSecondsIsWrittenEveryTime()
    {
        var written = Frames(TimeSpan.FromMinutes(1), frame => $"width={frame / 100}");

        // Six hundred frames: the first, then a change at each of frames 100 to 500.
        Assert.Equal(6, written);
    }

    /// <summary>
    /// Drives one surface at 10 Hz for a stretch of session and counts the lines, which is the only way
    /// to state any of this in the units the log is actually read in.
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
