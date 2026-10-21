using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class ThePlannerNeverRaisesWhatTheDepotNeedsTests
{
    private const long MarketId = 3960010001;

    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed)
            ? parsed!
            : throw new InvalidOperationException("Fixture JSON did not parse.");

    /// <summary>A depot needing 1000 steel and 400 aluminium, with nothing provided.</summary>
    private static ConstructionSite Site() =>
        ColonisationSites.Empty
            .Apply(
                Event($$"""{"timestamp":"{{Now:yyyy-MM-ddTHH:mm:ssZ}}","event":"ColonisationConstructionDepot","MarketID":{{MarketId}},"ConstructionProgress":0.1,"ConstructionComplete":false,"ConstructionFailed":false,"ResourcesRequired":[{"Name":"$steel_name;","Name_Localised":"Steel","RequiredAmount":1000,"ProvidedAmount":0,"Payment":100},{"Name":"$aluminium_name;","Name_Localised":"Aluminium","RequiredAmount":400,"ProvidedAmount":0,"Payment":100}]}"""),
                "Hyades Sector AB-C d1",
                "Planetary Construction Site")
            .ById(MarketId)!;

    private static PlannedSite Plan(
        long? marketId = MarketId,
        PlanStatus status = PlanStatus.Build,
        Dictionary<string, int>? needed = null,
        Dictionary<string, int>? onCarriers = null) =>
        new("site-1", "Port", status)
        {
            MarketId = marketId,
            Needed = needed ?? new Dictionary<string, int> { ["steel"] = 600, ["aluminium"] = 500 },
            OnLinkedCarriers = onCarriers ?? new Dictionary<string, int>(),
        };

    private static PlannedNeed Need(IReadOnlyList<PlannedNeed> needs, string symbol) =>
        Assert.Single(needs, need => need.Symbol == symbol);

    [Fact]
    public void ThePlannersFigureIsUsedWhenItIsLower()
    {
        var steel = Need(PlannedNeeds.Merge(Site(), Plan()), "steel");

        Assert.Equal(1000, steel.Depot);
        Assert.Equal(600, steel.Planner);
        Assert.Equal(600, steel.Remaining);
    }

    [Fact]
    public void TheDepotsFigureIsUsedWhenItIsLower()
    {
        var aluminium = Need(PlannedNeeds.Merge(Site(), Plan()), "aluminium");

        Assert.Equal(400, aluminium.Depot);
        Assert.Equal(500, aluminium.Planner);
        Assert.Equal(400, aluminium.Remaining);
    }

    [Fact]
    public void APlanForAnotherMarketChangesNothing()
    {
        var needs = PlannedNeeds.Merge(Site(), Plan(marketId: MarketId + 1, onCarriers: new() { ["steel"] = 50 }));

        Assert.All(needs, need => Assert.Null(need.Planner));
        Assert.All(needs, need => Assert.Equal(0, need.OnLinkedCarriers));
        Assert.Equal(1000, Need(needs, "steel").Remaining);
    }

    [Fact]
    public void APlanWithNoMarketChangesNothing()
    {
        var needs = PlannedNeeds.Merge(Site(), Plan(marketId: null));

        Assert.All(needs, need => Assert.Null(need.Planner));
    }

    [Fact]
    public void ACommodityThePlannerDoesNotListKeepsTheDepotsFigure()
    {
        var steel = Need(PlannedNeeds.Merge(Site(), Plan(needed: new() { ["aluminium"] = 100 })), "steel");

        Assert.Null(steel.Planner);
        Assert.Equal(1000, steel.Remaining);
    }

    [Fact]
    public void ACompletePlanDoesNotMakeTheSiteComplete()
    {
        var site = Site();
        var needs = PlannedNeeds.Merge(site, Plan(status: PlanStatus.Complete, needed: new() { ["steel"] = 0, ["aluminium"] = 0 }));

        Assert.False(site.Complete);
        Assert.Equal([1000, 400], needs.Select(need => need.Remaining));
        Assert.All(needs, need => Assert.Null(need.Planner));
    }

    [Fact]
    public void LinkedCarriersCargoIsReportedAndNotSubtracted()
    {
        var steel = Need(PlannedNeeds.Merge(Site(), Plan(onCarriers: new() { ["steel"] = 250 })), "steel");

        Assert.Equal(250, steel.OnLinkedCarriers);
        Assert.Equal(600, steel.Remaining);
    }

    [Fact]
    public void WithNoPlanEveryCommodityKeepsTheDepotsFigure()
    {
        var needs = PlannedNeeds.Merge(Site(), null);

        Assert.Equal(["steel", "aluminium"], needs.Select(need => need.Symbol));
        Assert.Equal([1000, 400], needs.Select(need => need.Remaining));
    }
}
