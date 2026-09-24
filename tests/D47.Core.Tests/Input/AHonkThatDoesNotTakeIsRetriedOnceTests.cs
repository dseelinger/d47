using System.Text.Json;
using D47.Core.Actions;
using D47.Core.Callouts;
using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>A hold with no <c>FSSDiscoveryScan</c> after it is repeated once, then reported (#451).</summary>
public class AHonkThatDoesNotTakeIsRetriedOnceTests
{
    private static readonly EliteBinding Fire = new("PrimaryFire", "Primary", "Mouse", "Mouse_1");

    private static readonly EliteBinding HudMode = new("PlayerHUDModeToggle", "Primary", "Keyboard", "Key_M");

    private static readonly EliteBinds Bound = new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings = [Fire, HudMode],
    };

    /// <summary>The Mumbal jump in <c>Journal.2026-09-24T090721.01.log</c>.</summary>
    private static readonly DateTimeOffset Jumped = new(2026, 9, 24, 13, 22, 3, TimeSpan.Zero);

    private static readonly StatusFlags Witchspace = StatusFlags.InMainShip | StatusFlags.FsdJump;

    private static readonly StatusFlags Analysis =
        StatusFlags.InMainShip | StatusFlags.Supercruise | StatusFlags.AnalysisMode;

    private static readonly StatusFlags Combat = StatusFlags.InMainShip | StatusFlags.Supercruise;

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

    /// <summary>Jumps, then holds a second later in analysis mode, as every honk in the sample did.</summary>
    private static HonkOnArrival HeldAtOneSecond()
    {
        var honk = new HonkOnArrival(() => true, () => Bound);
        honk.Examine(At(Witchspace, 0, "FSDJump"));
        Assert.True(IsHoldOfFire(honk.Examine(At(Analysis, 1))));
        return honk;
    }

    /// <summary>When the wait for the scan after a hold at <paramref name="held"/> runs out.</summary>
    private static double Deadline(double held) =>
        held + (HonkOnArrival.Charge + HonkOnArrival.Confirmation).TotalSeconds;

    [Fact]
    public void MumbalSentWithNoScanIsHeldAgain()
    {
        var honk = HeldAtOneSecond();

        Assert.False(honk.Examine(At(Analysis, Deadline(1) - 0.1)).Acts);

        var retry = honk.Examine(At(Analysis, Deadline(1)));

        Assert.True(IsHoldOfFire(retry));
        Assert.Null(retry.Say);
    }

    [Fact]
    public void CdSixtyOneSixEightHundredOneScannedAndNothingMoreHappens()
    {
        var honk = HeldAtOneSecond();

        // Sent at 13:27:02, scanned at 13:27:03.
        Assert.False(honk.Examine(At(Analysis, 6.5, "FSSDiscoveryScan")).Acts);

        for (var second = 7.0; second < 30; second += 0.5)
        {
            var decision = honk.Examine(At(Analysis, second));
            Assert.False(decision.Acts);
            Assert.Null(decision.Say);
        }
    }

    [Fact]
    public void ASecondHoldWithNoScanIsSaidAndNothingMoreIsTried()
    {
        var honk = HeldAtOneSecond();
        var retried = Deadline(1);
        Assert.True(IsHoldOfFire(honk.Examine(At(Analysis, retried))));

        Assert.False(honk.Examine(At(Analysis, Deadline(retried) - 0.1)).Acts);

        var gaveUp = honk.Examine(At(Analysis, Deadline(retried)));

        Assert.False(gaveUp.Acts);
        Assert.Equal(HonkOnArrival.DidNotTake, gaveUp.Say);

        for (var second = Deadline(retried) + 0.5; second < 60; second += 0.5)
        {
            var decision = honk.Examine(At(Analysis, second));
            Assert.False(decision.Acts);
            Assert.Null(decision.Say);
        }
    }

    [Fact]
    public void AScanFromTheCommanderHonkingByHandEndsTheWait()
    {
        var honk = HeldAtOneSecond();

        // Arrives after the wait would have ended on the first hold alone, but before the tick that ends it.
        Assert.False(honk.Examine(At(Analysis, Deadline(1) - 0.05, "FSSDiscoveryScan")).Acts);

        for (var second = Deadline(1); second < 40; second += 0.5)
        {
            var decision = honk.Examine(At(Analysis, second));
            Assert.False(decision.Acts);
            Assert.Null(decision.Say);
        }
    }

    [Fact]
    public void TheRetryReadsAnalysisModeAgainOnItsOwnTick()
    {
        var honk = HeldAtOneSecond();

        // The Commander has since left analysis mode; the first hold's reading is not reused.
        var decision = honk.Examine(At(Combat, Deadline(1)));

        Assert.False(decision.Acts);
        Assert.Equal(HonkOnArrival.DidNotTake, decision.Say);
    }

    [Fact]
    public void ANewJumpWhileWaitingStartsOverForTheNewSystem()
    {
        var honk = HeldAtOneSecond();

        Assert.False(honk.Examine(At(Witchspace, 4, "FSDJump")).Acts);

        // Nothing is retried or said for the system left behind.
        var arrived = honk.Examine(At(Analysis, Deadline(1)));

        Assert.True(IsHoldOfFire(arrived));
        Assert.Null(arrived.Say);

        Assert.False(honk.Examine(At(Analysis, Deadline(1) + 5, "FSSDiscoveryScan")).Acts);
        Assert.False(honk.Examine(At(Analysis, Deadline(Deadline(1)) + 1)).Acts);
    }

    [Fact]
    public void ASwitchBackOwedToAnAbandonedArrivalIsNotPaidOnALaterOne()
    {
        var honk = new HonkOnArrival(() => true, () => Bound);
        honk.Examine(At(Witchspace, 0, "FSDJump"));
        honk.Examine(At(Combat, 1));
        honk.Examine(At(Analysis, 1.2));

        // Jumped mid-wait, and the arm for that arrival expires unspent.
        honk.Examine(At(Witchspace, 4, "FSDJump"));
        honk.Examine(At(Witchspace, 4 + HonkOnArrival.Window.TotalSeconds + 1));

        // Later the Commander arrives in analysis mode by choice.
        honk.Examine(At(Witchspace, 100, "FSDJump"));
        Assert.True(IsHoldOfFire(honk.Examine(At(Analysis, 101))));

        Assert.False(honk.Examine(At(Analysis, 106, "FSSDiscoveryScan")).Acts);
        Assert.False(honk.Examine(At(Analysis, 110)).Acts);
    }

    [Fact]
    public void AfterASwitchItStaysInAnalysisModeForTheRetryAndSwitchesBackAtTheEnd()
    {
        var honk = new HonkOnArrival(() => true, () => Bound);
        honk.Examine(At(Witchspace, 0, "FSDJump"));

        Assert.True(IsTapOfHudMode(honk.Examine(At(Combat, 1))));
        Assert.True(IsHoldOfFire(honk.Examine(At(Analysis, 1.2))));

        // No switch back while the scan may still come.
        Assert.False(honk.Examine(At(Analysis, 1.2 + (HonkOnArrival.Charge + HonkOnArrival.Settle).TotalSeconds)).Acts);

        var retried = Deadline(1.2);
        Assert.True(IsHoldOfFire(honk.Examine(At(Analysis, retried))));

        var gaveUp = honk.Examine(At(Analysis, Deadline(retried)));

        Assert.True(IsTapOfHudMode(gaveUp));
        Assert.Equal(HonkOnArrival.DidNotTake, gaveUp.Say);
    }
}
