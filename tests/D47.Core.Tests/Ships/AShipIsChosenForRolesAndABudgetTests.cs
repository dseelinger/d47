using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>Which hull best suits roles and a budget: ranked by code, with no model choosing a module (#2199).</summary>
public class AShipIsChosenForRolesAndABudgetTests(ITestOutputHelper output)
{
    private static readonly string[] Pool = ["cutter", "python", "krait_mkii", "type9", "diamondbackxl"];

    private static readonly BuildGoal Roles = new(
        new Dictionary<BuildRole, double> { [BuildRole.Exploration] = 1, [BuildRole.Cargo] = 1 },
        CombatProfile.Bounty,
        [],
        [],
        null,
        false);

    private static IEnumerable<ShipSpecification> Hulls(params string[] symbols) =>
        symbols.Select(symbol => EliteSpecifications.Ship(symbol)!);

    private static RankState Ranks(int empire)
    {
        Assert.True(JournalEvent.TryParse(
            $$"""{ "timestamp":"2026-10-01T11:00:01Z", "event":"Rank", "Combat":1, "Trade":1, "Explore":1, "Soldier":0, "Exobiologist":0, "Empire":{{empire}}, "Federation":0, "CQC":0 }""",
            NullLogger.Instance,
            out var parsed));

        return RankState.Empty.Apply(parsed!);
    }

    private static BestShipResult Choose(RankState ranks, long? credits, params string[] symbols) =>
        BestShip.Choose(Hulls(symbols), Roles, ranks, new HashSet<string>(), credits, CancellationToken.None);

    private static IEnumerable<string> Symbols(BestShipResult result) => result.Kept.Select(choice => choice.Hull.Symbol);

    private static ShipPlanAdvisor Advisor(ScriptedReplies model) =>
        new(() => model, () => "high-model", () => null, () => null, () => null, null, null, NullLogger.Instance);

    [Fact]
    public void ARankGatedHullIsLeftOutUntilTheRankIsHeld()
    {
        var low = Choose(Ranks(1), 5_000_000_000, Pool);
        var high = Choose(Ranks(12), 5_000_000_000, Pool);

        Assert.DoesNotContain("cutter", Symbols(low));
        Assert.Contains("cutter", Symbols(high));
    }

    [Fact]
    public void AnUnaffordableHullIsLeftOut()
    {
        var cutter = EliteSpecifications.Ship("cutter")!.Cost!.Value;
        var result = Choose(Ranks(14), cutter - 1, Pool);

        Assert.DoesNotContain("cutter", Symbols(result));
        Assert.NotEmpty(result.Kept);
        Assert.All(result.Kept, choice => Assert.True(choice.Cost <= cutter - 1));
    }

    [Fact]
    public void AHullWhoseModulesAndReserveDoNotFitIsLeftOut()
    {
        var python = EliteSpecifications.Ship("python")!.Cost!.Value;

        Assert.DoesNotContain("python", Symbols(Choose(Ranks(0), python, Pool)));
    }

    [Fact]
    public void AtMostThreeHullsAreNamedAndTheSameInputNamesTheSameThree()
    {
        var first = Choose(Ranks(14), 5_000_000_000, Pool);
        var second = Choose(Ranks(14), 5_000_000_000, Pool);

        Assert.True(first.Kept.Count > BestShipResult.MostNamed);
        Assert.Equal(BestShipResult.MostNamed, first.Named.Count);
        Assert.Equal(first.Named.Select(choice => choice.Hull.Symbol), second.Named.Select(choice => choice.Hull.Symbol));
        Assert.Equal(first.Named.Select(choice => choice.Score), second.Named.Select(choice => choice.Score));
    }

    [Fact]
    public void AHullAlreadyOwnedCostsNothingForTheHull()
    {
        var bought = Choose(Ranks(0), 5_000_000_000, "python").Kept.Single();
        var owned = BestShip.Choose(Hulls("python"), Roles, Ranks(0), new HashSet<string> { "python" }, 5_000_000_000, CancellationToken.None).Kept.Single();

        Assert.Equal(EliteSpecifications.Ship("python")!.Cost!.Value, bought.Cost - owned.Cost);
    }

    [Fact]
    public void EveryCoreInternalSlotIsFilledFromAnEmptyHull()
    {
        foreach (var choice in Choose(Ranks(14), null, "python", "type9").Kept)
        {
            foreach (var slot in EliteSpecifications.Slots(choice.Hull.Symbol).Where(slot => slot.Kind == ShipSlotKind.Core))
            {
                Assert.True(choice.Result.Plan.For(slot.Name) is { IsEmpty: false }, $"{choice.Hull.Symbol} {slot.Name} is empty");
            }
        }
    }

    [Fact]
    public void AGoalOnAShipWhoseLoadoutIsUnseenFillsNoCoreInternal()
    {
        var owned = new ShipBuild("F1", "owned", "python", ShipId: 5);
        var goal = Roles with { Mode = GoalMode.Only, Target = BuildRole.Exploration };
        var result = BuildSearch.Find(owned, null, goal, CancellationToken.None);

        Assert.DoesNotContain(result.Changes, change => change.Reason == "Fills an empty core internal slot.");
    }

    [Fact]
    public void ASearchOfEveryHullFinishesWithinTheLimit()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var result = BestShip.Choose(EliteSpecifications.Ships, Roles, Ranks(14), new HashSet<string>(), null, CancellationToken.None);

        output.WriteLine($"{result.Kept.Count} hulls in {clock.Elapsed.TotalSeconds:0.0} s");
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task ABestShipRemarkIsOneRequestAndHoldsNoGoal()
    {
        var model = new ScriptedReplies("""
            {"kind":"best_ship","goal":{"weights":{"exploration":1,"cargo":1},"profile":"bounty","floors":[],"mustHaves":[],"credits":5000000000,"engineering":false}}
            """);

        var advice = await Advisor(model).AdviseAsync(
            new ShipBuild("F1", "build-1", "python"), [], "which ship is best for exploring and hauling", TestContext.Current.CancellationToken);

        Assert.Equal(BuildRemarkKind.BestShip, advice.Kind);
        Assert.True(advice.Succeeded);
        Assert.Empty(advice.Changes);
        Assert.Null(advice.Goal);
        Assert.Single(model.Requests);
        Assert.Contains("credits", advice.Reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABestShipRemarkNamingNoRoleIsAskedAbout()
    {
        var model = new ScriptedReplies("""{"kind":"best_ship","ask":"Which roles?"}""");
        var advice = await Advisor(model).AdviseAsync(
            new ShipBuild("F1", "b", "python"), [], "best ship", TestContext.Current.CancellationToken);

        Assert.Equal("Which roles?", advice.Reply);
        Assert.Empty(advice.Changes);
    }

    [Fact]
    public void ACancelledSearchStops() =>
        Assert.ThrowsAny<OperationCanceledException>(() =>
            BestShip.Choose(Hulls(Pool), Roles, Ranks(14), new HashSet<string>(), null, new CancellationToken(canceled: true)));
}
