using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A board objective that names one of the Commander's ships by its own name boards that ship's hull.</summary>
public sealed class AShipNamedByItsOwnNameBoardsItsHullTests
{
    private static string Beats(string ship) => $$"""
        {"opening": "Again.", "reply": "Here.", "beats": [
          {"title": "Range", "function": "setup", "kind": "board", "ship": "{{ship}}", "line": "Sign for it."},
          {"title": "The Bounties", "function": "midpoint", "kind": "bounty", "count": 3, "line": "Paid."},
          {"title": "The Ore", "function": "finale", "kind": "mine", "count": 20, "line": "Refined."}
        ]}
        """;

    private static readonly FleetRegistry Fleet = new()
    {
        TakenAt = DateTimeOffset.Parse("2026-10-07T19:10:50Z"),
        SnapshotSystem = "LTT 7786",
        Ships = [new StoredShip(11, "Anaconda", "Flamebrand", "LTT 7786")],
    };

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed), json);
        return parsed!;
    }

    private static (AdventureOutcome Result, RoundScriptedLlmProvider Provider) Generate(params string[] answers)
    {
        var store = new GameStateStore { RestoreFleet = fid => fid == "F735466" ? Fleet : null };

        store.Apply(Event("""{ "timestamp":"2026-10-12T09:00:00Z", "event":"Commander", "FID":"F100", "Name":"TALYN MERIDAN" }"""));
        store.Apply(Event("""{ "timestamp":"2026-10-12T09:00:01Z", "event":"Location", "StarSystem":"Oppi", "SystemAddress":3382387380970, "StarPos":[3.68750,-44.62500,131.00000], "Docked":false }"""));
        store.Pick(new CommanderIdentity("F735466", "JOHN DEPARAGON"));

        var provider = new RoundScriptedLlmProvider([RoundScriptedLlmProvider.Saying(Spine), .. answers.Select(answer => RoundScriptedLlmProvider.Saying(answer))]);

        var generator = new AdventureGenerator(
            () => provider, () => null, () => "You are the ship.", () => "core", () => null,
            () => store.Active, () => new AdventureGeneratorTests.Galaxy(), () => null, null, null, NullLogger.Instance);

        var result = generator.GenerateAsync(
            new AdventureAsk(AdventureReach.Session, Story: new AdventureStory(Id, Card.Title, Card.Describe(), Secret.Secret, 1, 10, 5)),
            Now,
            CancellationToken.None).GetAwaiter().GetResult();

        return (result, provider);
    }

    [Fact]
    public void AnOwnedShipsNameBoardsItsHullOnTheFirstDraft()
    {
        var (result, provider) = Generate(Beats("flamebrand "));

        Assert.True(result.Succeeded, result.Refusal);
        Assert.Equal(2, provider.CallCount);

        var board = result.Draft!.Beats[0].Trigger;
        Assert.Equal(TriggerKind.Board, board.Kind);
        Assert.Equal("anaconda", board.ShipType);
    }

    [Fact]
    public void ANameNoShipCarriesIsRefusedWithAHullTypeToGiveInstead()
    {
        var (result, provider) = Generate(Beats("Emberwake"), Beats("Emberwake"));

        Assert.False(result.Succeeded);
        Assert.Contains("\"Emberwake\" that d47 has no name for", result.Refusal);
        Assert.Contains("as a hull type such as Anaconda", provider.Requests[2].Prompt.History[0].Text);
    }

    [Fact]
    public void TheWriterIsToldToGiveAHullTypeAndNotTheShipsName()
    {
        var (_, provider) = Generate(Beats("Anaconda"));

        Assert.Contains("its hull type, one of ", provider.Requests[1].Prompt.History[0].Text);
    }
}
