using System.Globalization;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>get_mission_board lists the whole board when the model sets all, and reads three otherwise.</summary>
public class TheModelCanListEveryMissionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T10:00:00Z", CultureInfo.InvariantCulture);

    [Fact]
    public async Task TheSeventhOfElevenMissionsIsAnsweredFromTheToolResult()
    {
        var registry = CapabilityRegistry.Build([MissionsCapability.Create(ElevenMissions, () => Now)]);

        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Calling("call_1", MissionsCapability.Tool, """{"all":true}"""),
            RoundScriptedLlmProvider.Saying("For Polymer Traders."));

        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: new InstantClock());

        await foreach (var _ in loop.RunAsync("who is the Polymers delivery for", cancellationToken: TestContext.Current.CancellationToken))
        {
        }

        var result = provider.Requests[1].Prompt.History
            .SelectMany(message => message.Content)
            .OfType<ConversationContent.ToolResult>()
            .Single();

        var lines = result.Content.Split('\n');
        var line = Assert.Single(lines, text => text.Contains("Polymers Run", StringComparison.Ordinal));

        Assert.StartsWith("7. ", line, StringComparison.Ordinal);
        Assert.Contains("Polymer Traders", line, StringComparison.Ordinal);
        Assert.Contains("Dock 7, System 7", line, StringComparison.Ordinal);
        Assert.Equal(12, lines.Length);
    }

    [Fact]
    public void WithoutAllTheBoardNamesThreeAndCountsTheRest()
    {
        var said = MissionsCapability.Describe(ElevenMissions(), Now);

        Assert.Contains("And eight more.", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Polymers Run", said, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissionWithNoAcceptReadGivesItsTitleAndSaysNoDetailIsOnRecord()
    {
        var store = new GameStateStore();
        store.Apply(Event("""{ "timestamp":"2026-09-30T09:00:00Z", "event":"LoadGame", "FID":"F100", "Commander":"Fixture" }"""));
        store.Apply(Event(
            """{ "timestamp":"2026-09-30T09:59:00Z", "event":"Missions", "Active":[ { "MissionID":9, "Name":"Mission_Courier", "PassengerMission":false, "Expires":3600 } ], "Failed":[], "Complete":[] }"""));

        var said = MissionsCapability.Describe(store.Active, Now, all: true);

        Assert.Equal("You have one mission.\n1. Mission_Courier, no detail on record.", said);
    }

    private static CommanderGameState ElevenMissions()
    {
        var store = new GameStateStore();
        store.Apply(Event("""{ "timestamp":"2026-09-30T09:00:00Z", "event":"LoadGame", "FID":"F100", "Commander":"Fixture" }"""));

        for (var id = 1; id <= 11; id++)
        {
            var title = id == 7 ? "Polymers Run" : $"Job {id}";
            var faction = id == 7 ? "Polymer Traders" : "Party of Yoru";
            var cargo = id == 7
                ? """, "Commodity":"$Polymers_Name;", "Commodity_Localised":"Polymers", "Count":20"""
                : string.Empty;
            var expiry = $"2026-09-30T{10 + id:00}:00:00Z";

            store.Apply(Event(
                $$"""{ "timestamp":"2026-09-30T09:30:00Z", "event":"MissionAccepted", "Faction":"{{faction}}", "Name":"Mission_Delivery", "LocalisedName":"{{title}}", "DestinationSystem":"System {{id}}", "DestinationStation":"Dock {{id}}", "Expiry":"{{expiry}}", "Reward":{{id * 1000}}, "MissionID":{{id}}{{cargo}} }"""));
        }

        store.Apply(Event("""{ "timestamp":"2026-09-30T09:31:00Z", "event":"CargoDepot", "MissionID":7, "UpdateType":"Deliver", "ItemsCollected":20, "ItemsDelivered":12, "TotalItemsToDeliver":20 }"""));

        return store.Active!;
    }

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var journalEvent));
        return journalEvent!;
    }
}
