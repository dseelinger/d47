using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class ACarrierRouteReadsTheHoldFromTheCarrierTests
{
    private const string Stats =
        """
        {"timestamp":"2026-09-05T12:00:00Z","event":"CarrierStats","CarrierID":3700000000,"Callsign":"K7Q-B4X",
         "Name":"Sacred Fire","CarrierType":"FleetCarrier","FuelLevel":600,
         "SpaceUsage":{"TotalCapacity":25000,"Crew":6000,"Cargo":500,"CargoSpaceReserved":0,"ShipPacks":0,
                       "ModulePacks":0,"FreeSpace":18500},
         "Crew":[{"CrewRole":"Refuel","Activated":true,"Enabled":true,"CrewName":"Ana"}]}
        """;

    private static CarrierState Carrier()
    {
        Assert.True(JournalEvent.TryParse(Stats, NullLogger.Instance, out var parsed));
        return CarrierState.None.Apply(parsed!) with { StarSystem = "Sol" };
    }

    [Fact]
    public void TheHoldIsCapacityLessFreeSpaceSoCrewAboardCounts()
    {
        Assert.True(CarrierRouteQuery.TryFrom(Carrier(), null, "Colonia", false, out var query, out _));

        Assert.Equal(25_000, query.Capacity);
        Assert.Equal(6_500, query.CapacityUsed);
        Assert.Equal(600, query.FuelLoaded);
        Assert.Equal("Sol", query.Source);
        Assert.Equal(["Colonia"], query.Destinations);
    }

    [Fact]
    public void ATripThereAndBackEndsAtTheSource()
    {
        Assert.True(CarrierRouteQuery.TryFrom(Carrier(), "Sol", "Colonia", true, out var query, out _));

        Assert.Equal(["Colonia", "Sol"], query.Destinations);
    }

    [Fact]
    public void TritiumInTheHoldNeverExceedsTheSpaceUsed()
    {
        var carrier = Carrier() with { TritiumInHold = 9_000, TritiumInHoldUncertain = false };

        Assert.True(CarrierRouteQuery.TryFrom(carrier, null, "Colonia", false, out var query, out _));

        Assert.Equal(6_500, query.TritiumStored);
        Assert.False(query.TritiumUncertain);
    }

    [Fact]
    public void ATritiumFigureTheJournalNeverGaveIsFlaggedNotInvented()
    {
        Assert.True(CarrierRouteQuery.TryFrom(Carrier(), null, "Colonia", false, out var unread, out _));
        Assert.Equal(0, unread.TritiumStored);
        Assert.True(unread.TritiumUncertain);

        var moved = Carrier() with { TritiumInHold = 100, TritiumInHoldUncertain = true };

        Assert.True(CarrierRouteQuery.TryFrom(moved, null, "Colonia", false, out var uncertain, out _));
        Assert.Equal(100, uncertain.TritiumStored);
        Assert.True(uncertain.TritiumUncertain);
    }

    [Fact]
    public void ACommanderWithoutACarrierIsRefused()
    {
        Assert.False(CarrierRouteQuery.TryFrom(CarrierState.None, null, "Colonia", false, out _, out var failure));
        Assert.Contains("carrier", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void ASquadronCarrierIsRefused()
    {
        var squadron = Carrier() with { IsSquadron = true };

        Assert.False(CarrierRouteQuery.TryFrom(squadron, null, "Colonia", false, out _, out var failure));
        Assert.Contains("squadron", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void AHoldThatWasNeverReadIsRefusedRatherThanPlottedAsEmpty()
    {
        var unread = Carrier() with { FreeSpace = null };

        Assert.False(CarrierRouteQuery.TryFrom(unread, null, "Colonia", false, out _, out var failure));
        Assert.Equal("Open carrier management once so I can read the hold.", failure);
    }
}
