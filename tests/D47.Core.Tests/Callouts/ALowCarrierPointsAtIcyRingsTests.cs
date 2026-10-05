using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class ALowCarrierPointsAtIcyRingsTests
{
    private const long CarrierId = 3700000000L;

    private const string Laser = """{"Slot":"HugeHardpoint1","Item":"hpt_miningtoolv2_fixed_huge","On":true,"Priority":0}""";
    private const string Collector = """{"Slot":"Slot01_Size3","Item":"int_dronecontrol_collection_size3_class3","On":true,"Priority":0}""";
    private const string Refinery = """{"Slot":"Slot02_Size3","Item":"int_refinery_size3_class2","On":true,"Priority":0}""";

    private static readonly DateTimeOffset Noon = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Event(string kind, params (string Key, object? Value)[] fields)
    {
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = Noon.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["event"] = kind,
        };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        return Parse(JsonSerializer.Serialize(payload));
    }

    private static JournalEvent Loadout(int id, string ship, string name, params string[] modules) =>
        Parse($$"""{"timestamp":"2026-09-05T10:0{{id}}:00Z","event":"Loadout","Ship":"{{ship}}","ShipID":{{id}},"ShipName":"{{name}}","CargoCapacity":100,"Modules":[{{string.Join(",", modules)}}]}""");

    private static readonly JournalEvent Dock = Event(
        "Docked",
        ("StationName", "K7Q-B4X"),
        ("StationType", "FleetCarrier"),
        ("MarketID", CarrierId));

    private static CommanderGameState State(params JournalEvent[] loadouts)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        state.Apply(Event(
            "CarrierStats",
            ("CarrierType", "FleetCarrier"),
            ("Callsign", "K7Q-B4X"),
            ("Name", "Sacred Fire"),
            ("CarrierID", CarrierId),
            ("FuelLevel", 150),
            ("JumpRangeMax", 500.0),
            ("SpaceUsage", new Dictionary<string, int> { ["TotalCapacity"] = 17_000, ["FreeSpace"] = 8_946 })));

        foreach (var loadout in loadouts)
        {
            state.Apply(loadout);
        }

        state.Apply(Dock);
        return state;
    }

    private static CarrierWaypoint Stop(string name, bool restock = false, bool icy = false, bool pristine = false) =>
        new(name, 100, 0, 1_000, 20, restock, 0, 0, icy, pristine, false);

    private static StoredRoutePlan Plan(int? reached, long? carrierId = CarrierId) => new()
    {
        Kind = RoutePlanKind.Carrier,
        Headline = "Sol to Colonia",
        Carrier = new CarrierRoute(
        [
            Stop("Sol", icy: true, pristine: true),
            Stop("Alpha", restock: true),
            Stop("Beta", icy: true, pristine: true),
            Stop("Gamma"),
            Stop("Delta", icy: true, pristine: true),
            Stop("Epsilon", icy: true),
            Stop("Zeta", icy: true, pristine: true),
            Stop("Eta", icy: true, pristine: true),
        ]),
        CarrierId = carrierId,
        Reached = reached,
    };

    private static string? Said(StoredRoutePlan? plan, CommanderGameState state)
    {
        var context = new CalloutContext(Noon, false, state, GameStatus.Unknown, NavRoute.None, [Dock]);
        var callout = new CarrierFuelCallout { Plan = () => plan };

        return callout.Examine(context).SingleOrDefault()?.Text;
    }

    [Fact]
    public void PastTheLastRestockItNamesTheNearestPristineIcyRingsAndTheFitShip()
    {
        var state = State(Loadout(1, "Type11", "Hammer", Laser, Collector, Refinery));

        var text = Said(Plan(reached: 3), state);

        Assert.Contains(
            "Pristine icy rings: Beta, one jump behind, Delta, one jump ahead, Sol, 3 jumps behind, and 2 more.",
            text,
            StringComparison.Ordinal);
        Assert.Contains("Fitted to mine it: Hammer, a Type-11", text, StringComparison.Ordinal);
        Assert.Contains("a refinery", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BeforeTheLastRestockTheWarningIsUnchanged()
    {
        var state = State(Loadout(1, "Type11", "Hammer", Laser, Collector, Refinery));

        var text = Said(Plan(reached: 1), state);

        Assert.Equal("Sacred Fire has 150 tonnes of tritium; a full jump at this load burns 88.", text);
    }

    [Fact]
    public void WithNoPlanTheWarningIsUnchanged()
    {
        var state = State(Loadout(1, "Type11", "Hammer", Laser, Collector, Refinery));

        Assert.Equal("Sacred Fire has 150 tonnes of tritium; a full jump at this load burns 88.", Said(null, state));
    }

    [Fact]
    public void AnotherCarriersPlanIsIgnored()
    {
        var state = State();

        Assert.DoesNotContain("icy", Said(Plan(reached: 3, carrierId: 1L), state)!, StringComparison.Ordinal);
    }

    [Fact]
    public void WithNoFitShipItSaysSo()
    {
        var state = State(Loadout(1, "Type9", "Mule", Laser, Collector));

        var text = Said(Plan(reached: 3), state);

        Assert.EndsWith("None of your ships is fitted to mine it.", text, StringComparison.Ordinal);
    }
}
