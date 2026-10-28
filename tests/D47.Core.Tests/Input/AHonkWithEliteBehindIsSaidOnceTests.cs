using System.Text.Json;
using D47.Core.Actions;
using D47.Core.Callouts;
using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>With Elite behind another window the honk sends nothing, tries once more, then says so once (#1838).</summary>
public class AHonkWithEliteBehindIsSaidOnceTests
{
    private static readonly EliteBinding Fire = new("PrimaryFire", "Primary", "Mouse", "Mouse_1");

    private static readonly EliteBinding HudMode = new("PlayerHUDModeToggle", "Primary", "Keyboard", "Key_M");

    private static readonly EliteBinds Bound = new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings = [Fire, HudMode],
    };

    private static readonly DateTimeOffset Jumped = DateTimeOffset.UnixEpoch;

    private static readonly StatusFlags Witchspace = StatusFlags.InMainShip | StatusFlags.FsdJump;

    private static readonly StatusFlags Analysis =
        StatusFlags.InMainShip | StatusFlags.Supercruise | StatusFlags.AnalysisMode;

    private static readonly StatusFlags Combat = StatusFlags.InMainShip | StatusFlags.Supercruise;

    private static readonly double Spacing = (HonkOnArrival.Charge + HonkOnArrival.Confirmation).TotalSeconds;

    private bool _inFront;

    private static CalloutContext At(StatusFlags flags, double seconds, params string[] events)
    {
        var now = Jumped + TimeSpan.FromSeconds(seconds);

        return new(
            now,
            false,
            null,
            new GameStatus { Flags = flags, ReadAt = now },
            NavRoute.None,
            [.. events.Select(kind => new JournalEvent(now, kind, JsonDocument.Parse("{}").RootElement.Clone()))]);
    }

    private static bool IsHoldOfFire(AutonomousDecision decision) =>
        decision.Steps.SequenceEqual(InputSequence.Hold(Fire, HonkOnArrival.Charge));

    private static bool IsTapOfHudMode(AutonomousDecision decision) =>
        decision.Steps.SequenceEqual(InputSequence.Tap(HudMode));

    private HonkOnArrival Jump()
    {
        var honk = new HonkOnArrival(() => true, () => Bound, () => _inFront);
        honk.Examine(At(Witchspace, 0, "FSDJump"));
        return honk;
    }

    private static void Silent(AutonomousDecision decision)
    {
        Assert.False(decision.Acts);
        Assert.Null(decision.Say);
    }

    private static void NothingMoreUntil(HonkOnArrival honk, StatusFlags flags, double from, double until)
    {
        for (var second = from; second < until; second += 0.1)
        {
            Silent(honk.Examine(At(flags, second)));
        }
    }

    [Fact]
    public void BehindOnBothTriesSaysOneSentenceAndSendsNothing()
    {
        var honk = Jump();

        Silent(honk.Examine(At(Analysis, 1)));
        NothingMoreUntil(honk, Analysis, 1.1, 1 + Spacing - 0.05);

        var gaveUp = honk.Examine(At(Analysis, 1 + Spacing));

        Assert.False(gaveUp.Acts);
        Assert.Equal("Cannot honk since Elite is not the active window.", gaveUp.Say);

        NothingMoreUntil(honk, Analysis, 1 + Spacing + 0.1, 60);
    }

    [Fact]
    public void BehindThenInFrontHoldsOnTheSecondTry()
    {
        var honk = Jump();

        Silent(honk.Examine(At(Analysis, 1)));

        _inFront = true;
        NothingMoreUntil(honk, Analysis, 1.1, 1 + Spacing - 0.05);

        var second = honk.Examine(At(Analysis, 1 + Spacing));

        Assert.True(IsHoldOfFire(second));
        Assert.Null(second.Say);

        Assert.False(honk.Examine(At(Analysis, 1 + Spacing + 1, "FSSDiscoveryScan")).Acts);
        NothingMoreUntil(honk, Analysis, 1 + Spacing + 1.1, 60);
    }

    [Fact]
    public void SentWithNoScanThenBehindSaysOnlyTheNewSentence()
    {
        _inFront = true;
        var honk = Jump();
        Assert.True(IsHoldOfFire(honk.Examine(At(Analysis, 1))));

        _inFront = false;
        NothingMoreUntil(honk, Analysis, 1.1, 1 + Spacing - 0.05);

        var gaveUp = honk.Examine(At(Analysis, 1 + Spacing));

        Assert.False(gaveUp.Acts);
        Assert.Equal(HonkOnArrival.NotInFront, gaveUp.Say);

        NothingMoreUntil(honk, Analysis, 1 + Spacing + 0.1, 60);
    }

    [Fact]
    public void ARetryHeldThenBehindSaysTheNewSentenceRatherThanDidNotTake()
    {
        _inFront = true;
        var honk = Jump();
        Assert.True(IsHoldOfFire(honk.Examine(At(Analysis, 1))));
        Assert.True(IsHoldOfFire(honk.Examine(At(Analysis, 1 + Spacing))));

        // Elite went behind during the retry, so its hold was let go.
        _inFront = false;
        NothingMoreUntil(honk, Analysis, 1 + Spacing + 0.1, 1 + (2 * Spacing) - 0.05);

        Assert.Equal(HonkOnArrival.NotInFront, honk.Examine(At(Analysis, 1 + (2 * Spacing))).Say);

        NothingMoreUntil(honk, Analysis, 1 + (2 * Spacing) + 0.1, 60);
    }

    [Fact]
    public void CombatModeInSupercruiseBehindTapsNothing()
    {
        var honk = Jump();

        Silent(honk.Examine(At(Combat, 1)));
        NothingMoreUntil(honk, Combat, 1.1, 1 + Spacing - 0.05);

        var gaveUp = honk.Examine(At(Combat, 1 + Spacing));

        Assert.False(gaveUp.Acts);
        Assert.Equal(HonkOnArrival.NotInFront, gaveUp.Say);

        NothingMoreUntil(honk, Combat, 1 + Spacing + 0.1, 60);
    }

    [Fact]
    public void TheSecondTryInCombatModeSwitchesAsAFirstTryWould()
    {
        var honk = Jump();
        Silent(honk.Examine(At(Combat, 1)));

        _inFront = true;

        Assert.True(IsTapOfHudMode(honk.Examine(At(Combat, 1 + Spacing))));
        Assert.True(IsHoldOfFire(honk.Examine(At(Analysis, 1 + Spacing + 0.2))));
    }

    [Fact]
    public void ANewJumpDropsTheSkippedTry()
    {
        var honk = Jump();
        Silent(honk.Examine(At(Analysis, 1)));

        // The skip for the system left behind does not make the new arrival's first try its second.
        honk.Examine(At(Witchspace, 4, "FSDJump"));
        Silent(honk.Examine(At(Analysis, 5)));
        NothingMoreUntil(honk, Analysis, 5.1, 5 + Spacing - 0.05);

        Assert.Equal(HonkOnArrival.NotInFront, honk.Examine(At(Analysis, 5 + Spacing)).Say);
    }

    [Fact]
    public void AnArmThatExpiresBeforeTheSecondTrySaysNothing()
    {
        var honk = Jump();

        // Dropped late, and the second try would fall outside the window.
        Silent(honk.Examine(At(Analysis, 25)));
        NothingMoreUntil(honk, Analysis, 25.1, 60);
    }
}
