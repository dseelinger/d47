using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>Builds found for a stated goal on a stock Krait Mk II, with no model and no game.</summary>
public class ASearchedBuildMeetsItsGoalTests
{
    private static readonly ShipBuild Start = new("F1", "build-1", "krait_mkii", 7, "Stock");

    /// <summary>E-rated core modules, an E-rated shield, one 16 t rack and two medium pulse lasers.</summary>
    private static readonly ShipLoadout Stock = new()
    {
        Type = "krait_mkii",
        ShipId = 7,
        Modules =
        [
            Fitted("Armour", "krait_mkii_armour_grade1"),
            Fitted("PowerPlant", "int_powerplant_size7_class1"),
            Fitted("MainEngines", "int_engine_size6_class1"),
            Fitted("FrameShiftDrive", "int_hyperdrive_size5_class1"),
            Fitted("LifeSupport", "int_lifesupport_size4_class1"),
            Fitted("PowerDistributor", "int_powerdistributor_size7_class1"),
            Fitted("Radar", "int_sensors_size6_class1"),
            Fitted("FuelTank", "int_fueltank_size5_class3"),
            Fitted("Slot03_Size5", "int_shieldgenerator_size5_class1"),
            Fitted("Slot05_Size4", "int_cargorack_size4_class1"),
            Fitted("MediumHardpoint1", "hpt_pulselaser_fixed_medium"),
            Fitted("MediumHardpoint2", "hpt_pulselaser_fixed_medium"),
        ],
    };

    private static ShipModule Fitted(string slot, string item) => new(slot, item, Powered: true, Health: 100, Value: null);

    private static BuildGoal Goal(
        GoalMode mode,
        BuildRole? target,
        IReadOnlyList<BuildFloor>? floors = null,
        IReadOnlyList<string>? mustHaves = null,
        long? credits = null,
        IReadOnlyDictionary<BuildRole, double>? weights = null) =>
        new(
            weights ?? new Dictionary<BuildRole, double>(),
            CombatProfile.Bounty,
            floors ?? [],
            mustHaves ?? [],
            credits,
            Engineering: false,
            mode,
            target);

    private static BuildResult Find(BuildGoal goal) => BuildSearch.Find(Start, Stock, goal, CancellationToken.None);

    private static double Figure(BuildGauges gauges, BuildFigure figure) => figure switch
    {
        BuildFigure.LadenJump => gauges.Jump!.Worst,
        BuildFigure.UnladenJump => gauges.Jump!.Best,
        BuildFigure.Cargo => gauges.Combat!.Cargo,
        BuildFigure.Shields => gauges.Combat!.Shields ?? 0,
        BuildFigure.Hull => gauges.Combat!.Hull ?? 0,
        _ => gauges.Combat!.SustainedDps,
    };

    [Fact]
    public void AnExplorationOnlyGoalGainsLadenRange()
    {
        var result = Find(Goal(GoalMode.Only, BuildRole.Exploration));

        Assert.True(result.Gauges.Jump!.Worst > result.StartGauges.Jump!.Worst + 1);
        Assert.NotEmpty(result.Changes);
    }

    [Theory]
    [InlineData(BuildRole.Exploration, BuildFigure.LadenJump)]
    [InlineData(BuildRole.Cargo, BuildFigure.Cargo)]
    [InlineData(BuildRole.Combat, BuildFigure.Shields)]
    public void KeepingTheRestNeverLowersAnotherFigure(BuildRole target, BuildFigure own)
    {
        var result = Find(Goal(GoalMode.KeepRest, target));

        var others = target == BuildRole.Combat
            ? new[] { BuildFigure.LadenJump, BuildFigure.UnladenJump, BuildFigure.Cargo }
            : Enum.GetValues<BuildFigure>().Where(figure => figure != own).ToArray();

        foreach (var figure in others)
        {
            Assert.True(
                Figure(result.Gauges, figure) >= Figure(result.StartGauges, figure) - 1e-6,
                $"{figure} fell from {Figure(result.StartGauges, figure)} to {Figure(result.Gauges, figure)}");
        }
    }

    [Fact]
    public void KeepingTheRestStillRaisesTheTarget()
    {
        var result = Find(Goal(GoalMode.KeepRest, BuildRole.Exploration));

        Assert.True(result.Gauges.Jump!.Worst > result.StartGauges.Jump!.Worst + 1);
    }

    [Fact]
    public void AnyGainChangesTheFewestSlots()
    {
        var result = Find(Goal(GoalMode.AnyGain, BuildRole.Cargo));

        // An empty optional slot takes a rack, so one change is enough.
        Assert.Single(result.Changes);
        Assert.True(result.Gauges.Combat!.Cargo > result.StartGauges.Combat!.Cargo);
    }

    [Fact]
    public void AnyGainTakesTheCheapestOfTheSingleChanges()
    {
        var result = Find(Goal(GoalMode.AnyGain, BuildRole.Cargo));
        var change = Assert.Single(result.Changes);

        // A 2 t rack is the cheapest gain there is, and of the slots that take it the first in the layout wins.
        Assert.Equal("Slot01_Size6", change.Slot);
        Assert.Equal("int_cargorack_size1_class1", change.After.Variant);
    }

    [Fact]
    public void AFloorThatCannotBeMetIsNamed()
    {
        var floor = new BuildFloor(BuildFigure.Cargo, 100_000);

        var result = Find(Goal(GoalMode.Only, BuildRole.Exploration, floors: [floor]));

        Assert.Equal(floor, result.Unmet);
    }

    [Fact]
    public void AFloorThatCanBeMetIsMet()
    {
        var floor = new BuildFloor(BuildFigure.Cargo, 64);

        var result = Find(Goal(GoalMode.Only, BuildRole.Exploration, floors: [floor]));

        Assert.Null(result.Unmet);
        Assert.True(result.Gauges.Combat!.Cargo >= 64);
    }

    [Fact]
    public void AMustHaveIsKept()
    {
        var result = Find(Goal(GoalMode.Only, BuildRole.Cargo, mustHaves: ["Fuel Scoop"]));

        Assert.Empty(result.Missing);
        Assert.Contains(
            result.Plan.Slots,
            plan => plan.Variant is { } variant && EliteSpecifications.Module(variant)?.Name == "Fuel Scoop");
    }

    [Fact]
    public void AnUnscoredModuleIsKeptWhenTheRestIsKept()
    {
        var withScoop = Stock with { Modules = [.. Stock.Modules, Fitted("Slot09_Size1", "int_fuelscoop_size1_class1")] };

        var result = BuildSearch.Find(Start, withScoop, Goal(GoalMode.KeepRest, BuildRole.Cargo), CancellationToken.None);

        Assert.DoesNotContain(result.Changes, change => change.Slot == "Slot09_Size1");
        Assert.Empty(result.Missing);
    }

    [Fact]
    public void ThePowerBudgetFitsInEveryResult()
    {
        var goals = new[]
        {
            Goal(GoalMode.Only, BuildRole.Exploration),
            Goal(GoalMode.Only, BuildRole.Combat),
            Goal(GoalMode.KeepRest, BuildRole.Cargo),
            Goal(GoalMode.AnyGain, BuildRole.Combat),
            Goal(GoalMode.Weighted, null, weights: new Dictionary<BuildRole, double>
            {
                [BuildRole.Combat] = 0.5,
                [BuildRole.Exploration] = 0.375,
                [BuildRole.Cargo] = 0.125,
            }),
        };

        foreach (var goal in goals)
        {
            Assert.True(Find(goal).Gauges.Power!.Fits, $"{goal.Mode} {goal.Target} does not fit its power budget");
        }
    }

    [Fact]
    public void WhatIsSpentStaysInsideTheCreditsWithTheRebuyHeldBack()
    {
        const long credits = 3_000_000;

        var result = Find(Goal(GoalMode.Only, BuildRole.Cargo, credits: credits));

        var value = 44_160_710 + result.Plan.Slots.Sum(plan => EliteSpecifications.Module(plan.Variant)?.Cost ?? 0)
                    + Stock.Modules
                        .Where(module => result.Plan.For(module.Slot) is null)
                        .Sum(module => EliteSpecifications.Module(module.Item)?.Cost ?? 0);

        Assert.True(result.Credits > 0);
        Assert.True(result.Credits + BuildSearch.RebuyShare * value <= credits);
    }

    [Fact]
    public void TheSameInputGivesTheSamePlanTwice()
    {
        var goal = Goal(GoalMode.Weighted, null, weights: new Dictionary<BuildRole, double>
        {
            [BuildRole.Combat] = 1,
            [BuildRole.Exploration] = 1,
        });

        var first = Find(goal);
        var second = Find(goal);

        Assert.Equal(first.Plan.Slots, second.Plan.Slots);
        Assert.Equal(first.Changes, second.Changes);
    }

    [Fact]
    public void EngineeringIsOfferedOnlyWhenAskedFor()
    {
        var plain = Find(Goal(GoalMode.Only, BuildRole.Exploration));
        var engineered = Find(Goal(GoalMode.Only, BuildRole.Exploration) with { Engineering = true });

        Assert.DoesNotContain(plain.Plan.Slots, plan => plan.Blueprint is not null);
        Assert.NotNull(engineered.Plan.For("FrameShiftDrive")?.Blueprint);
        Assert.True(engineered.Gauges.Jump!.Worst > plain.Gauges.Jump!.Worst);
    }

    [Fact]
    public void ACancelledSearchStops()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => BuildSearch.Find(Start, Stock, Goal(GoalMode.Only, BuildRole.Exploration), cancelled.Token));
    }

    [Fact]
    public void AModeThatRaisesOneRoleNeedsItNamed()
    {
        Assert.Throws<ArgumentException>(() => Find(Goal(GoalMode.Only, target: null)));
    }
}
