using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>The bias list carries the names of the game actions, after the journal's names.</summary>
public class TheCommandWordsSitBetweenTheJournalAndTheEngineersTests
{
    private static readonly string[] Named =
    [
        "landing gear", "hardpoints", "cargo scoop", "heat sink", "frame shift drive",
        "silent running", "flight assist", "night vision", "supercruise",
    ];

    private static CommanderGameState StateFrom(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in lines.Prepend("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static CommanderGameState Docked() => StateFrom(
        """{"timestamp":"3311-01-01T00:01:00Z","event":"Docked","StarSystem":"Shinrarta Dezhra","StationName":"Jameson Memorial"}""");

    [Fact]
    public void TheShipsControlsAreNamed()
    {
        var nouns = ProperNouns.From(Docked());

        Assert.All(Named, name => Assert.Contains(name, nouns));
    }

    [Fact]
    public void JournalNamesThenCommandWordsThenEngineers()
    {
        var nouns = ProperNouns.From(Docked()).ToList();
        var engineers = EngineerDirectory.All.Select(engineer => engineer.Name).ToHashSet(StringComparer.Ordinal);

        var lastJournal = nouns.LastIndexOf("Jameson Memorial");
        var firstCommand = nouns.IndexOf("landing gear");
        var lastCommand = Named.Max(nouns.IndexOf);
        var firstEngineer = nouns.FindIndex(engineers.Contains);

        Assert.Equal(1, lastJournal);
        Assert.True(lastJournal < firstCommand, string.Join(", ", nouns));
        Assert.True(lastCommand < firstEngineer, string.Join(", ", nouns));
    }

    [Fact]
    public void TheCommandWordsTakeNoMoreThanTheirShare()
    {
        var nouns = ProperNouns.From(Docked());
        var engineers = EngineerDirectory.All.Select(engineer => engineer.Name).ToHashSet(StringComparer.Ordinal);

        var commands = nouns.Skip(2).TakeWhile(name => !engineers.Contains(name)).Count();

        Assert.InRange(commands, Named.Length, ProperNouns.CommandShare);
    }

    /// <summary>A crowded journal still fills its own share of the list.</summary>
    [Fact]
    public void ACrowdedJournalStillHasItsOwnSlots()
    {
        var ships = string.Join(
            ",",
            Enumerable.Range(0, 120).Select(number =>
                $$"""{"ShipID":{{number}},"ShipType":"Krait","Name":"Wandering Albatross {{number}}","StarSystem":"Sol","Value":1}"""));

        var nouns = ProperNouns.From(StateFrom(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"Location","StarSystem":"Sol"}""",
            $$"""{"timestamp":"3311-01-01T00:02:00Z","event":"StoredShips","StarSystem":"Sol","StationName":"Abraham Lincoln","ShipsHere":[{{ships}}],"ShipsRemote":[]}"""));

        Assert.Equal(
            ProperNouns.Limit - ProperNouns.ShippedShare - ProperNouns.CommandShare,
            nouns.TakeWhile(name => name != "landing gear").Count());
        Assert.Contains("heat sink", nouns);
    }
}
