using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>
/// A switch position meaning a state rather than a press (list.md Phase 21, items 3 to 6).
/// <para>
/// The assertions that matter most here are the ones about <em>nothing happening</em>. Gear
/// already down and the switch moved to down must do nothing at all; a switch left alone while
/// the game changes its own mind must do nothing at all. Every existing remapper gets both of
/// those wrong, and getting them wrong is silent.
/// </para>
/// </summary>
public class SwitchReconcilerTests
{
    private const string Stick = "{wgi/nrid/throttle}";

    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    private static SwitchReconciler New() => new(NullLogger<SwitchReconciler>.Instance);

    private static EliteBinds Binds() => new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings =
        [
            new EliteBinding("LandingGearToggle", "Primary", "Keyboard", "Key_L"),
            new EliteBinding("ShipSpotLightToggle", "Primary", "Keyboard", "Key_Insert"),
            new EliteBinding("DeployHardpointToggle", "Primary", "Keyboard", "Key_U"),
        ],
    };

    private static GameStatus Status(StatusFlags flags) =>
        new() { Flags = flags | StatusFlags.InMainShip, ReadAt = Start };

    private static HotasReading Held(params int[] held)
    {
        var buttons = new bool[32];

        foreach (var button in held)
        {
            buttons[button] = true;
        }

        return new HotasReading { Id = Stick, Buttons = buttons };
    }

    /// <summary>A gear switch: button 8 means down, button 9 means up.</summary>
    private static SwitchMapping GearSwitch(string device = Stick) => new()
    {
        Name = "gear switch",
        DeviceId = device,
        Device = "VID 0x4098 PID 0xBD65, 32 buttons, 0 hats, 7 axes",
        Positions =
        [
            new SwitchPosition(8, "landing_gear", DesiredState.On),
            new SwitchPosition(9, "landing_gear", DesiredState.Off),
        ],
    };

    private static SwitchTick Tick(
        HotasReading reading,
        StatusFlags flags,
        DateTimeOffset? at = null,
        bool enabled = true) => new()
        {
            Now = at ?? Start,
            Readings = [reading],
            Status = Status(flags),
            Binds = Binds(),
            Enabled = enabled,
        };

    [Fact]
    public void FindingASwitchWhereItAlreadyIsPressesNothing()
    {
        // Startup, and after a reconnect. d47 learns where the switch is sitting rather than
        // acting on finding it there — otherwise every start would flip something.
        var reconciler = New();

        reconciler.Poll(Tick(Held(9), StatusFlags.LandingGearDown), [GearSwitch()]);

        Assert.Empty(reconciler.Drain());
    }

    [Fact]
    public void AFlipToAStateTheGameIsNotInPressesTheBindingOnce()
    {
        var reconciler = New();
        var switches = new[] { GearSwitch() };

        reconciler.Poll(Tick(Held(9), StatusFlags.None), switches);
        reconciler.Poll(Tick(Held(8), StatusFlags.None), switches);

        var pending = reconciler.Drain();

        Assert.Single(pending);
        Assert.Equal("gear switch", pending[0].Switch);
        Assert.NotEmpty(pending[0].Steps);
    }

    [Fact]
    public void AFlipToAStateTheGameIsAlreadyInDoesNothingAtAll()
    {
        // The entire point of the phase. Gear already down from a voice command, switch moved to
        // down — a remapper presses the toggle here and retracts the gear on approach.
        var reconciler = New();
        var switches = new[] { GearSwitch() };

        reconciler.Poll(Tick(Held(9), StatusFlags.LandingGearDown), switches);
        reconciler.Poll(Tick(Held(8), StatusFlags.LandingGearDown), switches);

        Assert.Empty(reconciler.Drain());
    }

    [Fact]
    public void BetweenFlipsNothingIsTouchedHoweverFarTheGameDrifts()
    {
        // The switch says down and the game raises the gear by itself. That is a disagreement to
        // *show*, never one to correct: correcting it would be d47 acting without being asked.
        var reconciler = New();
        var switches = new[] { GearSwitch() };

        reconciler.Poll(Tick(Held(8), StatusFlags.LandingGearDown), switches);
        reconciler.Poll(Tick(Held(8), StatusFlags.None), switches);
        reconciler.Poll(Tick(Held(8), StatusFlags.None), switches);

        Assert.Empty(reconciler.Drain());
        Assert.Single(reconciler.Disagreeing);
        Assert.Equal("the landing gear is off", reconciler.Disagreeing[0].Disagrees);
    }

    [Fact]
    public void ASwitchThatAgreesWithTheGameIsNotAnnunciated()
    {
        var reconciler = New();

        reconciler.Poll(Tick(Held(8), StatusFlags.LandingGearDown), [GearSwitch()]);

        Assert.Empty(reconciler.Disagreeing);
    }

    [Fact]
    public void ASwitchIsNotDisagreeingAboutSomethingItsActionCannotDoRightNow()
    {
        // A gear switch is not disagreeing with anything while the Commander is on foot. Mode is
        // the same ControlContext that decides what is worth advertising.
        var reconciler = New();

        reconciler.Poll(
            new SwitchTick
            {
                Now = Start,
                Readings = [Held(8)],
                Status = new GameStatus { Flags2 = 1, ReadAt = Start },
                Binds = Binds(),
                Enabled = true,
            },
            [GearSwitch()]);

        Assert.Empty(reconciler.Disagreeing);
    }

    [Fact]
    public void AMappingWhoseDeviceIsGoneAsksToBeReassignedAndPressesNothing()
    {
        // Item 4. Turning 4x32 mode on or off renumbers every button on the throttle while
        // leaving VID+PID identical, so a VID+PID key would drive whatever now sits at button 8.
        // The NonRoamableId changes, so the mapping fails closed instead.
        var reconciler = New();
        var switches = new[] { GearSwitch("{wgi/nrid/before-the-mode-change}") };

        reconciler.Poll(Tick(Held(8), StatusFlags.None), switches);
        reconciler.Poll(Tick(Held(9), StatusFlags.None), switches);

        Assert.Empty(reconciler.Drain());
        Assert.Equal(SwitchHealth.Reassign, reconciler.States[0].Health);
        Assert.Contains("Assign it again", reconciler.States[0].Note, StringComparison.Ordinal);
    }

    [Fact]
    public void NoControllersYetIsNotTheSameAnswerAsNeedsReassigning()
    {
        // The device list fills in over seconds rather than at startup, so for the first moments
        // of every run every switch is missing its device. Reporting that as "reassign this" is a
        // false alarm about a good mapping, once per launch.
        var reconciler = New();

        reconciler.Poll(
            new SwitchTick
            {
                Now = Start,
                Readings = [],
                Status = Status(StatusFlags.None),
                Binds = Binds(),
                Enabled = true,
            },
            [GearSwitch()]);

        Assert.Equal(SwitchHealth.Waiting, reconciler.States[0].Health);
    }

    [Fact]
    public void ADeviceComingBackDoesNotPressAnythingOnItsFirstSighting()
    {
        // Unplug and replug is the same shape as startup: the switch is wherever it is, and
        // finding it there is not the Commander asking for anything.
        var reconciler = New();
        var switches = new[] { GearSwitch() };

        reconciler.Poll(Tick(Held(8), StatusFlags.LandingGearDown), switches);

        reconciler.Poll(
            new SwitchTick
            {
                Now = Start,
                Readings = [],
                Status = Status(StatusFlags.None),
                Binds = Binds(),
                Enabled = true,
            },
            switches);

        reconciler.Poll(Tick(Held(9), StatusFlags.None), switches);

        Assert.Empty(reconciler.Drain());
    }

    [Fact]
    public void NothingIsPressedWhileTheFeatureIsSwitchedOff()
    {
        var reconciler = New();
        var switches = new[] { GearSwitch() };

        reconciler.Poll(Tick(Held(9), StatusFlags.None, enabled: false), switches);
        reconciler.Poll(Tick(Held(8), StatusFlags.None, enabled: false), switches);

        Assert.Empty(reconciler.Drain());
        Assert.Equal(SwitchHealth.Off, reconciler.States[0].Health);
    }

    [Fact]
    public void AnUnboundActionRefusesWithTheReasonRatherThanInSilence()
    {
        var reconciler = New();

        var switches = new[]
        {
            GearSwitch() with
            {
                Positions =
                [
                    new SwitchPosition(8, "cargo_scoop", DesiredState.On),
                    new SwitchPosition(9, "cargo_scoop", DesiredState.Off),
                ],
            },
        };

        reconciler.Poll(Tick(Held(9), StatusFlags.None), switches);
        reconciler.Poll(Tick(Held(8), StatusFlags.None), switches);

        Assert.Empty(reconciler.Drain());
        Assert.NotEmpty(reconciler.States[0].Note);
    }

    [Fact]
    public void AnUnassignedPositionMeansNothingAndPressesNothing()
    {
        var reconciler = New();

        var switches = new[]
        {
            GearSwitch() with
            {
                Positions =
                [
                    new SwitchPosition(8, "landing_gear", DesiredState.On),
                    new SwitchPosition(9),
                ],
            },
        };

        reconciler.Poll(Tick(Held(8), StatusFlags.LandingGearDown), switches);
        reconciler.Poll(Tick(Held(9), StatusFlags.LandingGearDown), switches);

        Assert.Empty(reconciler.Drain());
    }

    [Fact]
    public void AReconcileThatIsImmediatelyUndonePausesTheSwitchAndSaysSo()
    {
        // Item 5. Something else is bound to the same action — a leftover Elite binding, or a
        // vJoy device SimApp Pro is publishing. It cannot be detected statically, so the symptom
        // is watched instead.
        var reconciler = New();
        var switches = new[] { GearSwitch() };

        reconciler.Poll(Tick(Held(9), StatusFlags.None), switches);
        reconciler.Poll(Tick(Held(8), StatusFlags.None), switches);
        Assert.Single(reconciler.Drain());

        // The press lands: the game reports the gear down.
        reconciler.Poll(Tick(Held(8), StatusFlags.LandingGearDown, Start.AddMilliseconds(300)), switches);

        // And then it goes back, with nothing having moved.
        reconciler.Poll(Tick(Held(8), StatusFlags.None, Start.AddMilliseconds(600)), switches);

        var said = reconciler.Drain();

        Assert.Equal(SwitchHealth.Paused, reconciler.States[0].Health);
        Assert.True(reconciler.IsPaused("gear switch"));
        Assert.Single(said);
        Assert.Empty(said[0].Steps);
        Assert.Contains("Something else is bound to", said[0].Say ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void APausedSwitchStopsReconcilingUntilItIsResumed()
    {
        var reconciler = New();
        var switches = new[] { GearSwitch() };

        reconciler.Poll(Tick(Held(9), StatusFlags.None), switches);
        reconciler.Poll(Tick(Held(8), StatusFlags.None), switches);
        reconciler.Poll(Tick(Held(8), StatusFlags.LandingGearDown, Start.AddMilliseconds(300)), switches);
        reconciler.Poll(Tick(Held(8), StatusFlags.None, Start.AddMilliseconds(600)), switches);
        reconciler.Drain();

        // A flip while paused does nothing at all.
        reconciler.Poll(Tick(Held(9), StatusFlags.LandingGearDown, Start.AddSeconds(10)), switches);
        Assert.Empty(reconciler.Drain());

        reconciler.Resume("gear switch");

        // Resuming presses nothing by itself — the switch is wherever it is, and the next flip
        // is the next question.
        reconciler.Poll(Tick(Held(9), StatusFlags.LandingGearDown, Start.AddSeconds(11)), switches);
        Assert.Empty(reconciler.Drain());

        reconciler.Poll(Tick(Held(8), StatusFlags.None, Start.AddSeconds(12)), switches);
        Assert.Single(reconciler.Drain());
    }

    [Fact]
    public void AModeChangeExplainsTheChangeBackAndIsNotCountedAsAContest()
    {
        // Hardpoints retract by themselves on entering supercruise. That is the game doing its
        // job, not a second binding — and it is the one false positive that is not hypothetical.
        var reconciler = New();

        var switches = new[]
        {
            GearSwitch() with
            {
                Name = "hardpoint switch",
                Positions =
                [
                    new SwitchPosition(8, "hardpoints", DesiredState.On),
                    new SwitchPosition(9, "hardpoints", DesiredState.Off),
                ],
            },
        };

        reconciler.Poll(Tick(Held(9), StatusFlags.None), switches);
        reconciler.Poll(Tick(Held(8), StatusFlags.None), switches);
        Assert.Single(reconciler.Drain());

        reconciler.Poll(Tick(Held(8), StatusFlags.HardpointsDeployed, Start.AddMilliseconds(300)), switches);

        // Into supercruise, and the hardpoints come in by themselves.
        reconciler.Poll(Tick(Held(8), StatusFlags.Supercruise, Start.AddMilliseconds(600)), switches);

        Assert.False(reconciler.IsPaused("hardpoint switch"));
        Assert.Empty(reconciler.Drain());
    }

    [Fact]
    public void AChangeBackLongAfterTheReconcileIsNotBlamedOnIt()
    {
        var reconciler = New();
        var switches = new[] { GearSwitch() };

        reconciler.Poll(Tick(Held(9), StatusFlags.None), switches);
        reconciler.Poll(Tick(Held(8), StatusFlags.None), switches);
        reconciler.Drain();

        reconciler.Poll(Tick(Held(8), StatusFlags.LandingGearDown, Start.AddMilliseconds(300)), switches);

        // Half a minute later the Commander raises the gear themselves. Nothing to do with the
        // switch, and blaming it would pause a switch that works.
        reconciler.Poll(Tick(Held(8), StatusFlags.None, Start.AddSeconds(30)), switches);

        Assert.False(reconciler.IsPaused("gear switch"));
    }

    [Fact]
    public void AReadingThatNamesNoPositionIsUnknownRatherThanUnchanged()
    {
        // Two of the switch's own buttons held at once is not a position it was walked through.
        // Reading it as "still where it last was" is the silent failure the phase exists to
        // prevent, so nothing is pressed and the state says it cannot tell.
        var reconciler = New();
        var switches = new[] { GearSwitch() };

        reconciler.Poll(Tick(Held(9), StatusFlags.None), switches);
        reconciler.Poll(Tick(Held(8, 9), StatusFlags.None), switches);

        Assert.Empty(reconciler.Drain());
        Assert.Contains("cannot tell", reconciler.States[0].Note, StringComparison.Ordinal);
    }

    [Fact]
    public void APositionThatHoldsNothingIsAPositionAndDrivesAState()
    {
        // The centre-holds-nothing switch the spike could not rule out. It has to reconcile like
        // any other position, or a Commander with one has a switch that half works.
        var reconciler = New();

        var switches = new[]
        {
            GearSwitch() with
            {
                Positions =
                [
                    new SwitchPosition(8, "landing_gear", DesiredState.On),
                    new SwitchPosition(null, "landing_gear", DesiredState.Off),
                ],
            },
        };

        reconciler.Poll(Tick(Held(8), StatusFlags.LandingGearDown), switches);
        reconciler.Poll(Tick(Held(), StatusFlags.LandingGearDown), switches);

        var pending = reconciler.Drain();

        Assert.Single(pending);
        Assert.NotEmpty(pending[0].Steps);
    }
}
