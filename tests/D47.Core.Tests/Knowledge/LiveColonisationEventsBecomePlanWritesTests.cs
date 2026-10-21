using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class LiveColonisationEventsBecomePlanWritesTests
{
    private const long MarketId = 3960010001;
    private const string System = "Hyades Sector AB-C d1";

    private static JournalEvent Event(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed)
            ? parsed!
            : throw new InvalidOperationException("Fixture JSON did not parse.");

    private static JournalEvent Depot(bool complete = false) =>
        Event(
            $$"""{"timestamp":"2026-10-21T12:00:00Z","event":"ColonisationConstructionDepot","MarketID":{{MarketId}},"ConstructionProgress":0.1,"ConstructionComplete":{{(complete ? "true" : "false")}},"ConstructionFailed":false,"ResourcesRequired":[{"Name":"$Steel_name;","Name_Localised":"Steel","RequiredAmount":1000,"ProvidedAmount":400,"Payment":100},{"Name":"$Aluminium_name;","Name_Localised":"Aluminium","RequiredAmount":500,"ProvidedAmount":500,"Payment":100}]}""");

    private static JournalEvent Contribution() =>
        Event(
            $$"""{"timestamp":"2026-10-21T12:05:00Z","event":"ColonisationContribution","MarketID":{{MarketId}},"Contributions":[{"Name":"$Steel_name;","Name_Localised":"Steel","Amount":120},{"Name":"$Titanium_name;","Name_Localised":"Titanium","Amount":30}]}""");

    private static string? Known(long marketId) => marketId == MarketId ? System : null;

    [Fact]
    public void ADepotEventWritesWhatIsStillNeeded()
    {
        var write = Assert.IsType<NeedsWrite>(Assert.Single(PlanWrites.From([Depot()], Known)));

        Assert.Equal(MarketId, write.MarketId);
        Assert.Equal(System, write.StarSystem);
        Assert.Equal(600, write.Remaining["steel"]);
        Assert.Equal(0, write.Remaining["aluminium"]);
        Assert.Equal(1500, write.MaxNeed);
    }

    [Fact]
    public void ACompletedDepotWritesCompletionAndNoNeeds()
    {
        var write = Assert.Single(PlanWrites.From([Depot(complete: true)], Known));

        Assert.IsType<CompleteWrite>(write);
    }

    [Fact]
    public void AContributionWritesEachRowsAmount()
    {
        var write = Assert.IsType<ContributionWrite>(Assert.Single(PlanWrites.From([Contribution()], Known)));

        Assert.Equal(120, write.Delivered["steel"]);
        Assert.Equal(30, write.Delivered["titanium"]);
    }

    [Fact]
    public void ASiteWithNoKnownSystemWritesNothing()
    {
        Assert.Empty(PlanWrites.From([Depot(), Contribution()], _ => null));
    }
}
