using D47.Core.Adventures;
using D47.Core.Knowledge;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Adventures;

public class TheAcknowledgementSaysWhatTheCommanderDidTests
{
    private static AdventureTrigger Filled(TriggerKind kind) => new()
    {
        Kind = kind,
        Count = 5,
        SystemAddress = kind == TriggerKind.Beacon ? GuardianCores.Beacons.Keys.First() : 1,
        MarketId = 2,
        BodyId = 3,
        System = "Ossen's Lantern",
        Station = "Jameson Memorial",
        Body = "Ossen's Lantern A 1",
        Career = "Exploration",
        Rank = 6,
        ShipType = "sidewinder",
        Faction = "Lantern Dominion",
        Handle = "the long haul",
        Engineer = "Felicity Farseer",
        Stage = "Unlocked",
        SiteName = "Site 7",
        Filter = kind switch
        {
            TriggerKind.Unlock => TechBrokerCatalogue.Joined[0].Name,
            TriggerKind.Site => AdventureTrigger.TouristSite,
            _ => null,
        },
    };

    public static TheoryData<TriggerKind> Kinds()
    {
        var kinds = new TheoryData<TriggerKind>();
        foreach (var kind in Enum.GetValues<TriggerKind>())
        {
            kinds.Add(kind);
        }

        return kinds;
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EveryKindHasASecondPersonPastTenseSentenceOrIsNamedAsHavingNone(TriggerKind kind)
    {
        var done = Filled(kind).Done();

        if (kind is TriggerKind.Choice or TriggerKind.Event)
        {
            Assert.Null(done);
            return;
        }

        Assert.NotNull(done);
        Assert.StartsWith("You ", done, StringComparison.Ordinal);
        Assert.EndsWith(".", done, StringComparison.Ordinal);
    }

    [Fact]
    public void ADockNamesTheStationAndItsSystem() =>
        Assert.Equal(
            "You docked at Jameson Memorial in Shinrarta Dezhra.",
            new AdventureTrigger { Kind = TriggerKind.Dock, MarketId = 2, Station = "Jameson Memorial", System = "Shinrarta Dezhra" }.Done());

    [Fact]
    public void ACountedObjectiveNamesItsTargetNotTheRunningTotal() =>
        Assert.Equal("You collected 5 bounties.", new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 5 }.Done());

    [Fact]
    public void ADockWithNoStationNameIsTheStockLineAlone() =>
        Assert.Null(new AdventureTrigger { Kind = TriggerKind.Dock, MarketId = 2 }.Done());

    [Fact]
    public void AnArriveWithNoSystemNameIsTheStockLineAlone() =>
        Assert.Null(new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 1234567 }.Done());
}
