using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The readings an engineer prerequisite is decided from.</summary>
public class WhatAnEngineerAsksForIsReadFromTheJournalTests
{
    private static readonly DateTimeOffset First = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AStatisticIsReadBySectionAndKey()
    {
        var statistics = CareerStatistics.Empty.Apply(Event(
            """{"timestamp":"2026-01-01T00:00:00Z","event":"Statistics","Exploration":{"Greatest_Distance_From_Start":4557.7227735854}}"""));

        Assert.Equal(First, statistics.TakenAt);
        Assert.Equal(4557.7227735854, statistics.Read("Exploration.Greatest_Distance_From_Start"));
        Assert.Null(statistics.Read("Exploration.No_Such_Key"));
        Assert.Null(statistics.Read("Exploration"));
    }

    [Fact]
    public void ASuperpowerAnswersFromTheReputationEvent()
    {
        var reputation = ReputationState.Empty.Apply(Event(
            """{"timestamp":"2026-01-01T00:00:00Z","event":"Reputation","Empire":28.3479,"Federation":25.2108,"Independent":0.0,"Alliance":25.0098}"""));

        Assert.Equal(new FactionReading(25.0098, First), reputation.Reading("Alliance"));
    }

    [Fact]
    public void AFactionKeepsItsReadingThroughAJumpThatDoesNotListIt()
    {
        var reputation = ReputationState.Empty
            .Apply(Event(
                """{"timestamp":"2026-01-01T00:00:00Z","event":"FSDJump","StarSystem":"Alpha","Factions":[{"Name":"Eurybia Blue Mafia","MyReputation":15.6616}]}"""))
            .Apply(Event(
                """{"timestamp":"2026-01-01T01:00:00Z","event":"FSDJump","StarSystem":"Beta","Factions":[{"Name":"Someone Else","MyReputation":-3.0}]}"""));

        Assert.Equal(new FactionReading(15.6616, First), reputation.Reading("Eurybia Blue Mafia"));
        Assert.Equal(-3.0, reputation.Reading("Someone Else")?.MyReputation);
        Assert.Null(reputation.Reading("Nobody Met"));
    }

    [Fact]
    public void AContributionTotalIsTakenAsEliteWritesIt()
    {
        var contributions = EngineerContributions.Empty
            .Apply(Event(
                """{"timestamp":"2026-01-01T00:00:00Z","event":"EngineerContribution","Engineer":"Liz Ryder","EngineerID":300090,"Type":"Commodity","Commodity":"kamitracigars","Quantity":15,"TotalQuantity":15}"""))
            .Apply(Event(
                """{"timestamp":"2026-01-01T01:00:00Z","event":"EngineerContribution","Engineer":"Liz Ryder","EngineerID":300090,"Type":"Commodity","Commodity":"kamitracigars","Quantity":15,"TotalQuantity":30}"""))
            .Apply(Event(
                """{"timestamp":"2026-01-01T02:00:00Z","event":"EngineerContribution","Engineer":"Liz Ryder","EngineerID":300090,"Type":"Bond","Quantity":500000,"TotalQuantity":500000}"""));

        Assert.Equal(30, contributions.Total(300090, "Commodity", "kamitracigars"));
        Assert.Equal(30, contributions.Total(300090, "commodity", "KamitraCigars"));
        Assert.Equal(500000, contributions.Total(300090, "Bond", null));
        Assert.Null(contributions.Total(300090, "Material", "kamitracigars"));
    }

    [Fact]
    public void ANewCommanderClearsOnlyTheCommanderItNames()
    {
        var store = new GameStateStore();

        foreach (var (fid, name) in new[] { ("F1", "Alice"), ("F2", "Bob") })
        {
            store.Apply(Event($$"""{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"{{fid}}","Name":"{{name}}"}"""));
            store.Apply(Event("""{"timestamp":"2026-01-01T00:00:01Z","event":"FSDJump","StarSystem":"Alpha","Factions":[{"Name":"Eurybia Blue Mafia","MyReputation":15.6616}]}"""));
            store.Apply(Event("""{"timestamp":"2026-01-01T00:00:02Z","event":"EngineerContribution","EngineerID":300090,"Type":"Commodity","Commodity":"kamitracigars","TotalQuantity":15}"""));
        }

        store.Apply(Event("""{"timestamp":"2026-01-01T00:01:00Z","event":"Commander","FID":"F1","Name":"Alice"}"""));
        store.Apply(Event("""{"timestamp":"2026-01-01T00:02:00Z","event":"NewCommander","FID":"F2","Name":"Bob","Package":"Default3"}"""));

        var alice = store.All.Single(state => state.Identity.FrontierId == "F1");
        var bob = store.All.Single(state => state.Identity.FrontierId == "F2");

        Assert.Same(alice, store.Active);
        Assert.NotNull(alice.Reputation.Reading("Eurybia Blue Mafia"));
        Assert.Equal(15, alice.Contributions.Total(300090, "Commodity", "kamitracigars"));
        Assert.Null(bob.Reputation.Reading("Eurybia Blue Mafia"));
        Assert.False(bob.Contributions.IsKnown);
    }

    [Fact]
    public void ANewCommanderNotYetMetIsAddedWithoutBecomingActive()
    {
        var store = new GameStateStore();
        var switches = 0;
        store.CommanderChanged += _ => switches++;

        store.Apply(Event("""{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Alice"}"""));
        store.Apply(Event("""{"timestamp":"2026-01-01T00:01:00Z","event":"NewCommander","FID":"F9","Name":"Carol"}"""));

        Assert.Equal("F1", store.Active?.Identity.FrontierId);
        Assert.Equal(1, switches);
        Assert.Contains(store.All, state => state.Identity is { FrontierId: "F9", Name: "Carol" });
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
