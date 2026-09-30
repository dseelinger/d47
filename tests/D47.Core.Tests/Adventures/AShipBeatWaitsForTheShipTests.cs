using D47.Core.Adventures;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public class AShipBeatWaitsForTheShipTests
{
    private static readonly AdventureTrigger Corsair = new() { Kind = TriggerKind.Board, ShipType = "corsair" };

    private static D47.Core.Journal.JournalEvent Bought(string ship) =>
        Event($$"""{ "timestamp":"{{Stamp(Accepted)}}", "event":"ShipyardNew", "ShipType":"{{ship}}", "NewShipID":21 }""");

    private static D47.Core.Journal.JournalEvent Swapped(string ship) =>
        Event($$"""{ "timestamp":"{{Stamp(Accepted)}}", "event":"ShipyardSwap", "ShipType":"{{ship}}", "ShipID":9, "StoreOldShip":"Corsair" }""");

    [Fact]
    public void BuyingTheShipFiresTheBeat()
    {
        Assert.True(AdventureFold.Matches(Corsair, Bought("corsair")));
    }

    [Fact]
    public void SwappingIntoTheShipFiresTheBeat()
    {
        Assert.True(AdventureFold.Matches(Corsair with { ShipType = "mandalay" }, Swapped("mandalay")));
    }

    [Fact]
    public void AnotherShipDoesNotFireTheBeat()
    {
        Assert.False(AdventureFold.Matches(Corsair, Bought("mandalay")));
        Assert.False(AdventureFold.Matches(Corsair, Swapped("mandalay")));
    }

    [Fact]
    public void ATypeWithNoShipNamedNeverMatches()
    {
        var unnamed = new AdventureTrigger { Kind = TriggerKind.Board };

        Assert.False(unnamed.IsResolved);
        Assert.False(AdventureFold.Matches(unnamed, Bought("corsair")));
    }

    [Fact]
    public void AShipNoOneHasAHullNameForIsRefusedByName()
    {
        var adventure = LanternRoute() with
        {
            Beats = [Beat("Boarding", "setup", new AdventureTrigger { Kind = TriggerKind.Board, ShipType = "starbarge" }, "Aboard.")],
        };

        var problem = Assert.Single(AdventureValidation.Problems(adventure));

        Assert.Contains("Beat 1 (Boarding)", problem);
        Assert.Contains("starbarge", problem);
    }

    [Fact]
    public void AKnownShipIsAccepted()
    {
        var adventure = LanternRoute() with { Beats = [Beat("Boarding", "setup", Corsair, "Aboard.")] };

        Assert.Empty(AdventureValidation.Problems(adventure));
    }
}
