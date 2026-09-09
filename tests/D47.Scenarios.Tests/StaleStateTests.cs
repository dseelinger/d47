using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Scenarios.Tests;

/// <summary>The live block outranks the transcript for the facts it states.</summary>
public class StaleStateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private const string Commander =
        """{ "timestamp":"2026-09-06T23:30:00Z", "event":"Commander", "FID":"F9000001", "Name":"Scenario" }""";

    private const string LoadGame =
        """{ "timestamp":"2026-09-06T23:30:01Z", "event":"LoadGame", "FID":"F9000001", "Commander":"Scenario", "Ship":"Asp Explorer", "ShipID":1, "GameMode":"Open", "Credits":100000 }""";

    private const string Docked =
        """{ "timestamp":"2026-09-06T23:34:15Z", "event":"Docked", "StarSystem":"Alrai Sector KC-U b3-4", "StationName":"Zeppelin Depot", "StationType":"Outpost", "MarketID":128000001 }""";

    private const string Undocked =
        """{ "timestamp":"2026-09-06T23:40:08Z", "event":"Undocked", "StationName":"Zeppelin Depot" }""";

    private static FakeLlmProvider Answering() =>
        new(
            new LlmStreamEvent.TextDelta("Clear of the pad, Commander."),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed))
        {
            ToolCalls = true,
            WebSearch = false,
        };

    /// <summary>The block an earlier turn carried, built by folding the journal up to the dock.</summary>
    private static string DockedBlock()
    {
        using var world = new ScenarioWorld();
        world.ApplyJournal([Commander, LoadGame, Docked]);

        var block = world.LiveGameState();

        // The control for everything below: if the earlier block never named the station, a pass here would
        // be a negative assertion holding because the stale claim was never there.
        Assert.Contains("Zeppelin Depot", block);

        return block!;
    }

    [Fact]
    public async Task ATurnWhoseHistoryStillSaysDockedIsToldOtherwiseByTheLiveBlock()
    {
        var stale = DockedBlock();

        var scenario = new Scenario
        {
            Id = "stale-state/undocked-a-minute-ago",
            Note = "#364: docked, undocked, and then asked about now",
            Utterance = "Where are we right now, and are we still on the pad?",
            Journal = [Commander, LoadGame, Docked, Undocked],
            History =
            [
                new ConversationMessage(ConversationRole.User, $"How long have we been here?\n\n{stale}"),
                new ConversationMessage(
                    ConversationRole.Assistant,
                    "We are docked at Zeppelin Depot, Commander. The crane has had us six minutes."),
            ],
        };

        var trace = await ScenarioRunner.RunAsync(scenario, Answering(), PersonaCatalog.Warden, cancellationToken: Token);

        var prompt = Assert.Single(trace.Prompts);

        // The stale claim is in the request, which is the reason for the case: this is the turn the Commander
        // actually had, not a tidied one.
        Assert.Contains("Zeppelin Depot", TurnTrace.Render(prompt), StringComparison.Ordinal);

        // And the block describing now names no station and says outright that the ship is not docked, so the
        // older text has something to lose against.
        Assert.NotNull(prompt.LiveGameState);
        Assert.Contains("not docked", prompt.LiveGameState);
        Assert.DoesNotContain("Zeppelin Depot", prompt.LiveGameState);
        Assert.Contains("earlier in this conversation", prompt.LiveGameState);
    }

    /// <summary>The same turn before the game has said anything about where the Commander is.</summary>
    [Fact]
    public async Task ATurnWithNoLocationYetClaimsNeither()
    {
        var scenario = new Scenario
        {
            Id = "stale-state/nothing-read-yet",
            Utterance = "Where are we right now, and are we still on the pad?",
            Journal = [Commander, LoadGame],
        };

        var trace = await ScenarioRunner.RunAsync(scenario, Answering(), PersonaCatalog.Warden, cancellationToken: Token);

        var prompt = Assert.Single(trace.Prompts);

        Assert.DoesNotContain("docked", prompt.LiveGameState ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
