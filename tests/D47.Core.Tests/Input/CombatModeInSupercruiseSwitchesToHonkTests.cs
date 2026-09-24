using System.Text.Json;
using D47.Core.Actions;
using D47.Core.Callouts;
using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>Arriving in combat mode in supercruise: switch to analysis mode, honk, switch back (#457).</summary>
public class CombatModeInSupercruiseSwitchesToHonkTests
{
    private static readonly EliteBinding Fire = new("PrimaryFire", "Primary", "Mouse", "Mouse_1");

    private static readonly EliteBinding HudMode = new("PlayerHUDModeToggle", "Primary", "Keyboard", "Key_M");

    private static readonly EliteBinds Bound = new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings = [Fire, HudMode],
    };

    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    private static readonly StatusFlags CombatInSupercruise = StatusFlags.InMainShip | StatusFlags.Supercruise;

    private static readonly StatusFlags AnalysisInSupercruise = CombatInSupercruise | StatusFlags.AnalysisMode;

    private static CalloutContext At(StatusFlags flags, TimeSpan after, bool jump = false, bool scan = false) =>
        new(
            Start + after,
            false,
            null,
            new GameStatus { Flags = flags, ReadAt = Start + after },
            NavRoute.None,
            [
                .. jump ? [Event("FSDJump", after)] : Array.Empty<JournalEvent>(),
                .. scan ? [Event("FSSDiscoveryScan", after)] : Array.Empty<JournalEvent>(),
            ]);

    private static JournalEvent Event(string kind, TimeSpan after) =>
        new(Start + after, kind, JsonDocument.Parse("{}").RootElement.Clone());

    private static HonkOnArrival Armed(EliteBinds? binds = null)
    {
        var honk = new HonkOnArrival(() => true, () => binds ?? Bound);
        honk.Examine(At(StatusFlags.InMainShip | StatusFlags.FsdJump, TimeSpan.Zero, jump: true));
        return honk;
    }

    private static bool IsTapOf(AutonomousDecision decision, EliteBinding binding) =>
        decision.Steps.SequenceEqual(InputSequence.Tap(binding));

    private static bool IsHoldOfFire(AutonomousDecision decision) =>
        decision.Steps.SequenceEqual(InputSequence.Hold(Fire, HonkOnArrival.Charge));

    [Fact]
    public void ItSwitchesHoldsOnceTheStatusShowsAnalysisModeAndSwitchesBack()
    {
        var honk = Armed();

        var switched = honk.Examine(At(CombatInSupercruise, TimeSpan.FromSeconds(1)));
        Assert.True(IsTapOf(switched, HudMode));
        Assert.Null(switched.Say);

        // The status has not caught up yet: no hold on a guess.
        Assert.False(honk.Examine(At(CombatInSupercruise, TimeSpan.FromSeconds(1.1))).Acts);

        var hold = honk.Examine(At(AnalysisInSupercruise, TimeSpan.FromSeconds(1.2)));
        Assert.True(IsHoldOfFire(hold));

        // Not while the scanner is still charging.
        Assert.False(honk.Examine(At(AnalysisInSupercruise, TimeSpan.FromSeconds(4))).Acts);
        Assert.False(honk.Examine(At(AnalysisInSupercruise, TimeSpan.FromSeconds(6), scan: true)).Acts);

        var back = honk.Examine(At(
            AnalysisInSupercruise, TimeSpan.FromSeconds(1.2) + HonkOnArrival.Charge + HonkOnArrival.Settle));
        Assert.True(IsTapOf(back, HudMode));

        Assert.False(honk.Examine(At(CombatInSupercruise, TimeSpan.FromSeconds(10))).Acts);
    }

    [Fact]
    public void ArrivingInAnalysisModeOnlyHolds()
    {
        var honk = Armed();

        Assert.True(IsHoldOfFire(honk.Examine(At(AnalysisInSupercruise, TimeSpan.FromSeconds(1)))));
        Assert.False(honk.Examine(At(AnalysisInSupercruise, TimeSpan.FromSeconds(6.5), scan: true)).Acts);

        for (var second = 7; second < 30; second++)
        {
            Assert.False(honk.Examine(At(AnalysisInSupercruise, TimeSpan.FromSeconds(second))).Acts);
        }
    }

    [Fact]
    public void ASwitchThatNeverTakesIsSaidAndNothingIsHeld()
    {
        var honk = Armed();

        Assert.True(IsTapOf(honk.Examine(At(CombatInSupercruise, TimeSpan.FromSeconds(1))), HudMode));
        Assert.False(honk.Examine(At(CombatInSupercruise, TimeSpan.FromSeconds(2.5))).Acts);

        var gaveUp = honk.Examine(At(CombatInSupercruise, TimeSpan.FromSeconds(3.1)));

        Assert.False(gaveUp.Acts);
        Assert.Equal("I could not switch to analysis mode to honk", gaveUp.Say);

        for (var second = 4; second < 30; second++)
        {
            Assert.False(honk.Examine(At(AnalysisInSupercruise, TimeSpan.FromSeconds(second))).Acts);
        }
    }

    [Fact]
    public void ItDoesNotSwitchBackOnceTheShipHasDroppedOrTheCommanderHasSwitched()
    {
        var dropped = Armed();
        dropped.Examine(At(CombatInSupercruise, TimeSpan.FromSeconds(1)));
        dropped.Examine(At(AnalysisInSupercruise, TimeSpan.FromSeconds(1.2)));
        Assert.False(dropped.Examine(At(
            StatusFlags.InMainShip | StatusFlags.AnalysisMode, TimeSpan.FromSeconds(8))).Acts);

        var commander = Armed();
        commander.Examine(At(CombatInSupercruise, TimeSpan.FromSeconds(1)));
        commander.Examine(At(AnalysisInSupercruise, TimeSpan.FromSeconds(1.2)));
        Assert.False(commander.Examine(At(CombatInSupercruise, TimeSpan.FromSeconds(8))).Acts);
    }

    [Fact]
    public void AnUnboundHudToggleIsNamedRatherThanCombatMode()
    {
        var honk = Armed(new EliteBinds { PresetName = "Test", SourceFile = "Test.binds", Bindings = [Fire] });

        var decision = honk.Examine(At(CombatInSupercruise, TimeSpan.FromSeconds(1)));

        Assert.False(decision.Acts);
        Assert.StartsWith(
            "I did not honk: you are in combat mode, and I cannot switch to analysis mode.", decision.Say);
        Assert.Contains("no binding for the HUD mode", decision.Say);
    }
}
