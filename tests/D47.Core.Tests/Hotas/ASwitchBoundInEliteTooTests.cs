using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>A switch sitting on a button Elite binds as well.</summary>
public class ASwitchBoundInEliteTooTests
{
    private const string Stick = "{wgi/nrid/throttle}";
    private const string OtherStick = "{wgi/nrid/second-throttle}";

    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    /// <summary>The Commander's own throttle, as d47 describes it.</summary>
    private const string ThrottleDescription = "VID 0x4098 PID 0xBD65, 32 buttons, 0 hats, 0 axes";

    private static SwitchReconciler New() => new(NullLogger<SwitchReconciler>.Instance);

    /// <summary>
    /// The binding that caused it, verbatim from the report: landing gear on the keyboard's L, and on
    /// <c>Joy_9</c> of the throttle.
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

    /// <summary>The off-by-one, pinned.</summary>
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

    [Fact]
    public void ASwitchOnAButtonEliteLeavesAloneSaysNothing() => Assert.Null(CollisionOn(Binds()));

    /// <summary>The device half is not optional.</summary>
    [Fact]
    public void AnotherSticksButtonOfTheSameNumberIsNotACollision()
    {
        var binds = Binds(new EliteBinding("LandingGearToggle", "Secondary", "044FB10A", "Joy_9"));

        Assert.Null(CollisionOn(binds));
    }

    /// <summary>
    /// A device d47 cannot name to Elite is skipped rather than matched on the button alone: a warning
    /// naming the wrong stick is worse than no warning.
    /// </summary>
    [Fact]
    public void ADeviceWhoseNameCannotBeWorkedOutIsNotGuessedAt()
    {
        var binds = Binds(new EliteBinding("LandingGearToggle", "Secondary", "4098BD65", "Joy_9"));

        Assert.Null(CollisionOn(binds, GearSwitch(OtherStick, description: "HID-compliant game controller")));
    }

    /// <summary>The silent half, said out loud.</summary>
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
