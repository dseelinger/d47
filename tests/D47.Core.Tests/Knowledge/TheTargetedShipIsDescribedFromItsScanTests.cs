using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary><c>get_target</c> describes a locked ship from its scan (#2287).</summary>
public class TheTargetedShipIsDescribedFromItsScanTests
{
    private const string Npc = """{ "timestamp":"2026-11-10T02:18:54Z", "event":"ShipTargeted", "TargetLocked":true, "Ship":"adder", "ScanStage":3, "PilotName":"$npc_name_decorate:#name=Doug Quiad;", "PilotName_Localised":"Doug Quiad", "PilotRank":"Master", "ShieldHealth":87.5, "HullHealth":64.0, "Faction":"The Prime Spade", "LegalStatus":"Wanted", "Bounty":92454 }""";
    private const string Commander = """{ "timestamp":"2026-11-10T02:19:14Z", "event":"ShipTargeted", "TargetLocked":true, "Ship":"corsair", "ScanStage":3, "PilotName":"$RolePanel2_unmanned; $cmdr_decorate:#name=CALVIN INSTI;", "PilotName_Localised":"unmanned CMDR CALVIN INSTI", "PilotRank":"Mostly Harmless", "LegalStatus":"Clean" }""";
    private const string Dropped = """{ "timestamp":"2026-11-10T02:20:00Z", "event":"ShipTargeted", "TargetLocked":false }""";

    private static readonly StatusDestination Planet = new(633608311522, 12, "Fixture 3 a");

    private static async Task<string> AskAsync(GameStatus status, params string[] events)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Tester"));

        foreach (var line in events)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            state.Apply(parsed!);
        }

        var settings = TestSurface.For(new MemoryInstall()).Settings;
        var tool = GalaxyCapability.Create(null, () => "Sol", settings, gameStatus: () => status, gameState: () => state)
            .Tools.Single(candidate => candidate.Name == GalaxyCapability.TargetTool);
        var result = await tool.Handler(new ToolArguments(new Dictionary<string, string>()), TestContext.Current.CancellationToken);

        return result.Content;
    }

    [Fact]
    public async Task AScannedNpcShipIsGivenWithPilotRankFactionLegalStatusAndBounty()
    {
        var answer = await AskAsync(new GameStatus(), Npc);

        Assert.Equal(
            "Your locked ship: hull adder; pilot Doug Quiad; rank Master; faction The Prime Spade; legal status Wanted; bounty 92,454 credits.",
            answer);
    }

    [Fact]
    public async Task AShipWithNoBountyDoesNotMentionOne()
    {
        var answer = await AskAsync(new GameStatus(), Npc.Replace(", \"Bounty\":92454", string.Empty, StringComparison.Ordinal));

        Assert.DoesNotContain("bounty", answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AShipHealthIsLeftOut()
    {
        var answer = await AskAsync(new GameStatus(), Npc);

        Assert.DoesNotContain("87", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("shield", answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ACommandersShipIsSaidToBeOneWithItsHullAndNoName()
    {
        var answer = await AskAsync(new GameStatus(), Commander);

        Assert.Equal("Your locked ship is flown by a Commander. Hull corsair. Legal status Clean.", answer);
        Assert.DoesNotContain("CALVIN", answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AShipAndAPlanetAreDescribedShipFirst()
    {
        var answer = await AskAsync(new GameStatus { Destination = Planet }, Npc);

        Assert.StartsWith("Your locked ship: hull adder;", answer, StringComparison.Ordinal);
        Assert.Contains("Fixture 3 a", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADroppedShipIsNoLongerMentioned()
    {
        Assert.Equal("Nothing is targeted.", await AskAsync(new GameStatus(), Npc, Dropped));

        var withPlanet = await AskAsync(new GameStatus { Destination = Planet }, Npc, Dropped);

        Assert.DoesNotContain("locked ship", withPlanet, StringComparison.Ordinal);
    }
}
