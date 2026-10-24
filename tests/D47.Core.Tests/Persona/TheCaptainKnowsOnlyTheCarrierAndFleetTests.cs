using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>The captain is offered no tools and knows the carrier report and the names and hulls of the Commander's ships.</summary>
public class TheCaptainKnowsOnlyTheCarrierAndFleetTests
{
    private const string ShipAiState = "SHIP-AI-LIVE-STATE";

    private static readonly CarrierState Owned = new()
    {
        CallSign = "BNH-T2F",
        Name = "Sacred Fire",
        StarSystem = "Sol",
        FuelLevel = 792,
    };

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static readonly JournalEvent Flying = Parse(
        """{"timestamp":"2026-10-14T10:00:00Z","event":"Loadout","Ship":"cobramkiii","ShipID":7,"ShipName":"Reaper","FuelCapacity":{"Main":16.0,"Reserve":0.5},"CargoCapacity":0,"Modules":[]}""");

    private static readonly JournalEvent Stored = Parse(
        """{"timestamp":"2026-10-14T10:01:00Z","event":"StoredShips","StationName":"Jameson Memorial","MarketID":128666762,"StarSystem":"Shinrarta Dezhra","ShipsHere":[{"ShipID":3,"ShipType":"sidewinder","Name":"Old Faithful","Value":30000,"Hot":false}],"ShipsRemote":[{"ShipID":5,"ShipType":"anaconda","Name":"Big Mama","StarSystem":"Achenar","ShipMarketID":1,"TransferPrice":1000,"TransferTime":100,"Value":1,"Hot":false}]}""");

    private static CommanderGameState Commander(params JournalEvent[] events)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Tester")) { Carrier = Owned };

        foreach (var journalEvent in events)
        {
            state.Apply(journalEvent);
        }

        return state;
    }

    private static async Task<LlmRequest> AskAsync(TurnLoop loop, FakeLlmProvider provider, string input)
    {
        await OnComms.AskAsync(loop, input);

        return provider.LastRequest!;
    }

    private static async Task<LlmRequest> SayAsync(TurnLoop loop, FakeLlmProvider provider, string input)
    {
        await OnComms.SayAsync(loop, input);

        return provider.LastRequest!;
    }

    private static (TurnLoop Loop, FakeLlmProvider Provider) Build(CommanderGameState state)
    {
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta("Aye, Commander."),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed))
        {
            ToolCalls = true,
        };

        var surface = TestSurface.For(new MemoryInstall());

        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider)
        {
            Persona = "You are the ship's AI aboard this vessel.",
            ShipAiNames = () => ["Warden"],
            LiveGameState = () => ShipAiState,
            CommsSelected = () => OnComms.Captain,
        };

        loop.Lines.Add(new CaptainLine(() => state, () => "Sol", () => DateTimeOffset.UnixEpoch));

        return (loop, provider);
    }

    [Fact]
    public async Task ACaptainTurnAdvertisesNoToolsAndTheShipAisStillDoes()
    {
        var (loop, provider) = Build(Commander(Flying, Stored));

        var ship = await AskAsync(loop, provider, "Warden, what do you make of the stars tonight");
        Assert.NotEmpty(ship.Prompt.Tools);

        var captain = await SayAsync(loop, provider, "how's our fuel?");
        Assert.Empty(captain.Prompt.Tools);
    }

    [Fact]
    public async Task ACaptainTurnIsSentTheCarrierAndRosterInPlaceOfTheShipAisState()
    {
        var (loop, provider) = Build(Commander(Flying, Stored));

        var captain = await SayAsync(loop, provider, "how's our fuel?");
        var live = captain.Prompt.LiveGameState;

        Assert.NotNull(live);
        Assert.DoesNotContain(ShipAiState, live, StringComparison.Ordinal);
        Assert.Contains("Fuel 792 t", live, StringComparison.Ordinal);
        Assert.Contains("The Commander owns 3 ships:", live, StringComparison.Ordinal);

        var ship = await AskAsync(loop, provider, "Warden, what do you make of the stars tonight");
        Assert.Equal(ShipAiState, ship.Prompt.LiveGameState);
    }

    [Fact]
    public void TheRosterNamesTheShipFlownAndEveryStoredShipByNameAndHullAlone()
    {
        var state = Commander(Flying, Stored);
        var roster = CaptainLine.Roster(state);

        Assert.Equal(
            $"""
             The Commander owns 3 ships:
             - Reaper ({state.FlownShip.TypeSaid}), flying now
             - {state.Fleet.Ships[0].Describe()}
             - {state.Fleet.Ships[1].Describe()}
             """.ReplaceLineEndings("\n"),
            roster);

        Assert.StartsWith("Old Faithful (", state.Fleet.Ships[0].Describe(), StringComparison.Ordinal);
        Assert.DoesNotContain("Shinrarta", roster, StringComparison.Ordinal);
        Assert.DoesNotContain("Achenar", roster, StringComparison.Ordinal);
    }

    [Fact]
    public void WithNoFleetSnapshotTheRosterSaysTheRestIsNotKnown()
    {
        var state = Commander(Flying);

        Assert.Equal(
            $"The Commander is flying Reaper ({state.FlownShip.TypeSaid}). Their other ships are not known.",
            CaptainLine.Roster(state));
    }

    [Fact]
    public void WithNoShipFlownKnownTheRosterCountsOnlyTheStoredShips()
    {
        var state = Commander(Stored);

        Assert.Equal(
            $"""
             The Commander has 2 stored ships:
             - {state.Fleet.Ships[0].Describe()}
             - {state.Fleet.Ships[1].Describe()}
             The ship they are flying is not known.
             """.ReplaceLineEndings("\n"),
            CaptainLine.Roster(state));
    }

    [Fact]
    public void TheBriefNamesNoToolAndSendsTheRestToTheShipsAi()
    {
        var brief = CaptainLine.Brief(Owned);

        Assert.DoesNotContain("describe_carrier", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("galaxy tools", brief, StringComparison.Ordinal);
        Assert.Contains("each one's name and hull", brief, StringComparison.Ordinal);
        Assert.Contains("leave it to the core", brief, StringComparison.Ordinal);
    }
}
