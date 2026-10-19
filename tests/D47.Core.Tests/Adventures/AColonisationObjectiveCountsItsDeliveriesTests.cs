using D47.Core.Adventures;
using D47.Core.Journal;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public class AColonisationObjectiveCountsItsDeliveriesTests
{
    private const string Delivery = """
        { "timestamp":"@", "event":"ColonisationContribution", "MarketID":3700000001, "Contributions":[
            { "Name":"$ComputerComponents_name;", "Name_Localised":"Computer Components", "Amount":20 },
            { "Name":"$FoodCartridges_name;", "Name_Localised":"Food Cartridges", "Amount":30 } ] }
        """;

    private const string Claim = """{ "timestamp":"@", "event":"ColonisationSystemClaim", "StarSystem":"Ossen's Lantern", "SystemAddress":1 }""";

    private static AdventureTrigger Contribute(string? commodity, int count = 40) =>
        new() { Kind = TriggerKind.Contribute, Count = count, Commodity = commodity };

    private static JournalEvent Journal(string line) => Event(line.Replace("@", Stamp(Accepted.AddMinutes(1)), StringComparison.Ordinal));

    [Theory]
    [InlineData(null, 50)]
    [InlineData("computercomponents", 20)]
    [InlineData("$FoodCartridges_name;", 30)]
    [InlineData("Food Cartridges", 30)]
    public void ADeliveryAddsTheTonsOfItsMatchingCommodities(string? commodity, int tons)
    {
        var delivery = Journal(Delivery);

        Assert.True(AdventureFold.Matches(Contribute(commodity), delivery));
        Assert.Equal(tons, AdventureFold.Amount(Contribute(commodity), delivery));
    }

    [Fact]
    public void ADeliveryOfAnotherCommodityDoesNotCount() =>
        Assert.False(AdventureFold.Matches(Contribute("platinum"), Journal(Delivery)));

    [Fact]
    public void AClaimMeetsAClaimObjectiveOnly()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.Claim };

        Assert.True(trigger.IsResolved);
        Assert.False(trigger.IsCounted);
        Assert.True(AdventureFold.Matches(trigger, Journal(Claim)));
        Assert.False(AdventureFold.Matches(trigger, Journal(Delivery)));
        Assert.False(AdventureFold.Matches(Contribute(null), Journal(Claim)));
    }

    [Fact]
    public void AContributeObjectiveIsCountedAndSaysSo()
    {
        var trigger = Contribute(null);

        Assert.True(trigger.IsCounted);
        Assert.Equal("Tons delivered to construction sites: 0 of 40", trigger.Progress(0));
        Assert.Equal("deliver 40 t of any commodity to colonisation construction sites", trigger.Describe());
        Assert.Equal("deliver cargo to colonisation construction sites", RefusedActivities.Phrase(TriggerKind.Contribute, null));
        Assert.Equal("claim a system for colonisation", RefusedActivities.Phrase(TriggerKind.Claim, null));
    }

    [Fact]
    public void ABeatThatNamesAColonisationKindIsAccepted()
    {
        Assert.True(AdventureValidation.TryKind("contribute", out var contribute));
        Assert.Equal(TriggerKind.Contribute, contribute);
        Assert.True(AdventureValidation.TryKind("claim", out var claim));
        Assert.Equal(TriggerKind.Claim, claim);
    }
}
