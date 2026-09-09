using System.Text.Json;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests;

/// <summary>The squadron's carrier, followed beside the Commander's own and never merged with it.</summary>
public class TheSquadronCarrierIsKeptApartTests
{
    private static JournalEvent Event(string json, int minute = 0)
    {
        var root = JsonDocument.Parse(json).RootElement;

        return new JournalEvent(
            new DateTimeOffset(2026, 8, 29, 23, minute, 0, TimeSpan.Zero),
            root.GetProperty("event").GetString()!,
            root);
    }

    private const string Own = """
        {"event":"CarrierStats","CarrierID":3715429376,"CarrierType":"FleetCarrier",
         "Callsign":"BNH-T2F","Name":"Sacred Fire","FuelLevel":792}
        """;

    private const string Theirs = """
        {"event":"CarrierStats","CarrierID":3713474048,"CarrierType":"SquadronCarrier",
         "Callsign":"QRS-11X","Name":"Wandering Home","FuelLevel":140}
        """;

    private static (CarrierState Own, CarrierState Squadron) Read(params string[] events)
    {
        var own = CarrierState.None;
        var squadron = CarrierState.NoSquadron;

        for (var index = 0; index < events.Length; index++)
        {
            var read = Event(events[index], index);

            own = own.Apply(read);
            squadron = squadron.Apply(read);
        }

        return (own, squadron);
    }

    /// <summary>
    /// Both in one journal, and each ends up with its own figures whichever order they arrive in.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EachCarrierKeepsItsOwnFigures(bool squadronLast)
    {
        var (own, squadron) = squadronLast ? Read(Own, Theirs) : Read(Theirs, Own);

        Assert.Equal("BNH-T2F", own.CallSign);
        Assert.Equal("Sacred Fire", own.Name);
        Assert.Equal(792, own.FuelLevel);

        Assert.Equal("QRS-11X", squadron.CallSign);
        Assert.Equal("Wandering Home", squadron.Name);
        Assert.Equal(140, squadron.FuelLevel);
    }

    /// <summary>The squadron's location never reaches the Commander's own carrier.</summary>
    [Fact]
    public void TheSquadronsLocationDoesNotMoveTheCommandersCarrier()
    {
        var (own, squadron) = Read(
            Own,
            """
            {"event":"CarrierLocation","CarrierType":"FleetCarrier","CarrierID":3715429376,
             "StarSystem":"Kuwemaki"}
            """,
            """
            {"event":"CarrierLocation","CarrierType":"SquadronCarrier","CarrierID":3713474048,
             "StarSystem":"Col 285 Sector GT-G c11-9"}
            """);

        Assert.Equal("Kuwemaki", own.StarSystem);
        Assert.Equal("Col 285 Sector GT-G c11-9", squadron.StarSystem);
    }

    /// <summary>The asymmetry, and it is the safety property.</summary>
    [Fact]
    public void AJournalFromBeforeTheTypeFieldConjuresNoSquadronCarrier()
    {
        var (own, squadron) = Read(
            """
            {"event":"CarrierStats","CarrierID":3715429376,"Callsign":"BNH-T2F",
             "Name":"Sacred Fire","FuelLevel":792}
            """,
            """{"event":"CarrierLocation","CarrierID":3715429376,"StarSystem":"Kuwemaki"}""");

        Assert.True(own.Owned);
        Assert.Equal("Kuwemaki", own.StarSystem);

        Assert.False(squadron.Owned);
        Assert.Null(squadron.StarSystem);
        Assert.Null(squadron.CallSign);
    }

    /// <summary>
    /// A squadron carrier learns its callsign at the airlock exactly as the Commander's own does — by
    /// id, from a docking whose <c>MarketID</c> is the carrier's.
    /// </summary>
    [Fact]
    public void ADockAtTheSquadronsCarrierNamesItAndNotTheOtherOne()
    {
        var (own, squadron) = Read(
            Own,

            // Known by id and type, with no callsign — which is every CarrierLocation ever written: 0 of
            // 1,134 in the corpus carry one.
            """
            {"event":"CarrierLocation","CarrierType":"SquadronCarrier","CarrierID":3713474048,
             "StarSystem":"Col 285 Sector GT-G c11-9"}
            """,
            """
            {"event":"Docked","MarketID":3713474048,"StationName":"QRS-11X",
             "StationType":"FleetCarrier","StarSystem":"Col 285 Sector GT-G c11-9"}
            """);

        Assert.Equal("QRS-11X", squadron.CallSign);
        Assert.True(squadron.Owned);

        // And the Commander's own kept its own name throughout.
        Assert.Equal("BNH-T2F", own.CallSign);
    }

    /// <summary>
    /// And an event that says <c>FleetCarrier</c> is refused by the squadron side before its id is ever
    /// consulted, so the two cannot cross even if a state has picked up the wrong id.
    /// </summary>
    [Fact]
    public void AnEventSayingFleetCarrierNeverReachesTheSquadronSide()
    {
        var (_, squadron) = Read(
            Theirs,
            """
            {"event":"CarrierLocation","CarrierType":"FleetCarrier","CarrierID":3713474048,
             "StarSystem":"Somewhere Else"}
            """);

        Assert.NotEqual("Somewhere Else", squadron.StarSystem);
    }

    /// <summary>A Commander in no squadron has no squadron carrier, and nothing pretends otherwise.</summary>
    [Fact]
    public void NoSquadronMeansNoSquadronCarrier()
    {
        var (own, squadron) = Read(Own);

        Assert.True(own.Owned);
        Assert.False(squadron.Owned);
    }
}
