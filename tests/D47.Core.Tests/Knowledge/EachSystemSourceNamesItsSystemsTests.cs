using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Knowledge;

public class EachSystemSourceNamesItsSystemsTests
{
    private static readonly DateTimeOffset At = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

    private static RoutePlanBook Book() =>
        new("route-plans.json", new MemoryFileSystem(), NullLogger<RoutePlanBook>.Instance);

    [Fact]
    public void AnEmptyPlanBookNamesNothing() => Assert.Empty(Book().Systems());

    [Fact]
    public void APlanBookNamesTheJumpsEndsAndWaypointsThenTheRichesAndTradeStops()
    {
        var book = Book();

        book.Record(
            new PlottedRoute("Sol", "Colonia", 100, 2, [new RouteWaypoint("Wolf 359", 8, 50, true), new RouteWaypoint("Colonia", 8, 0, false)]),
            "Sol to Colonia",
            At);
        book.Record(new RichesRoute([new RichesStop("Alpha Centauri", 1, [])]), "Riches", At);
        book.Record(new TradeRoute([new TradeStop("Diaguandri", "Ray Gateway")]), "Trade", At);

        Assert.Equal(
            ["Sol", "Colonia", "Wolf 359", "Colonia", "Alpha Centauri", "Diaguandri"],
            book.Systems().OfType<string>().ToArray());
    }

    [Fact]
    public void AnEmptyBoardNamesNothing() => Assert.Empty(new CommodityBoard().Systems());

    [Fact]
    public void ABoardNamesWhereTheLastAnswerWasMeasuredFromAndWhereEachOfferIs()
    {
        var board = new CommodityBoard();
        var offer = new CommodityOffer(new MarketSnapshot { Station = "Ray Gateway", System = "Diaguandri" }, 100, 10, 5);

        board.Post(new CommodityPosting(new CommodityQuery("gold"), new CommodityAnswer([offer], 1, 0, true), "Sol", At));

        Assert.Equal(["Sol", "Diaguandri"], board.Systems().OfType<string>().ToArray());
    }

    [Fact]
    public void NoCommanderNamesNothing() => Assert.Empty(CommanderSystems.Of(null));

    [Fact]
    public void ACommanderNamesTheirLocationCarriersShipsModulesAndConstructionSites()
    {
        var commander = new CommanderGameState(new CommanderIdentity("F1", "Tester"));

        string[] lines =
        [
            """{"timestamp":"2026-08-20T09:00:00Z","event":"Location","StarSystem":"Site System"}""",
            """{"timestamp":"2026-08-20T09:00:01Z","event":"CarrierLocation","CarrierType":"FleetCarrier","CarrierID":1,"StarSystem":"Fleet System"}""",
            """{"timestamp":"2026-08-20T09:00:02Z","event":"CarrierJumpRequest","CarrierType":"FleetCarrier","CarrierID":1,"SystemName":"Fleet Next"}""",
            """{"timestamp":"2026-08-20T09:00:03Z","event":"CarrierLocation","CarrierType":"SquadronCarrier","CarrierID":2,"StarSystem":"Squadron System"}""",
            """{"timestamp":"2026-08-20T09:00:04Z","event":"CarrierJumpRequest","CarrierType":"SquadronCarrier","CarrierID":2,"SystemName":"Squadron Next"}""",
            """{"timestamp":"2026-08-20T09:00:05Z","event":"StoredShips","StarSystem":"Ship System","StationName":"Hangar","MarketID":3,"ShipsHere":[{"ShipID":5,"ShipType":"python"}],"ShipsRemote":[]}""",
            """{"timestamp":"2026-08-20T09:00:06Z","event":"StoredModules","StarSystem":"Module System","StationName":"Hangar","MarketID":3,"Items":[{"Name":"$int_hyperdrive_size5_class5","StarSystem":"Module System"}]}""",
            """{"timestamp":"2026-08-20T09:00:07Z","event":"ColonisationConstructionDepot","MarketID":4,"ConstructionProgress":0.5,"ConstructionComplete":false,"ConstructionFailed":false,"ResourcesRequired":[]}""",
            """{"timestamp":"2026-08-20T09:00:08Z","event":"Location","StarSystem":"Sol"}""",
        ];

        foreach (var line in lines)
        {
            commander.Apply(Event(line));
        }

        var names = CommanderSystems.Of(commander).ToArray();

        Assert.Contains("Sol", names);
        Assert.Contains("Fleet System", names);
        Assert.Contains("Fleet Next", names);
        Assert.Contains("Squadron System", names);
        Assert.Contains("Squadron Next", names);
        Assert.Contains("Ship System", names);
        Assert.Contains("Module System", names);
        Assert.Contains("Site System", names);
    }
}
