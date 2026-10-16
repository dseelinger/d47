using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>A position that names a split of the power distributor.</summary>
public class ASwitchPositionCanSetThePipsTests
{
    private const string Stick = "{wgi/nrid/throttle}";

    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    private static readonly PowerDistribution Balanced = new(4, 4, 4);

    private static readonly SwitchMapping Pips = new()
    {
        Name = "PIPS",
        DeviceId = Stick,
        Positions =
        [
            new SwitchPosition(1, Target: "pips_engines"),
            new SwitchPosition(2, Target: "pips_balanced"),
            new SwitchPosition(3, Target: "pips_weapons"),
            new SwitchPosition(4, Target: "pips_systems_engines"),
        ],
    };

    private static readonly EliteBinds Binds = new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings =
        [
            new EliteBinding("ResetPowerDistribution", "Primary", "Keyboard", "Key_F1"),
            new EliteBinding("IncreaseSystemsPower", "Primary", "Keyboard", "Key_F2"),
            new EliteBinding("IncreaseEnginesPower", "Primary", "Keyboard", "Key_F3"),
            new EliteBinding("IncreaseWeaponsPower", "Primary", "Keyboard", "Key_F4"),
        ],
    };

    private static SwitchReconciler New() => new(NullLogger<SwitchReconciler>.Instance);

    private static SwitchTick Tick(
        int held,
        PowerDistribution? pips,
        double seconds = 0,
        StatusFlags flags = StatusFlags.InMainShip)
    {
        var buttons = new bool[8];
        buttons[held] = true;
        var now = Start.AddSeconds(seconds);

        return new SwitchTick
        {
            Now = now,
            Readings = [new HotasReading { Id = Stick, Buttons = buttons }],
            Status = new GameStatus { Flags = flags, Pips = pips, ReadAt = now },
            Binds = Binds,
            Enabled = true,
        };
    }

    /// <summary>The steps for tapping each action in turn, with the gap between taps.</summary>
    private static List<InputStep> Taps(params string[] actions)
    {
        var steps = new List<InputStep>();

        foreach (var id in actions)
        {
            var reach = ActionReachability.Resolve(GameActions.Find(id)!, Binds, ControlContext.AnyShip);
            Assert.True(reach.IsOffered);

            if (steps.Count > 0)
            {
                steps.Add(InputStep.Wait(TimeSpan.FromMilliseconds(100)));
            }

            steps.AddRange(InputSequence.Tap(reach.Binding!));
        }

        return steps;
    }

    private static IReadOnlyList<PendingReconcile> Flip(int from, int to, PowerDistribution? pips)
    {
        var reconciler = New();
        reconciler.Poll(Tick(from, pips), [Pips]);
        reconciler.Poll(Tick(to, pips, 0.1), [Pips]);

        return reconciler.Drain();
    }

    [Fact]
    public void StatusJsonPipsAreReadInHalfPips()
    {
        var files = new MemoryFileSystem();
        files.WriteText(
            Path.Combine(@"C:\d47-test", GameStatusReader.FileName),
            """{ "timestamp":"2026-10-15T01:45:01Z", "event":"Status", "Flags":16842765, "Pips":[2,8,2], "GuiFocus":0 }""");

        var reader = new GameStatusReader(@"C:\d47-test", files, NullLogger.Instance);
        Assert.True(reader.Poll());

        Assert.Equal(new PowerDistribution(2, 8, 2), reader.Current.Pips);
    }

    [Theory]
    [InlineData("""{ "event":"Status", "Flags":0 }""")]
    [InlineData("""{ "event":"Status", "Flags":0, "Pips":[4,4] }""")]
    [InlineData("""{ "event":"Status", "Flags":0, "Pips":"4,4,4" }""")]
    public void StatusJsonWithoutUsablePipsReadsNull(string json)
    {
        var files = new MemoryFileSystem();
        files.WriteText(Path.Combine(@"C:\d47-test", GameStatusReader.FileName), json);

        var reader = new GameStatusReader(@"C:\d47-test", files, NullLogger.Instance);
        Assert.True(reader.Poll());

        Assert.Null(reader.Current.Pips);
    }

    [Fact]
    public void ALiveStatusJsonReadsItsPips()
    {
        var files = new MemoryFileSystem();
        files.WriteText(
            Path.Combine(@"C:\d47-test", GameStatusReader.FileName),
            EmbeddedFixture.Text("status.Status.2026-09-30T140606.json"));

        var reader = new GameStatusReader(@"C:\d47-test", files, NullLogger.Instance);
        Assert.True(reader.Poll());

        Assert.Equal(Balanced, reader.Current.Pips);
    }

    /// <summary>
    /// Each target's presses, applied from balanced to a model of the distributor, land on its split. A press adds
    /// two half-pips and takes one from each other system, or both from the one that is not empty.
    /// </summary>
    [Fact]
    public void EverySequenceReachesItsSplitFromBalanced()
    {
        foreach (var target in PipTargets.All)
        {
            var pips = new[] { 4, 4, 4 };

            foreach (var id in target.Presses)
            {
                var to = id switch
                {
                    "power_to_systems" => 0,
                    "power_to_engines" => 1,
                    "power_to_weapons" => 2,
                    _ => throw new InvalidOperationException($"{target.Id} presses {id}"),
                };

                int[] others = [.. Enumerable.Range(0, 3).Where(index => index != to)];
                Assert.True(pips[to] <= 6, $"{target.Id} presses {id} with that system at {pips[to]}");

                pips[to] += 2;

                if (pips[others[0]] > 0 && pips[others[1]] > 0)
                {
                    pips[others[0]]--;
                    pips[others[1]]--;
                }
                else
                {
                    var from = pips[others[0]] > 0 ? others[0] : others[1];
                    Assert.True(pips[from] >= 2, $"{target.Id} presses {id} with too little to take");
                    pips[from] -= 2;
                }
            }

            Assert.Equal(target.Pips, new PowerDistribution(pips[0], pips[1], pips[2]));
        }
    }

    [Fact]
    public void EveryTargetSumsToTwelveHalfPips()
    {
        foreach (var target in PipTargets.All)
        {
            Assert.Equal(12, target.Pips.Systems + target.Pips.Engines + target.Pips.Weapons);
        }
    }

    [Fact]
    public void TheTenTargetsAreNamedInTheIssue()
    {
        Assert.Equal(
            [
                "pips_balanced", "pips_engines", "pips_weapons", "pips_systems", "pips_systems_engines",
                "pips_systems_weapons", "pips_engines_systems", "pips_engines_weapons", "pips_weapons_systems",
                "pips_weapons_engines",
            ],
            PipTargets.All.Select(target => target.Id));
    }

    [Fact]
    public void EveryTargetRunsOnlyKeysThatExistInShipFighterAndSrv()
    {
        foreach (var target in PipTargets.All)
        {
            Assert.Equal(ControlContext.AnyShip | ControlContext.Fighter | ControlContext.Srv, target.Contexts);

            foreach (var id in target.Presses)
            {
                Assert.NotNull(GameActions.Find(id));
            }
        }
    }

    [Fact]
    public void BalancedIsOneResetAndNothingElse()
    {
        var pending = Assert.Single(Flip(1, 2, new PowerDistribution(2, 8, 2)));

        Assert.Equal(Taps("balance_power"), pending.Steps);
    }

    [Fact]
    public void FourPipsToEnginesResetsThenPressesEnginesTwice()
    {
        var pending = Assert.Single(Flip(2, 1, Balanced));

        Assert.Equal(Taps("balance_power", "power_to_engines", "power_to_engines"), pending.Steps);
    }

    [Fact]
    public void FourPipsToWeaponsResetsThenPressesWeaponsTwice()
    {
        var pending = Assert.Single(Flip(2, 3, Balanced));

        Assert.Equal(Taps("balance_power", "power_to_weapons", "power_to_weapons"), pending.Steps);
    }

    [Fact]
    public void ATwoSystemTargetPressesItsWholeSequenceAfterTheReset()
    {
        var pending = Assert.Single(Flip(2, 4, Balanced));

        Assert.Equal(
            Taps(
                "balance_power",
                "power_to_systems",
                "power_to_systems",
                "power_to_engines",
                "power_to_engines",
                "power_to_systems"),
            pending.Steps);
    }

    [Fact]
    public void EveryTargetStartsWithAResetAndThenItsOwnSequence()
    {
        foreach (var target in PipTargets.All)
        {
            var mapping = Pips with
            {
                Positions = [new SwitchPosition(1, Target: "pips_balanced"), new SwitchPosition(2, Target: target.Id)],
            };

            var reconciler = New();
            reconciler.Poll(Tick(1, new PowerDistribution(6, 3, 3)), [mapping]);
            reconciler.Poll(Tick(2, new PowerDistribution(6, 3, 3), 0.1), [mapping]);

            var pending = Assert.Single(reconciler.Drain());

            Assert.Equal(Taps([PipTargets.Reset, .. target.Presses]), pending.Steps);
        }
    }

    [Fact]
    public void FlippingToTheSplitAlreadyReadPressesNothing()
    {
        Assert.Empty(Flip(1, 2, Balanced));
        Assert.Empty(Flip(2, 1, new PowerDistribution(2, 8, 2)));
    }

    [Fact]
    public void FlippingBackBeforeThePressesLandPressesTheWayBack()
    {
        var reconciler = New();
        reconciler.Poll(Tick(2, Balanced), [Pips]);
        reconciler.Poll(Tick(1, Balanced, 0.1), [Pips]);
        reconciler.Poll(Tick(2, Balanced, 0.2), [Pips]);

        var pending = reconciler.Drain();

        Assert.Equal(2, pending.Count);
        Assert.Equal(Taps("balance_power"), pending[1].Steps);
    }

    [Fact]
    public void AFlickOffTheSplitPressesTheWayBack()
    {
        var reconciler = New();
        reconciler.Poll(Tick(2, Balanced), [Pips]);

        var held = new bool[8];
        held[2] = true;
        var down = new bool[8];
        down[1] = true;
        down[2] = true;
        reconciler.Poll(
            Tick(2, Balanced, 0.1) with
            {
                Readings = [new HotasReading { Id = Stick, Buttons = held, DownSinceLastRead = down }],
            },
            [Pips]);

        var pending = reconciler.Drain();

        Assert.Equal(2, pending.Count);
        Assert.Equal(Taps("balance_power"), pending[1].Steps);
    }

    [Fact]
    public void WithoutPipsInStatusTheSequenceIsPressedAnyway()
    {
        Assert.Single(Flip(2, 1, null));
    }

    [Fact]
    public void ARepeatedPollOfTheSamePositionPressesNothing()
    {
        var reconciler = New();
        reconciler.Poll(Tick(2, Balanced), [Pips]);
        reconciler.Poll(Tick(1, Balanced, 0.1), [Pips]);
        reconciler.Drain();

        reconciler.Poll(Tick(1, new PowerDistribution(2, 8, 2), 0.2), [Pips]);

        Assert.Empty(reconciler.Drain());
    }

    [Fact]
    public void PipsThatLeaveTheTargetWithinTheContestWindowPauseTheSwitch()
    {
        var reconciler = New();
        reconciler.Poll(Tick(2, Balanced), [Pips]);
        reconciler.Poll(Tick(1, Balanced, 0.1), [Pips]);
        reconciler.Drain();

        reconciler.Poll(Tick(1, new PowerDistribution(2, 8, 2), 0.5), [Pips]);
        reconciler.Poll(Tick(1, Balanced, 1.5), [Pips]);

        Assert.True(reconciler.IsPaused("PIPS"));
        Assert.Contains(reconciler.Drain(), pending => pending.Say is not null);
    }

    [Fact]
    public void PipsThatLeaveAfterTheContestWindowDoNotPauseTheSwitch()
    {
        var reconciler = New();
        reconciler.Poll(Tick(2, Balanced), [Pips]);
        reconciler.Poll(Tick(1, Balanced, 0.1), [Pips]);
        reconciler.Drain();

        reconciler.Poll(Tick(1, new PowerDistribution(2, 8, 2), 0.5), [Pips]);
        reconciler.Poll(Tick(1, Balanced, 0.5 + SwitchReconciler.ContestWindow.TotalSeconds + 1), [Pips]);

        Assert.False(reconciler.IsPaused("PIPS"));
    }

    [Fact]
    public void PipsThatNeverArriveAreSaidOutLoud()
    {
        var reconciler = New();
        reconciler.Poll(Tick(2, Balanced), [Pips]);
        reconciler.Poll(Tick(1, Balanced, 0.1), [Pips]);
        reconciler.Drain();

        reconciler.Poll(Tick(1, Balanced, 0.1 + SwitchReconciler.ContestWindow.TotalSeconds + 1), [Pips]);

        var pending = Assert.Single(reconciler.Drain());
        Assert.Contains("did not take", pending.Say);
    }

    [Fact]
    public void ADisagreementNamesThePipsAsTheyStand()
    {
        var reconciler = New();
        reconciler.Poll(Tick(1, Balanced), [Pips]);
        reconciler.Poll(Tick(1, Balanced, 0.01), [Pips]);

        Assert.Equal("pips are 4, 4, 4", Assert.Single(reconciler.States).Disagrees);

        reconciler.Poll(Tick(1, new PowerDistribution(2, 8, 2), 0.02), [Pips]);

        Assert.Null(Assert.Single(reconciler.States).Disagrees);
    }

    [Fact]
    public void AMissingBindingNamesItOnTheRow()
    {
        var reconciler = New();
        var tick = Tick(2, Balanced) with
        {
            Binds = new EliteBinds { PresetName = "Test", SourceFile = "Test.binds", Bindings = [] },
        };

        reconciler.Poll(tick, [Pips]);

        Assert.NotEmpty(Assert.Single(reconciler.States).Note);
    }

    [Fact]
    public void ThePipTargetsAreValidInASwitchFile()
    {
        Assert.Null(SwitchValidation.Problem(Pips));
    }
}
