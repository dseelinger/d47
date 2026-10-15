using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>A spring-return switch pushed up and released between two polls.</summary>
public class ASwitchFlickBetweenPollsIsSeenTests
{
    private const string Stick = "{wgi/nrid/throttle}";

    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    private static readonly SwitchMapping GearSwitch = new()
    {
        Name = "LDG GEAR",
        DeviceId = Stick,
        Device = "VID 0x4098 PID 0xBD65, 32 buttons, 0 hats, 0 axes",
        Positions =
        [
            new SwitchPosition(9, "landing_gear", DesiredState.On),
            new SwitchPosition(null, "landing_gear", DesiredState.Off),
        ],
    };

    private static readonly EliteBinds Binds = new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings = [new EliteBinding("LandingGearToggle", "Primary", "Keyboard", "Key_L")],
    };

    private static SwitchTick Tick(
        int[] downSince,
        int[] held,
        StatusFlags flags = StatusFlags.InMainShip,
        DateTimeOffset? at = null)
    {
        var down = new bool[32];
        var now = new bool[32];

        foreach (var button in downSince)
        {
            down[button] = true;
        }

        foreach (var button in held)
        {
            now[button] = true;
        }

        return new SwitchTick
        {
            Now = at ?? Start,
            Readings = [new HotasReading { Id = Stick, Buttons = now, DownSinceLastRead = down }],
            Status = new GameStatus { Flags = flags | StatusFlags.InMainShip, ReadAt = at ?? Start },
            Binds = Binds,
            Enabled = true,
        };
    }

    [Fact]
    public void APositionDownOnlyBetweenPollsIsAFlipToIt()
    {
        var reconciler = new SwitchReconciler(NullLogger<SwitchReconciler>.Instance);
        var switches = new[] { GearSwitch };

        reconciler.Poll(Tick([], []), switches);
        reconciler.Poll(Tick([9], []), switches);

        var pending = Assert.Single(reconciler.Drain());
        Assert.NotEmpty(pending.Steps);
    }

    [Fact]
    public void WithNothingDownBetweenPollsNothingIsPressed()
    {
        var reconciler = new SwitchReconciler(NullLogger<SwitchReconciler>.Instance);
        var switches = new[] { GearSwitch };

        reconciler.Poll(Tick([], []), switches);
        reconciler.Poll(Tick([], []), switches);

        Assert.Empty(reconciler.Drain());
    }

    [Fact]
    public void ALatchedPositionStillHeldIsNotCountedTwice()
    {
        var reconciler = new SwitchReconciler(NullLogger<SwitchReconciler>.Instance);
        var switches = new[] { GearSwitch };

        reconciler.Poll(Tick([], []), switches);
        reconciler.Poll(Tick([9], [9]), switches);
        reconciler.Drain();
        reconciler.Poll(Tick([9], [9]), switches);

        Assert.Empty(reconciler.Drain());
    }

    [Fact]
    public void ASwitchMovedThroughAPositionToAnotherPressesOnlyWhereItStops()
    {
        var reconciler = new SwitchReconciler(NullLogger<SwitchReconciler>.Instance);

        var rotary = new[]
        {
            GearSwitch with
            {
                Positions =
                [
                    new SwitchPosition(1),
                    new SwitchPosition(2, "landing_gear", DesiredState.On),
                    new SwitchPosition(3),
                ],
            },
        };

        reconciler.Poll(Tick([1], [1]), rotary);
        reconciler.Poll(Tick([1, 2, 3], [3]), rotary);

        Assert.Empty(reconciler.Drain());
    }

    [Fact]
    public void TwoPositionsFlickedBetweenPollsPressNothingBecauseTheirOrderIsUnknown()
    {
        var reconciler = new SwitchReconciler(NullLogger<SwitchReconciler>.Instance);

        var threeWay = new[]
        {
            GearSwitch with
            {
                Positions =
                [
                    new SwitchPosition(null),
                    new SwitchPosition(8, "landing_gear", DesiredState.On),
                    new SwitchPosition(9, "landing_gear", DesiredState.Off),
                ],
            },
        };

        reconciler.Poll(Tick([], []), threeWay);
        reconciler.Poll(Tick([8, 9], []), threeWay);

        Assert.Empty(reconciler.Drain());
    }

    [Fact]
    public void APausedSwitchPressesNothingForAFlick()
    {
        var reconciler = new SwitchReconciler(NullLogger<SwitchReconciler>.Instance);
        var switches = new[] { GearSwitch };

        reconciler.Poll(Tick([], []), switches);
        reconciler.Poll(Tick([9], [9]), switches);
        reconciler.Poll(Tick([9], [9], StatusFlags.LandingGearDown, Start.AddMilliseconds(300)), switches);
        reconciler.Poll(Tick([9], [9], StatusFlags.None, Start.AddMilliseconds(600)), switches);
        Assert.True(reconciler.IsPaused(GearSwitch.Name));

        reconciler.Poll(Tick([9], [], StatusFlags.None, Start.AddSeconds(1)), switches);
        reconciler.Drain();

        reconciler.Poll(Tick([9], [], StatusFlags.None, Start.AddSeconds(2)), switches);

        Assert.Empty(reconciler.Drain());
    }

    [Fact]
    public void AFlickWhosePressNeverTakesIsSaid()
    {
        var reconciler = new SwitchReconciler(NullLogger<SwitchReconciler>.Instance);
        var switches = new[] { GearSwitch };

        reconciler.Poll(Tick([], []), switches);
        reconciler.Poll(Tick([9], []), switches);
        Assert.Single(reconciler.Drain());

        var late = Start + SwitchReconciler.ContestWindow + TimeSpan.FromSeconds(1);
        reconciler.Poll(Tick([], [], StatusFlags.None, late), switches);

        var said = Assert.Single(reconciler.Drain());
        Assert.Empty(said.Steps);
        Assert.Contains("did not take", said.Say ?? string.Empty, StringComparison.Ordinal);
    }
}
