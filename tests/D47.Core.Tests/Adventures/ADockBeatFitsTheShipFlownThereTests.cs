using D47.Core.Adventures;
using D47.Core.Knowledge;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureGeneratorTests;

namespace D47.Core.Tests.Adventures;

public sealed class ADockBeatFitsTheShipFlownThereTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] LargeAboardSmallStored =
    [
        """{ "timestamp":"2026-08-22T11:01:01Z", "event":"Loadout", "Ship":"anaconda", "ShipID":1, "Modules":[] }""",
        """{ "timestamp":"2026-08-22T11:01:02Z", "event":"StoredShips", "StationName":"Oppi Port", "MarketID":5001, "StarSystem":"Oppi", "ShipsHere":[{ "ShipID":2, "ShipType":"sidewinder", "Name":"Pip", "Value":1000, "Hot":false }], "ShipsRemote":[] }""",
    ];

    private static readonly StationSummary SmallOnly = new()
    {
        Name = "Pebble Dock", SystemName = "Dyson's Hollow", SystemAddress = AdventureFixtures.Hollow, MarketId = 2001,
        Distance = 12, Type = "Outpost", SmallPads = 4, MediumPads = 0, LargePads = 0,
    };

    private const string DockWithoutBoarding = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Pebble", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Pebble Dock", "line": "To one name."},
          {"title": "The Freight Delivered", "function": "resolution", "kind": "rank", "reason": "Someone there knows about the burst.", "career": "Trade", "rank": 8, "line": "The freight is delivered."}
        ]}
        """;

    private const string DockAfterBoarding = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Hull", "function": "turn", "kind": "board", "reason": "Someone there knows about the burst.", "ship": "Sidewinder", "line": "Sign for it."},
          {"title": "The Pebble", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Pebble Dock", "line": "To one name."}
        ]}
        """;

    [Fact]
    public async Task ADockBeatAtASmallPadStationIsRefusedWhileALargeShipIsAboard()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(DockWithoutBoarding),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(GoodBeats, 2)));

        var outcome = await Generator(provider, new Galaxy { Extra = [SmallOnly] }, 1, LargeAboardSmallStored)
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains(outcome.Notes, note => note.StartsWith("Rewrote 1 objective", StringComparison.Ordinal));
        Assert.Contains("Pebble Dock", provider.Requests[3].Prompt.History[0].Text);
    }

    [Fact]
    public async Task TheSameDockBeatStandsAfterABoardBeatForTheSmallShip()
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(DockAfterBoarding));

        var outcome = await Generator(provider, new Galaxy { Extra = [SmallOnly] }, 1, LargeAboardSmallStored)
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short, Brief: "a story that ends with me in a sidewinder"), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("Pebble Dock", outcome.Draft!.Beats[2].Trigger.Station);
    }
}
