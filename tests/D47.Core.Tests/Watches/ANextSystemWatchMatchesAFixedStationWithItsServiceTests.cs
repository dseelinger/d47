using D47.Core.Knowledge;
using D47.Core.Watches;
using Xunit;

namespace D47.Core.Tests.Watches;

public class ANextSystemWatchMatchesAFixedStationWithItsServiceTests
{
    private static Watch For(string service, string? kind = null, PadSize? pad = null) => new(
        Guid.NewGuid(), "Watch", WatchKind.NextSystem, Service: service, ServiceKind: kind, MinPad: pad);

    private static StationSummary Station(
        string name,
        IReadOnlyList<string> services,
        string system = "Kremainn",
        string type = "Orbis Starport",
        double? distance = 240,
        string? trader = null,
        string? broker = null,
        int mediumPads = 6,
        int largePads = 4) => new()
    {
        Name = name,
        SystemName = system,
        Type = type,
        DistanceToArrival = distance,
        TraderType = trader,
        TechnologyBroker = broker,
        Services = services,
        SmallPads = 4,
        MediumPads = mediumPads,
        LargePads = largePads,
    };

    [Fact]
    public void AServiceIsMatchedByTheIndexSpellingOfItsName()
    {
        var factors = Station("Cheranovsky City", ["Interstellar Factors Contact"]);
        var rearm = Station("Rearm Port", ["Restock"]);

        Assert.Same(factors, WatchRule.Arrival(For("Interstellar Factors"), "Kremainn", [factors]));
        Assert.Same(rearm, WatchRule.Arrival(For("Rearm"), "Kremainn", [rearm]));
        Assert.Null(WatchRule.Arrival(For("Black Market"), "Kremainn", [factors, rearm]));
    }

    [Fact]
    public void ABrokerKindIsMatchedAndAnyKindMatchesEither()
    {
        var human = Station("Human Broker", ["Technology Broker"], broker: "Human", distance: 100);
        var guardian = Station("Guardian Broker", ["Technology Broker"], broker: "Guardian", distance: 900);

        Assert.Same(guardian, WatchRule.Arrival(For("Technology Broker", "Guardian"), "Kremainn", [human, guardian]));
        Assert.Same(human, WatchRule.Arrival(For("Technology Broker"), "Kremainn", [guardian, human]));
    }

    [Fact]
    public void AMediumPadAdmitsAMediumOrLargePadAndALargeOnlyALarge()
    {
        var outpost = Station("Outpost", ["Black Market"], type: "Outpost", mediumPads: 2, largePads: 0);

        Assert.Same(outpost, WatchRule.Arrival(For("Black Market", pad: PadSize.Medium), "Kremainn", [outpost]));
        Assert.Null(WatchRule.Arrival(For("Black Market", pad: PadSize.Large), "Kremainn", [outpost]));
    }

    [Fact]
    public void AStationInANeighbouringSystemOrAMovingOneIsLeftOut()
    {
        var neighbour = Station("Next Door", ["Black Market"], system: "Kremainn B");
        var megaship = Station("Some Megaship", ["Black Market"], type: "Mega ship");
        var site = Station("Orbital Construction Site: Somewhere", ["Black Market"]);

        Assert.Null(WatchRule.Arrival(For("Black Market"), "Kremainn", [neighbour, megaship, site]));
    }

    [Fact]
    public void TheNearestToArrivalIsChosenAndAnUnknownDistanceComesLast()
    {
        var unknown = Station("Unknown", ["Black Market"], distance: null);
        var far = Station("Far", ["Black Market"], distance: 5000);
        var near = Station("Near", ["Black Market"], distance: 12);

        Assert.Same(near, WatchRule.Arrival(For("Black Market"), "Kremainn", [unknown, far, near]));
        Assert.Same(far, WatchRule.Arrival(For("Black Market"), "Kremainn", [unknown, far]));
    }

    [Fact]
    public void AnotherKindOfWatchMatchesNothing()
    {
        var station = Station("Port", ["Black Market"]);

        Assert.Null(WatchRule.Arrival(For("Black Market") with { Kind = WatchKind.Stock }, "Kremainn", [station]));
    }

    [Fact]
    public void TheNamesAndConditionAreWordedForThePageAndTheVoice()
    {
        Assert.Equal("Manufactured material trader", NextSystemWatch.ServiceName("Material Trader", "Manufactured"));
        Assert.Equal("Material trader", NextSystemWatch.ServiceName("material trader", null));
        Assert.Equal("Guardian technology broker", NextSystemWatch.ServiceName("Technology Broker", "Guardian"));
        Assert.Equal("Interstellar Factors", NextSystemWatch.ServiceName("Interstellar Factors", null));

        Assert.Equal("a Guardian technology broker", NextSystemWatch.Said("Technology Broker", "Guardian"));
        Assert.Equal("an Outfitting", NextSystemWatch.Said("Outfitting", null));
        Assert.Equal("Interstellar Factors", NextSystemWatch.Said("Interstellar Factors", null));

        Assert.Equal(
            "a station with a Manufactured material trader, any pad, in the next system",
            NextSystemWatch.Condition(For("Material Trader", "Manufactured")));
        Assert.Equal(
            "a station with Interstellar Factors, a large pad, in the next system",
            NextSystemWatch.Condition(For("Interstellar Factors", pad: PadSize.Large)));
    }

    [Fact]
    public void TheLineLeavesOutWhatTheStationDoesNotReport()
    {
        var station = new StationSummary { Name = "Quiet Hub", SystemName = "Kremainn", Services = ["Black Market"] };

        Assert.Equal(
            "Kremainn has a Black Market, Commander. Quiet Hub. That watch is off now.",
            NextSystemWatch.Line("Kremainn", For("Black Market"), station));

        var far = station with { DistanceToArrival = 1180.4, SmallPads = 2, MediumPads = 1, LargePads = 0 };

        Assert.Equal(
            "Kremainn has a Black Market, Commander. Quiet Hub, 1,180 light seconds, medium pad. That watch is off now.",
            NextSystemWatch.Line("Kremainn", For("Black Market"), far));
    }
}
