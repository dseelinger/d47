using System.Text.Json;
using D47.Core.Actions;
using D47.Core.Callouts;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>
/// The first member of the autonomous-action category (list.md Phase 10, items 2 and 3).
/// <para>
/// The interesting cases are all the ones where it decides <em>not</em> to fire. Nobody asked
/// for this, so nobody is watching it work, and every wrong firing is d47 pressing the
/// Commander's trigger at a moment they did not choose.
/// </para>
/// </summary>
public class HonkOnArrivalTests
{
    private static readonly EliteBinds Bound = new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings = [new EliteBinding("PrimaryFire", "Primary", "Mouse", "Mouse_1")],
    };

    private static JournalEvent Jump() => new(
        DateTimeOffset.UnixEpoch, "FSDJump", JsonDocument.Parse("{}").RootElement.Clone());

    private static GameStatus Status(StatusFlags flags) => new()
    {
        Flags = flags,
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    private static CalloutContext Context(
        GameStatus status,
        bool priming = false,
        DateTimeOffset? now = null,
        params JournalEvent[] events) =>
        new(now ?? DateTimeOffset.UnixEpoch, priming, null, status, NavRoute.None, events);

    /// <summary>
    /// Where a jump actually puts the Commander down: supercruise, in analysis mode.
    /// <para>
    /// This used to be <c>InMainShip | AnalysisMode</c> — normal space — which is a state the
    /// far end of a hyperspace jump is never in, and it is why every test here passed while the
    /// honk never once fired in the game.
    /// </para>
    /// </summary>
    private static readonly StatusFlags Arrived =
        StatusFlags.InMainShip | StatusFlags.Supercruise | StatusFlags.AnalysisMode;

    /// <summary>Dropped out of supercruise before the arm was spent. Also a honk.</summary>
    private static readonly StatusFlags Dropped =
        StatusFlags.InMainShip | StatusFlags.AnalysisMode;

    private static HonkOnArrival Honk(bool enabled = true, EliteBinds? binds = null) =>
        new(() => enabled, () => binds ?? Bound);

    [Fact]
    public void ArrivingInAnalysisModeHoldsTheFireButtonForTheFullCharge()
    {
        var honk = Honk();

        // The jump event arrives while still in the tunnel.
        honk.Examine(Context(Status(StatusFlags.InMainShip | StatusFlags.FsdJump), events: Jump()));

        var decision = honk.Examine(Context(Status(Arrived)));

        Assert.True(decision.Acts);
        Assert.Equal(TimeSpan.FromSeconds(5.3), HonkOnArrival.Charge);
        Assert.Equal(HonkOnArrival.Charge, decision.Steps.Single(s => s.Kind == InputStepKind.Delay).Delay);
        Assert.Equal(InputStepKind.MouseDown, decision.Steps[0].Kind);
        Assert.Equal(InputStepKind.MouseUp, decision.Steps[^1].Kind);
    }

    /// <summary>
    /// Both halves of flying count. The arrival itself is in supercruise, and a Commander who
    /// drops out before the arm expires is still owed the honk they asked for.
    /// </summary>
    [Fact]
    public void ItFiresInNormalSpaceToo()
    {
        var honk = Honk();
        honk.Examine(Context(Status(StatusFlags.InMainShip | StatusFlags.FsdJump), events: Jump()));

        Assert.True(honk.Examine(Context(Status(Dropped))).Acts);
    }

    [Fact]
    public void NothingFiresWhileStillInWitchspace()
    {
        // The game has the controls during the tunnel, and a held key goes nowhere.
        var honk = Honk();

        var decision = honk.Examine(
            Context(Status(StatusFlags.InMainShip | StatusFlags.FsdJump), events: Jump()));

        Assert.False(decision.Acts);
    }

    [Fact]
    public void ItFiresOnceAndThenDisarms()
    {
        var honk = Honk();
        honk.Examine(Context(Status(StatusFlags.InMainShip | StatusFlags.FsdJump), events: Jump()));

        Assert.True(honk.Examine(Context(Status(Arrived))).Acts);
        Assert.False(honk.Examine(Context(Status(Arrived))).Acts);
    }

    [Fact]
    public void AStartupBacklogOfJumpsArmsNothing()
    {
        // A honk for every system visited since lunch is a worse bug than never honking.
        var honk = Honk();

        honk.Examine(Context(Status(Arrived), priming: true, events: [Jump(), Jump(), Jump()]));

        Assert.False(honk.Examine(Context(Status(Arrived))).Acts);
    }

    [Fact]
    public void AnArmThatWasNeverSpentExpires()
    {
        // A Commander who has already turned and burned does not want their guns cycling
        // ninety seconds later.
        var honk = Honk();
        honk.Examine(Context(Status(StatusFlags.InMainShip | StatusFlags.FsdJump), events: Jump()));

        var late = DateTimeOffset.UnixEpoch + HonkOnArrival.Window + TimeSpan.FromSeconds(1);

        Assert.False(honk.Examine(Context(Status(Arrived), now: late)).Acts);
    }

    [Fact]
    public void CombatModeSaysWhyRatherThanSwitchingModesOnTheCommandersBehalf()
    {
        // Switching modes would be a second autonomous action wearing the first one's consent.
        var honk = Honk();
        honk.Examine(Context(Status(StatusFlags.InMainShip | StatusFlags.FsdJump), events: Jump()));

        var decision = honk.Examine(Context(Status(StatusFlags.InMainShip)));

        Assert.False(decision.Acts);
        Assert.Contains("analysis mode", decision.Say ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AFireButtonOnTheJoystickSaysSoRatherThanFailingSilently()
    {
        var stick = new EliteBinds
        {
            PresetName = "Test",
            SourceFile = "Test.binds",
            Bindings = [new EliteBinding("PrimaryFire", "Primary", "Joystick", "Joy_1")],
        };

        var honk = Honk(binds: stick);
        honk.Examine(Context(Status(StatusFlags.InMainShip | StatusFlags.FsdJump), events: Jump()));

        var decision = honk.Examine(Context(Status(Arrived)));

        Assert.False(decision.Acts);
        Assert.Contains("joystick", decision.Say ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ASwitchedOffActionIsNeverEvenExamined()
    {
        // Not an optimisation: an action that is off must not accumulate state either, so
        // switching it on mid-session starts it from now.
        var runner = new AutonomousActionRunner(NullLogger<AutonomousActionRunner>.Instance)
            .Add(Honk(enabled: false));

        runner.Tick(Context(Status(StatusFlags.InMainShip | StatusFlags.FsdJump), events: Jump()));
        runner.Tick(Context(Status(Arrived)));

        Assert.Empty(runner.Drain());
    }

    [Fact]
    public void EveryAutonomousActionIsOffInADefaultInstall()
    {
        // The category's rule, asserted rather than trusted: a new member arriving switched on
        // is consent for one thing being spent on another.
        var defaults = D47.Core.Configuration.D47Settings.Defaults;

        Assert.False(defaults.Actions.HonkOnArrival);
        Assert.False(defaults.Actions.Keyboard);
    }

    [Fact]
    public void OneActionThrowingDoesNotStopTheOthers()
    {
        var runner = new AutonomousActionRunner(NullLogger<AutonomousActionRunner>.Instance)
            .Add(new Throwing())
            .Add(Honk());

        runner.Tick(Context(Status(StatusFlags.InMainShip | StatusFlags.FsdJump), events: Jump()));
        runner.Tick(Context(Status(Arrived)));

        Assert.Single(runner.Drain());
    }

    private sealed class Throwing : IAutonomousAction
    {
        public string Id => "throwing";

        public string Label => "a broken action";

        public bool IsEnabled => true;

        public AutonomousDecision Examine(CalloutContext context) => throw new InvalidOperationException();
    }
}
