using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The construction tools add the planner's plan to the journal's figures when one is held.</summary>
public class ConstructionAnswersReadThePlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakePlanner(Func<string, SystemPlan?> answer) : IColonisationPlanService
    {
        public Task<SystemPlan?> SystemAsync(string systemName, CancellationToken cancellationToken) =>
            Task.FromResult(answer(systemName));
    }

    private static JournalEvent Event(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed)
            ? parsed!
            : throw new InvalidOperationException("Fixture JSON did not parse.");

    private static GameStateStore Store(int steelLeft)
    {
        var gameState = new GameStateStore();
        gameState.Apply(Event("""{"timestamp":"2026-09-01T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        var depot = $$"""{"timestamp":"{{Now.AddDays(-1):yyyy-MM-ddTHH:mm:ssZ}}","event":"ColonisationConstructionDepot","MarketID":1,"ConstructionProgress":0.2,"ConstructionComplete":false,"ConstructionFailed":false,"ResourcesRequired":[{"Name":"$steel_name;","Name_Localised":"Steel","RequiredAmount":{{steelLeft}},"ProvidedAmount":0,"Payment":100}]}""";

        gameState.Active!.Colonisation = ColonisationSites.Empty.Apply(Event(depot), "Sys A", "Site One");

        return gameState;
    }

    private static PlannedSite Planned(string name, PlanStatus status, long? marketId = null) =>
        new("id-" + name, name, status)
        {
            BuildType = "coriolis",
            BodyName = "Sys A 1",
            MarketId = marketId,
            Needed = status == PlanStatus.Build ? new Dictionary<string, int> { ["steel"] = 120 } : new Dictionary<string, int>(),
            OnLinkedCarriers = new Dictionary<string, int> { ["steel"] = 30 },
            LinkedCarriers = 2,
            Commanders = 3,
        };

    private static ColonisationPlans Plans(GameStateStore gameState, params PlannedSite[] sites) =>
        new(
            new FakePlanner(system => new SystemPlan(system, sites, Now)),
            () => true,
            () => gameState.Active,
            NullLogger<ColonisationPlans>.Instance);

    private static Task<ToolResult> Ask(GameStateStore gameState, ColonisationPlans? plans, string tool, string json = "{}") =>
        CapabilityRegistry
            .Build([ColonisationCapability.Create(() => gameState.Active, now: () => Now, plans: () => plans)])
            .InvokeAsync(tool, ToolArguments.FromJson(json), TestContext.Current.CancellationToken);

    [Fact]
    public async Task APlannedSiteWithNoDepotIsListedAsPlannedWithItsBuildTypeAndBody()
    {
        var gameState = Store(1000);
        var plans = Plans(gameState, Planned("Hub", PlanStatus.Plan));

        var result = await Ask(gameState, plans, "get_construction_sites");

        Assert.Contains("Hub — Coriolis at Sys A 1, planned.", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABuildingSiteWithADepotIsOneRow()
    {
        var gameState = Store(1000);
        var plans = Plans(gameState, Planned("Hub", PlanStatus.Build, marketId: 1));

        var result = await Ask(gameState, plans, "get_construction_sites");

        Assert.Equal(1, Count(result.Content, "Site One"));
        Assert.Contains("Planner: Coriolis at Sys A 1, building.", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("no depot of yours seen", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NeedsGiveBothFiguresWhereTheyDifferAndOneWhereTheyAgree()
    {
        var differ = Store(1000);
        var apart = await Ask(differ, Plans(differ, Planned("Hub", PlanStatus.Build, marketId: 1)), "get_construction_needs");

        Assert.Contains("120 tonnes left by the planner; your last docking said 1,000", apart.Content, StringComparison.Ordinal);
        Assert.Contains("30 tonnes on linked carriers", apart.Content, StringComparison.Ordinal);
        Assert.Contains("3 Commanders and 2 linked carriers", apart.Content, StringComparison.Ordinal);

        var agree = Store(120);
        var together = await Ask(agree, Plans(agree, Planned("Hub", PlanStatus.Build, marketId: 1)), "get_construction_needs");

        Assert.Contains("120 tonnes left,", together.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("your last docking said", together.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASiteNamedByItsBuildTypeIsAnsweredFromThePlannerAlone()
    {
        var gameState = Store(1000);
        var plans = Plans(gameState, Planned("Hub", PlanStatus.Build));

        var result = await Ask(gameState, plans, "get_construction_needs", """{"site":"coriolis"}""");

        Assert.Contains("planner's alone", result.Content, StringComparison.Ordinal);
        Assert.Contains("steel — 120 tonnes left, 30 tonnes on linked carriers.", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APlannerNameOrBuildTypeIsClippedToSixtyCharacters()
    {
        var gameState = Store(1000);
        var longName = new string('x', 100);
        var plans = Plans(gameState, Planned(longName, PlanStatus.Plan) with { BuildType = new string('y', 100) });

        var result = await Ask(gameState, plans, "get_construction_sites");

        Assert.Contains(new string('x', 60), result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 61), result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('y', 61), result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WithNoPlanHeldBothAnswersAreTodaysAndSayThePlanCouldNotBeRead()
    {
        var gameState = Store(1000);
        var silent = new ColonisationPlans(
            new FakePlanner(_ => null),
            () => true,
            () => gameState.Active,
            NullLogger<ColonisationPlans>.Instance);

        foreach (var tool in new[] { "get_construction_sites", "get_construction_needs" })
        {
            var today = await Ask(gameState, null, tool);
            var without = await Ask(gameState, silent, tool);

            Assert.Equal(
                today.Content + Environment.NewLine
                + "The plan could not be read from the colonisation planner for Sys A, so that part is from your journal alone.",
                without.Content);
        }
    }

    private static int Count(string text, string part)
    {
        var count = 0;

        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
