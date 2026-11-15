using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class TheCrewRosterIsFoldedFromEveryJournalTests
{
    private static ShipCrew Fold(params string[][] journals)
    {
        var install = new MemoryInstall();
        var files = new List<string>();

        for (var index = 0; index < journals.Length; index++)
        {
            var path = Path.Combine(install.Root, $"Journal.2026-10-{index + 1:00}T090000.01.log");
            install.Files.WriteLines(path, journals[index]);
            files.Add(path);
        }

        var rosters = CrewBackfill.FromHistory(install.Files, files, NullLogger.Instance, TestContext.Current.CancellationToken);

        return rosters.GetValueOrDefault("F1", ShipCrew.Empty);
    }

    private static string Load(int shipId, string type = "python", string name = "") =>
        $$"""{"timestamp":"2026-10-01T09:00:00Z","event":"Loadout","Ship":"{{type}}","ShipID":{{shipId}},"ShipName":"{{name}}","ShipIdent":"","Modules":[]}""";

    private const string LoadGame = """{"timestamp":"2026-10-01T09:00:00Z","event":"LoadGame","FID":"F1","ShipID":62}""";

    private const string Hire = """{"timestamp":"2026-10-01T09:01:00Z","event":"CrewHire","Name":"Vance Ilo","CrewID":7,"CombatRank":4}""";

    private const string Assign = """{"timestamp":"2026-10-01T09:02:00Z","event":"CrewAssign","Name":"Vance Ilo","CrewID":7,"Role":"Active"}""";

    private static string Swap(int shipId) =>
        $$"""{"timestamp":"2026-10-01T10:00:00Z","event":"ShipyardSwap","ShipType":"python","ShipID":{{shipId}},"StoreOldShip":"x","StoreShipID":1}""";

    [Fact]
    public void APilotHiredInAnOldFileWithNothingAfterIsOnTheRosterAndPosted()
    {
        var crew = Fold(
            [LoadGame, Load(62), Hire, Assign],
            [LoadGame, Load(62)],
            [LoadGame, Load(62)]);

        var member = Assert.Single(crew.Members);
        Assert.True(member.Active);
        Assert.Equal(62, member.PostedShipId);
        Assert.False(member.HullLeft);
        Assert.Equal(4, member.CombatRank);
    }

    [Fact]
    public void ThePilotIsFoundFurtherBackThanTheFleetLookback()
    {
        var journals = new List<string[]> { new[] { LoadGame, Load(62), Hire, Assign } };
        journals.AddRange(Enumerable.Range(0, 30).Select(_ => new[] { LoadGame }));

        Assert.Single(Fold([.. journals]).Members);
    }

    [Fact]
    public void SwappingAwayAndBackLeavesThePilotPostedToTheirHullWithTheFlagSet()
    {
        var crew = Fold([LoadGame, Load(62), Hire, Assign, Swap(37), Load(37), Swap(62), Load(62)]);

        var member = Assert.Single(crew.Members);
        Assert.Equal(62, member.PostedShipId);
        Assert.True(member.HullLeft);
    }

    [Fact]
    public void AssigningAgainOnAnotherHullMovesThePostingAndClearsTheFlag()
    {
        var crew = Fold([LoadGame, Load(5), Hire, Assign, Swap(14), Load(14), Assign]);

        var member = Assert.Single(crew.Members);
        Assert.Equal(14, member.PostedShipId);
        Assert.False(member.HullLeft);
    }

    [Fact]
    public void TwoHullsOfTheSameTypeAreToldApartByShipId()
    {
        var first = Fold([LoadGame, Load(5, name: "Same"), Hire, Assign]);
        var second = Fold([LoadGame, Load(9, name: "Same"), Hire, Assign]);

        Assert.Equal(first.Members[0].PostedTo, second.Members[0].PostedTo);
        Assert.NotEqual(first.Members[0].PostedShipId, second.Members[0].PostedShipId);
    }

    [Fact]
    public void HiringThenFiringLeavesTheRosterEmpty()
    {
        var fire = """{"timestamp":"2026-10-01T09:05:00Z","event":"CrewFire","Name":"Vance Ilo","CrewID":7}""";

        Assert.False(Fold([LoadGame, Load(62), Hire, Assign, fire]).Any);
    }

    [Fact]
    public void ALiveFireRemovesABackfilledPilotAndALiveAssignMovesThePosting()
    {
        var restored = Fold([LoadGame, Load(62), Hire, Assign]);

        var fired = restored.Apply(Parse("""{"timestamp":"2026-10-02T09:05:00Z","event":"CrewFire","Name":"Vance Ilo","CrewID":7}"""));
        Assert.False(fired.Any);

        var moved = restored.Apply(Parse(Assign), "Other", 14);
        Assert.Equal(14, Assert.Single(moved.Members).PostedShipId);
    }

    [Fact]
    public void ABackfilledRosterLaidUnderTheLiveOneKeepsEachPilotOnce()
    {
        var restored = Fold([LoadGame, Load(62), Hire, Assign]);
        var live = ShipCrew.Empty.Apply(Parse(Hire));

        Assert.Single(restored.With(live).Members);
    }

    private static JournalEvent Parse(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
