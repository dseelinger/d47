using System.Text.Json;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// The figures a carrier page needs, folded into the state that already discriminates the
/// Commander's carrier from a squadron's
/// (<a href="https://github.com/dseelinger/d47/issues/230">#230</a>).
/// <para>
/// <b>Here rather than in a carrier model of its own.</b> The first attempt at #230 built a
/// parallel watch keyed by <c>CarrierID</c>, which would have reintroduced the fault
/// <see cref="CarrierState"/> already records and fixes — a squadron's carrier read as the
/// Commander's own, reported 2026-08-21 and settled against the corpus.
/// </para>
/// <para>
/// The event bodies are taken from the Commander's own journals rather than invented.
/// </para>
/// </summary>
public class TheCarrierPageHasItsFiguresTests
{
    private static JournalEvent Event(string kind, string json, int minute = 0) =>
        new(new DateTimeOffset(2026, 8, 29, 23, minute, 0, TimeSpan.Zero),
            kind,
            JsonDocument.Parse(json).RootElement);

    private const string Stats = """
        {"event":"CarrierStats","CarrierID":3715429376,"CarrierType":"FleetCarrier",
         "Callsign":"BNH-T2F","Name":"Sacred Fire","DockingAccess":"all","AllowNotorious":false,
         "FuelLevel":792,"JumpRangeCurr":500.0,"JumpRangeMax":500.0,"PendingDecommission":false,
         "SpaceUsage":{"TotalCapacity":25000,"Crew":930,"Cargo":540,"FreeSpace":23530},
         "Finance":{"CarrierBalance":750352669,"ReserveBalance":0},
         "Crew":[{"CrewRole":"BlackMarket","Activated":false},
                 {"CrewRole":"Refuel","Activated":true,"Enabled":true,"CrewName":"Rosa Guthrie"}]}
        """;

    [Fact]
    public void TheStatsEventCarriesTheFiguresThePageDraws()
    {
        var carrier = CarrierState.None.Apply(Event("CarrierStats", Stats));

        Assert.Equal(750352669, carrier.Balance);
        Assert.Equal(25000, carrier.Capacity);
        Assert.Equal(23530, carrier.FreeSpace);
        Assert.Equal(500.0, carrier.JumpRange);
        Assert.False(carrier.PendingDecommission);

        // 1,470 of 25,000 used.
        Assert.Equal(0.0588, carrier.HowFull!.Value, 4);
    }

    /// <summary>
    /// Bought and switched on are different states, and only one of them is something a Commander
    /// can dock and use.
    /// </summary>
    [Fact]
    public void ServicesSayWhetherTheyAreOpen()
    {
        var carrier = CarrierState.None.Apply(Event("CarrierStats", Stats));

        Assert.Equal(2, carrier.Services.Count);
        Assert.False(carrier.Services.Single(service => service.Role == "BlackMarket").IsOpen);

        var refuel = carrier.Services.Single(service => service.Role == "Refuel");

        Assert.True(refuel.IsOpen);
        Assert.Equal("Rosa Guthrie", refuel.Name);
    }

    /// <summary>
    /// A stats event with no Crew array says nothing about the services. Reading that silence as
    /// "they are all gone" is how a page comes to report a carrier with nobody aboard.
    /// </summary>
    [Fact]
    public void SilenceAboutTheCrewDoesNotEmptyIt()
    {
        var carrier = CarrierState.None
            .Apply(Event("CarrierStats", Stats))
            .Apply(Event(
                "CarrierStats",
                """
                {"event":"CarrierStats","CarrierID":3715429376,"CarrierType":"FleetCarrier",
                 "Callsign":"BNH-T2F","Name":"Sacred Fire","FuelLevel":700}
                """,
                minute: 30));

        Assert.Equal(700, carrier.FuelLevel);
        Assert.Equal(2, carrier.Services.Count);
    }

    /// <summary>
    /// The destination body rides with the system, and both ways out of a jump clear it. Leaving
    /// it set would have the page naming a parking spot the carrier is no longer going to.
    /// </summary>
    [Theory]
    [InlineData("CarrierJumpCancelled", """{"event":"CarrierJumpCancelled","CarrierID":3715429376}""")]
    [InlineData("CarrierJump", """{"event":"CarrierJump","CarrierID":3715429376,"StarSystem":"Kuwemaki"}""")]
    public void TheDestinationBodyIsClearedWithTheJump(string kind, string json)
    {
        var booked = CarrierState.None
            .Apply(Event("CarrierStats", Stats))
            .Apply(Event(
                "CarrierJumpRequest",
                """
                {"event":"CarrierJumpRequest","CarrierType":"FleetCarrier","CarrierID":3715429376,
                 "SystemName":"Kuwemaki","Body":"Kuwemaki A 3","DepartureTime":"2026-08-29T23:45:10Z"}
                """));

        Assert.Equal("Kuwemaki", booked.DestinationSystem);
        Assert.Equal("Kuwemaki A 3", booked.DestinationBody);

        var after = booked.Apply(Event(kind, json, minute: 46));

        Assert.Null(after.DestinationSystem);
        Assert.Null(after.DestinationBody);
    }

    /// <summary>Nothing is claimed about a carrier the journal has never described.</summary>
    [Fact]
    public void ACommanderWithNoCarrierHasNoFigures()
    {
        Assert.Null(CarrierState.None.Balance);
        Assert.Null(CarrierState.None.HowFull);
        Assert.Empty(CarrierState.None.Services);
        Assert.False(CarrierState.None.Owned);
    }
}
