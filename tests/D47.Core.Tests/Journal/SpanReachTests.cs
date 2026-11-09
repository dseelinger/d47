using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>A span reach is a hundred jumps of the best ship the Commander owns.</summary>
public class SpanReachTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Boarding(int id, string hull, string name, double range) =>
        $$"""{"timestamp":"2026-08-20T00:01:00Z","event":"Loadout","Ship":"{{hull}}","ShipID":{{id}},"ShipName":"{{name}}","MaxJumpRange":{{range}},"Modules":[]}""";

    private static CommanderGameState State(params string[] events)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"2026-08-20T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        foreach (var json in events)
        {
            store.Apply(Event(json));
        }

        return store.Active!;
    }

    private const string Carrier =
        """{"timestamp":"2026-08-20T00:02:00Z","event":"CarrierStats","CarrierID":3715429376,"Callsign":"XQZ-11A","Name":"TEST","FuelLevel":800,"DockingAccess":"all"}""";

    [Fact]
    public void ABetterStoredShipSetsTheReach()
    {
        var reach = SpanReach.Of(State(
            Boarding(1, "asp", "FAR ONE", 40),
            """{"timestamp":"2026-08-20T00:01:30Z","event":"Location","StarSystem":"Sol"}""",
            Boarding(2, "sidewinder", "NEAR ONE", 10)));

        Assert.NotNull(reach);
        Assert.Equal(4000, reach.LightYears);
        Assert.Equal(1, reach.Ship!.ShipId);
        Assert.Equal("FAR ONE", reach.Ship.Name);
        Assert.False(reach.Ship.Aboard);
    }

    [Fact]
    public void ABetterShipAboardSetsTheReach()
    {
        var reach = SpanReach.Of(State(Boarding(1, "asp", "SLOW", 10), Boarding(2, "asp", "FAST", 30)));

        Assert.Equal(3000, reach!.LightYears);
        Assert.True(reach.Ship!.Aboard);
        Assert.Equal(2, reach.Ship.ShipId);
    }

    [Fact]
    public void ASoldShipDoesNotCount()
    {
        var reach = SpanReach.Of(State(
            Boarding(1, "asp", "GONE", 60),
            Boarding(2, "asp", "KEPT", 20),
            """{"timestamp":"2026-08-20T00:03:00Z","event":"ShipyardSell","SellShipID":1,"ShipType":"asp"}"""));

        Assert.Equal(2000, reach!.LightYears);
    }

    [Fact]
    public void ACarrierSetsAFloor()
    {
        var reach = SpanReach.Of(State(Boarding(1, "asp", "SLOW", 20), Carrier));

        Assert.Equal(5000, reach!.LightYears);
        Assert.Null(reach.Ship);
    }

    [Fact]
    public void AShipBeyondTheCarrierFloorStillSetsTheReach()
    {
        var reach = SpanReach.Of(State(Boarding(1, "asp", "FAR", 60), Carrier));

        Assert.Equal(6000, reach!.LightYears);
        Assert.Equal(1, reach.Ship!.ShipId);
    }

    [Fact]
    public void ACarrierAloneGivesTheFloor() =>
        Assert.Equal(5000, SpanReach.Of(State(Carrier))!.LightYears);

    [Fact]
    public void NoRangeAndNoCarrierGivesNothing() =>
        Assert.Null(SpanReach.Of(State()));

    [Fact]
    public void AShipWithoutARangeIsSkipped() =>
        Assert.Null(SpanReach.Of(State(
            """{"timestamp":"2026-08-20T00:01:00Z","event":"Loadout","Ship":"asp","ShipID":1,"Modules":[]}""")));

    [Fact]
    public void ATieGoesToTheShipAboard()
    {
        var reach = SpanReach.Of(State(Boarding(1, "asp", "STORED", 30), Boarding(2, "asp", "ABOARD", 30)));

        Assert.True(reach!.Ship!.Aboard);
        Assert.Equal(2, reach.Ship.ShipId);
    }
}
