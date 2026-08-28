using D47.Core.Hotas;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>
/// Walking a switch (Phase 21, "Assign a switch by walking its positions").
/// <para>
/// The shapes here are the ones the spike measured on real hardware. The two-position switch
/// really does hold button 8 or button 9 and never neither; the three-position one really was
/// walked 4→5→6→5→4; and sixteen buttons really were held at rest with nothing being touched
/// (docs/spikes/hotas-switch-read.md, findings 3 and 4). Each of those is a test because each
/// one is a way for a capture to be silently wrong.
/// </para>
/// </summary>
public class SwitchCaptureTests
{
    private const string Stick = "{wgi/nrid/throttle}";
    private const string Grip = "{wgi/nrid/grip}";

    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    /// <summary>One device holding exactly these buttons out of 32.</summary>
    private static HotasReading Held(string id, params int[] held)
    {
        var buttons = new bool[32];

        foreach (var button in held)
        {
            buttons[button] = true;
        }

        return new HotasReading { Id = id, Buttons = buttons, VendorId = 0x4098, ProductId = 0xBD65 };
    }

    private static HotasReading Hat(string id, int position) =>
        new() { Id = id, Buttons = new bool[32], Hats = [position] };

    /// <summary>
    /// A walk, driven the way the window drives it: a reading, then a wait, then a reading.
    /// Times are handed in rather than slept through — nothing here reads a clock.
    /// </summary>
    private sealed class Walk
    {
        private readonly SwitchCapture _capture = new();
        private TimeSpan _at = TimeSpan.Zero;

        public CaptureResult Last { get; private set; } = null!;

        public SwitchCapture Capture => _capture;

        /// <summary>A reading taken now, with no time passing. What a moving switch looks like.</summary>
        public Walk Move(params HotasReading[] readings)
        {
            Last = _capture.Poll(Start + _at, readings);
            return this;
        }

        /// <summary>The same reading held past the settle window, which is what makes it a position.</summary>
        public Walk Rest(params HotasReading[] readings)
        {
            Move(readings);
            _at += SwitchCapture.Settle + TimeSpan.FromMilliseconds(100);
            Last = _capture.Poll(Start + _at, readings);
            return this;
        }

        public Walk Wait(TimeSpan how)
        {
            _at += how;
            return this;
        }

        public CaptureResult Finish() => Last = _capture.Finish(Start + _at);
    }

    [Fact]
    public void NothingHappensUntilSomethingMoves()
    {
        // Sixteen buttons were held at rest on the bench with nothing being touched, so "some
        // button is down" is not a signal — only a button that changes is (finding 4).
        var walk = new Walk()
            .Rest(Held(Stick, 4, 8, 12, 16, 20, 24, 28, 29, 30))
            .Rest(Held(Stick, 4, 8, 12, 16, 20, 24, 28, 29, 30));

        Assert.Equal(CaptureStage.Waiting, walk.Last.Stage);
        Assert.Empty(walk.Last.Positions);
        Assert.Contains("each position in turn", walk.Last.Says, StringComparison.Ordinal);
    }

    [Fact]
    public void ATwoPositionSwitchIsCapturedAsTwoButtons()
    {
        var walk = new Walk()
            .Rest(Held(Stick, 8))
            .Move(Held(Stick, 9))
            .Rest(Held(Stick, 9));

        var result = walk.Finish();

        Assert.True(result.IsCaptured, result.Says);
        Assert.Equal([8, 9], result.Positions.Select(position => position.Button));
        Assert.Equal(Stick, result.DeviceId);
    }

    [Fact]
    public void AThreePositionSwitchWalkedThereAndBackIsThreePositions()
    {
        // The exact walk the spike recorded: 4 -> 5 -> 6 -> 5 -> 4. Coming back through a
        // position already seen is the ordinary case, not a second sighting of a new one.
        var walk = new Walk()
            .Rest(Held(Stick, 4))
            .Rest(Held(Stick, 5))
            .Rest(Held(Stick, 6))
            .Rest(Held(Stick, 5))
            .Rest(Held(Stick, 4));

        var result = walk.Finish();

        Assert.True(result.IsCaptured, result.Says);
        Assert.Equal([4, 5, 6], result.Positions.Select(position => position.Button));
    }

    [Fact]
    public void APositionThatHoldsNoButtonIsAPositionNamedByItsAbsence()
    {
        // Every maintained switch on the bench held exactly one button at all times including at
        // centre — and nothing in RawGameController requires that. A capture that could not
        // express "nothing held" would read this centre as *still where it last was*, which is
        // the exact silent failure the phase exists to prevent (Phase 21, item 3).
        var walk = new Walk()
            .Rest(Held(Stick, 4))
            .Rest(Held(Stick))
            .Rest(Held(Stick, 6));

        var result = walk.Finish();

        Assert.True(result.IsCaptured, result.Says);
        Assert.Equal([4, null, 6], result.Positions.Select(position => position.Button));
        Assert.Contains("nothing held", result.Says, StringComparison.Ordinal);
    }

    [Fact]
    public void ASpringReturnControlIsDeclinedBecauseItWentHome()
    {
        // Hold duration cannot separate this from a switch flip — the measured ranges overlap
        // outright (finding 5). What separates it is that the control is back where the last
        // stop was, with nothing new held.
        var walk = new Walk()
            .Rest(Held(Stick, 4))
            .Move(Held(Stick, 4, 20))
            .Move(Held(Stick, 4));

        Assert.True(walk.Last.IsDeclined);
        Assert.Contains("went back where it started", walk.Last.Says, StringComparison.Ordinal);
    }

    [Fact]
    public void WalkingPastAPositionIsDeclinedDifferentlyAndNamesTheButton()
    {
        // The other rushed walk, and it is a different problem with a different fix. Button 5 was
        // passed through on the way to 6, so it moved and was never a position.
        var walk = new Walk()
            .Rest(Held(Stick, 4))
            .Move(Held(Stick, 5))
            .Move(Held(Stick, 6))
            .Rest(Held(Stick, 6));

        var result = walk.Finish();

        Assert.True(result.IsDeclined, result.Says);
        Assert.Contains("Button 5", result.Says, StringComparison.Ordinal);
        Assert.Contains("pausing at each position", result.Says, StringComparison.Ordinal);
    }

    [Fact]
    public void AHatIsDeclinedRatherThanReadAsATwoPositionToggle()
    {
        // A hat is a third input class. It reports a compass position and always returns to
        // centre, so a reconciler bound to one would be told the ship should be in two states a
        // second apart (finding 6).
        var walk = new Walk()
            .Rest(Hat(Grip, 0))
            .Move(Hat(Grip, 1));

        Assert.True(walk.Last.IsDeclined);
        Assert.Contains("hat, not a switch", walk.Last.Says, StringComparison.Ordinal);
    }

    [Fact]
    public void AWalkWhereNothingMovedIsDeclinedRatherThanCaptured()
    {
        var result = new Walk()
            .Rest(Held(Stick, 4, 8, 12))
            .Rest(Held(Stick, 4, 8, 12))
            .Finish();

        Assert.True(result.IsDeclined);
        Assert.Contains("did not see the switch settle", result.Says, StringComparison.Ordinal);
    }

    [Fact]
    public void AButtonHeldDownForTheSettleWindowIsIndistinguishableFromASwitchAndIsAccepted()
    {
        // Recorded rather than defended against, because it cannot be told apart: a push button
        // held down and never released *is* a control sitting in a position. What catches it is
        // the release — WentHome fires the moment it comes up. This asserts the boundary of what
        // the walk can know, so a future change that quietly moves it fails here.
        var result = new Walk()
            .Rest(Held(Stick, 4))
            .Move(Held(Stick, 4, 20))
            .Rest(Held(Stick, 4, 20))
            .Finish();

        Assert.True(result.IsCaptured, result.Says);
        Assert.Equal([null, 20], result.Positions.Select(position => position.Button));
    }

    [Fact]
    public void TwoButtonsHeldAtOneStopAreRefusedRatherThanGuessedAt()
    {
        var walk = new Walk()
            .Rest(Held(Stick, 4))
            .Rest(Held(Stick, 8, 9));

        Assert.True(walk.Last.IsDeclined);
        Assert.Contains("held together", walk.Last.Says, StringComparison.Ordinal);
    }

    [Fact]
    public void AWalkThatNeverLeftItsFirstPositionIsDeclined()
    {
        var result = new Walk()
            .Rest(Held(Stick, 8))
            .Move(Held(Stick, 8, 9))
            .Move(Held(Stick, 8))
            .Finish();

        Assert.True(result.IsDeclined);
    }

    [Fact]
    public void OnlyTheDeviceTheCommanderTouchedIsWatched()
    {
        // The throttle presents four interfaces sharing one VID and PID (finding 2), so the
        // device cannot be picked by anything except what just moved.
        var walk = new Walk()
            .Rest(Held(Stick, 4), Held(Grip, 12))
            .Move(Held(Stick, 4), Held(Grip, 13))
            .Rest(Held(Stick, 4), Held(Grip, 13))
            .Move(Held(Stick, 5), Held(Grip, 13))
            .Rest(Held(Stick, 5), Held(Grip, 13));

        var result = walk.Finish();

        // The grip moved first, so the grip is the device — and the throttle moving afterwards
        // is somebody's hand on a different control, not a position of this switch.
        Assert.Equal(Grip, result.DeviceId);
    }

    [Fact]
    public void ADeviceThatDisappearsMidWalkIsDeclinedRatherThanFinished()
    {
        var walk = new Walk()
            .Rest(Held(Stick, 4))
            .Move(Held(Stick, 5))
            .Move(Held(Grip, 0));

        Assert.True(walk.Last.IsDeclined);
        Assert.Contains("disappeared", walk.Last.Says, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReportCarriesTheTimelineAndTheOutcome()
    {
        // Most of the devices this has to work on are ones nobody here will ever hold, so a
        // declined walk has to be diagnosable from a file the Commander sends.
        var walk = new Walk()
            .Rest(Held(Stick, 4))
            .Move(Held(Stick, 4, 20))
            .Move(Held(Stick, 4));

        var report = walk.Capture.Export();

        Assert.Contains("Outcome    : Declined", report, StringComparison.Ordinal);
        Assert.Contains("button 20 down", report, StringComparison.Ordinal);
        Assert.Contains("button 20 up", report, StringComparison.Ordinal);
        Assert.Contains("declined:", report, StringComparison.Ordinal);
        Assert.Contains(Stick, report, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingIsSampledBeforeTheSettleWindowHasPassed()
    {
        // The whole classification rests on the sample landing after the switch has had time to
        // go home. Sampling early would accept a spring-return control as a position.
        var walk = new Walk()
            .Rest(Held(Stick, 4))
            .Move(Held(Stick, 5))
            .Wait(SwitchCapture.Settle - TimeSpan.FromMilliseconds(200))
            .Move(Held(Stick, 5));

        Assert.Empty(walk.Last.Positions);

        walk.Wait(TimeSpan.FromMilliseconds(300)).Move(Held(Stick, 5));

        Assert.Equal([4, 5], walk.Last.Positions.Select(position => position.Button));
    }

    [Fact]
    public void AFinishedWalkStaysFinished()
    {
        var walk = new Walk()
            .Rest(Held(Stick, 8))
            .Rest(Held(Stick, 9));

        var first = walk.Finish();
        var again = walk.Capture.Poll(Start, [Held(Stick, 8)]);

        Assert.True(first.IsCaptured);
        Assert.Same(first, again);
    }
}
