using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>
/// A switch sitting on a button Elite binds as well (#147).
/// <para>
/// <b>Reported as a reconciler bug, and the reconciler was right every time.</b> The same physical
/// button was bound twice — once in Elite, once as a d47 maintained switch — so every flip acted
/// twice. Where d47 correctly pressed nothing, Elite's toggle moved the gear the wrong way; where
/// d47 correctly pressed, the two toggles cancelled and nothing happened at all. Measured against a
/// running game at 10 Hz: all seven flips explained, d47's belief about the gear correct on every
/// one of them.
/// </para>
/// <para>
/// d47 cannot stop Elite acting on a binding and must not try — <b>binds are read-only</b>. What it
/// can do is notice, because it already parses the binds file and already knows the device and
/// button of every switch position. Nobody had asked the one question that joins them.
/// </para>
/// </summary>
public class ASwitchBoundInEliteTooTests
{
    private const string Stick = "{wgi/nrid/throttle}";
    private const string OtherStick = "{wgi/nrid/second-throttle}";

    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    /// <summary>The Commander's own throttle, as d47 describes it.</summary>
    private const string ThrottleDescription = "VID 0x4098 PID 0xBD65, 32 buttons, 0 hats, 0 axes";

    private static SwitchReconciler New() => new(NullLogger<SwitchReconciler>.Instance);

    /// <summary>
    /// The binding that caused it, verbatim from the report: landing gear on the keyboard's L, and
    /// on <c>Joy_9</c> of the throttle.
    /// </summary>
    private static EliteBinds Binds(params EliteBinding[] extra) => new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings = [new EliteBinding("LandingGearToggle", "Primary", "Keyboard", "Key_L"), .. extra],
    };

    private static HotasReading Held(string id, params int[] held)
    {
        var buttons = new bool[32];

        foreach (var button in held)
        {
            buttons[button] = true;
        }

        return new HotasReading { Id = id, Buttons = buttons };
    }

    private static SwitchMapping GearSwitch(string id = Stick, string description = ThrottleDescription) => new()
    {
        Name = "LDG GEAR",
        DeviceId = id,
        Device = description,
        Positions =
        [
            new SwitchPosition(9, "landing_gear", DesiredState.On),
            new SwitchPosition(8, "landing_gear", DesiredState.Off),
        ],
    };

    private static SwitchTick Tick(HotasReading reading, EliteBinds binds, StatusFlags flags = StatusFlags.None) =>
        new()
        {
            Now = Start,
            Readings = [reading],
            Status = new GameStatus { Flags = flags | StatusFlags.InMainShip, ReadAt = Start },
            Binds = binds,
            Enabled = true,
        };

    private static string? CollisionOn(EliteBinds binds, SwitchMapping? mapping = null)
    {
        var reconciler = New();
        var switches = new[] { mapping ?? GearSwitch() };

        reconciler.Poll(Tick(Held(switches[0].DeviceId, 8), binds), switches);

        return reconciler.States.Single().Collides;
    }

    /// <summary>
    /// <b>The off-by-one, pinned.</b> Elite counts joystick buttons from one and d47 counts from
    /// zero, so Elite's <c>Joy_9</c> is d47's button 8. The field data settles it independently:
    /// were it button 9, the three flips to that position would have moved the gear, and they did
    /// not.
    /// </summary>
    [Fact]
    public void ElitesJoy9IsD47sButton8()
    {
        var binds = Binds(new EliteBinding("LandingGearToggle", "Secondary", "4098BD65", "Joy_9"));

        Assert.Single(binds.UsingJoystickButton(8, "4098BD65"));
        Assert.Empty(binds.UsingJoystickButton(9, "4098BD65"));
    }

    /// <summary>Elite's name for a device, derived from d47's own description of it.</summary>
    [Theory]
    [InlineData(ThrottleDescription, "4098BD65")]
    [InlineData("VID 0x044f PID 0xb10a, 16 buttons, 1 hats, 4 axes", "044FB10A")]
    [InlineData("something else entirely", null)]
    [InlineData("", null)]
    public void TheDeviceTokenIsDerivedFromTheDescription(string description, string? expected) =>
        Assert.Equal(expected, EliteBinds.EliteDeviceToken(description));

    /// <summary>The collision itself, named, on the switch the Commander is looking at.</summary>
    [Fact]
    public void ASwitchOnAButtonEliteBindsSaysSo()
    {
        var collides = CollisionOn(Binds(new EliteBinding("LandingGearToggle", "Secondary", "4098BD65", "Joy_9")));

        Assert.NotNull(collides);
        Assert.Contains("LandingGearToggle", collides, StringComparison.Ordinal);
        Assert.Contains("both act", collides, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// And a button Elite leaves alone says nothing, because a warning on every switch is a
    /// warning nobody reads.
    /// </summary>
    [Fact]
    public void ASwitchOnAButtonEliteLeavesAloneSaysNothing() => Assert.Null(CollisionOn(Binds()));

    /// <summary>
    /// <b>The device half is not optional.</b> This Commander's binds file carries four
    /// <c>DeviceIndex</c> values under one VID and PID, with <c>Joy_9</c> bound on two of them —
    /// so matching the button alone reported four collisions where one was real.
    /// </summary>
    [Fact]
    public void AnotherSticksButtonOfTheSameNumberIsNotACollision()
    {
        var binds = Binds(new EliteBinding("LandingGearToggle", "Secondary", "044FB10A", "Joy_9"));

        Assert.Null(CollisionOn(binds));
    }

    /// <summary>
    /// A device d47 cannot name to Elite is skipped rather than matched on the button alone: a
    /// warning naming the wrong stick is worse than no warning.
    /// </summary>
    [Fact]
    public void ADeviceWhoseNameCannotBeWorkedOutIsNotGuessedAt()
    {
        var binds = Binds(new EliteBinding("LandingGearToggle", "Secondary", "4098BD65", "Joy_9"));

        Assert.Null(CollisionOn(binds, GearSwitch(OtherStick, description: "HID-compliant game controller")));
    }

    /// <summary>
    /// <b>The silent half, said out loud.</b> The watch has always reported a state arriving and
    /// then going back — something fighting d47 — and never reported it not arriving at all. That
    /// is the shape the collision produced: two toggles cancelling, "Sent" in the log twice, and a
    /// gear that never moved.
    /// </summary>
    [Fact]
    public void APressWhoseStateNeverArrivesSaysSo()
    {
        var reconciler = New();
        var switches = new[] { GearSwitch() };
        var binds = Binds();

        // Sitting at "off" with the gear up, then flipped to "on": d47 presses.
        reconciler.Poll(Tick(Held(Stick, 8), binds), switches);
        reconciler.Poll(Tick(Held(Stick, 9), binds), switches);

        Assert.Single(reconciler.Drain(), pending => pending.Steps.Count > 0);

        // The gear never comes down, and the watch's window runs out.
        reconciler.Poll(
            new SwitchTick
            {
                Now = Start + TimeSpan.FromSeconds(30),
                Readings = [Held(Stick, 9)],
                Status = new GameStatus { Flags = StatusFlags.InMainShip, ReadAt = Start },
                Binds = binds,
                Enabled = true,
            },
            switches);

        var said = Assert.Single(reconciler.Drain());

        Assert.Contains("did not take", said.Say, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(said.Steps);
    }

    /// <summary>
    /// And a press that took says nothing, which is most of them. A notice on every successful
    /// press would be the feature reporting itself working.
    /// </summary>
    [Fact]
    public void APressThatTookSaysNothing()
    {
        var reconciler = New();
        var switches = new[] { GearSwitch() };
        var binds = Binds();

        reconciler.Poll(Tick(Held(Stick, 8), binds), switches);
        reconciler.Poll(Tick(Held(Stick, 9), binds), switches);

        reconciler.Drain();

        // The gear arrives, and the window runs out with it there.
        reconciler.Poll(Tick(Held(Stick, 9), binds, StatusFlags.LandingGearDown), switches);

        reconciler.Poll(
            new SwitchTick
            {
                Now = Start + TimeSpan.FromSeconds(30),
                Readings = [Held(Stick, 9)],
                Status = new GameStatus
                {
                    Flags = StatusFlags.InMainShip | StatusFlags.LandingGearDown,
                    ReadAt = Start,
                },
                Binds = binds,
                Enabled = true,
            },
            switches);

        Assert.Empty(reconciler.Drain());
    }
}
