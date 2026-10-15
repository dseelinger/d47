using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>A position that names a switch target rather than one action.</summary>
public class ASwitchPositionCanSetSeveralThingsTests
{
    private const string Stick = "{wgi/nrid/throttle}";

    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    private static readonly SwitchMapping LightsSwitch = new()
    {
        Name = "LIGHTS",
        DeviceId = Stick,
        Positions =
        [
            new SwitchPosition(1, Target: "lights_only"),
            new SwitchPosition(2, Target: "lights_and_night_vision_off"),
            new SwitchPosition(3, Target: "night_vision_only"),
        ],
    };

    private static readonly EliteBinds Binds = new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings =
        [
            new EliteBinding("ShipSpotLightToggle", "Primary", "Keyboard", "Key_Z"),
            new EliteBinding("NightVisionToggle", "Primary", "Keyboard", "Key_Insert"),
        ],
    };

    private static SwitchReconciler New() => new(NullLogger<SwitchReconciler>.Instance);

    private static SwitchTick Tick(int held, StatusFlags flags, DateTimeOffset? now = null)
    {
        var buttons = new bool[8];
        buttons[held] = true;

        return new SwitchTick
        {
            Now = now ?? Start,
            Readings = [new HotasReading { Id = Stick, Buttons = buttons }],
            Status = new GameStatus { Flags = flags | StatusFlags.InMainShip, ReadAt = now ?? Start },
            Binds = Binds,
            Enabled = true,
        };
    }

    private static int KeyDowns(PendingReconcile pending) =>
        pending.Steps.Count(step => step.Kind == InputStepKind.KeyDown);

    private static readonly StatusFlags[] Starts =
    [
        StatusFlags.None,
        StatusFlags.LightsOn,
        StatusFlags.NightVision,
        StatusFlags.LightsOn | StatusFlags.NightVision,
    ];

    /// <summary>Presses needed to go from a start to a target's parts.</summary>
    private static int Needed(StatusFlags start, bool lights, bool nightVision) =>
        (start.HasFlag(StatusFlags.LightsOn) != lights ? 1 : 0)
        + (start.HasFlag(StatusFlags.NightVision) != nightVision ? 1 : 0);

    [Fact]
    public void TheLightsSwitchPressesOnlyWhatIsOutOfPlaceFromEveryStart()
    {
        foreach (var start in Starts)
        {
            foreach (var (position, lights, nightVision) in new[] { (1, true, false), (2, false, false), (3, false, true) })
            {
                var other = position == 1 ? 2 : 1;
                var reconciler = New();
                var switches = new[] { LightsSwitch };

                reconciler.Poll(Tick(other, start), switches);
                reconciler.Poll(Tick(position, start), switches);

                var presses = reconciler.Drain().Sum(KeyDowns);

                Assert.Equal(Needed(start, lights, nightVision), presses);
            }
        }
    }

    [Fact]
    public void ATargetWithEveryPartInPlacePressesNothing()
    {
        var reconciler = New();
        var switches = new[] { LightsSwitch };

        reconciler.Poll(Tick(2, StatusFlags.None), switches);
        reconciler.Poll(Tick(1, StatusFlags.LightsOn), switches);

        Assert.Empty(reconciler.Drain());
    }

    [Fact]
    public void TheTapsOfOneTargetAreInOneReconcileInTheListedOrder()
    {
        var reconciler = New();
        var switches = new[] { LightsSwitch };

        reconciler.Poll(Tick(2, StatusFlags.NightVision), switches);
        reconciler.Poll(Tick(1, StatusFlags.NightVision), switches);

        var pending = Assert.Single(reconciler.Drain());
        Assert.Equal(2, KeyDowns(pending));
        Assert.Contains(pending.Steps, step => step.Kind == InputStepKind.Delay && step.Delay > InputSequence.TapHold);
    }

    [Fact]
    public void APartThatGoesBackOnItsOwnPausesTheSwitch()
    {
        var reconciler = New();
        var switches = new[] { LightsSwitch };

        reconciler.Poll(Tick(2, StatusFlags.None), switches);
        reconciler.Poll(Tick(1, StatusFlags.None), switches);
        reconciler.Drain();

        reconciler.Poll(Tick(1, StatusFlags.LightsOn, Start.AddSeconds(1)), switches);
        reconciler.Poll(Tick(1, StatusFlags.None, Start.AddSeconds(2)), switches);

        Assert.True(reconciler.IsPaused("LIGHTS"));
        Assert.Contains(reconciler.Drain(), pending => pending.Say?.Contains("Lights", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void ADisagreementNamesTheFirstPartOutOfPlace()
    {
        var reconciler = New();
        var switches = new[] { LightsSwitch };

        reconciler.Poll(Tick(3, StatusFlags.LightsOn), switches);

        Assert.Equal("Night vision is off", reconciler.States.Single().Disagrees, ignoreCase: true);
    }

    [Fact]
    public void AnUnknownTargetIsRefusedByName()
    {
        var mapping = LightsSwitch with
        {
            Positions = [new SwitchPosition(1, Target: "gear_party"), new SwitchPosition(2)],
        };

        Assert.Contains("gear_party", SwitchValidation.Problem(mapping), StringComparison.Ordinal);
    }

    [Fact]
    public void ATargetAndAnActionTogetherAreRefused()
    {
        var mapping = LightsSwitch with
        {
            Positions = [new SwitchPosition(1, "lights", Target: "lights_only"), new SwitchPosition(2)],
        };

        Assert.Equal(SwitchValidation.OneMeaning, SwitchValidation.Problem(mapping));
    }

    [Fact]
    public void ATargetsContextsAreItsPartsContextsTogether()
    {
        var expected = GameActions.Find("lights")!.Contexts | GameActions.Find("night_vision")!.Contexts;

        Assert.Equal(expected, LightsSwitch.Contexts);
    }

    [Fact]
    public void EveryTargetPartIsAnAssignableAction()
    {
        foreach (var target in SwitchTargets.All)
        {
            Assert.All(target.Parts, part => Assert.True(SwitchValidation.CanAssign(part.Action)));
        }
    }
}
