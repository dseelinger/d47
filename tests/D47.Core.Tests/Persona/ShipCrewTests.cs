using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>The pilots the Commander actually hired.</summary>
public class ShipCrewTests
{
    private static JournalEvent Event(string kind, params (string Key, object? Value)[] fields)
    {
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-02-10T09:00:00Z",
            ["event"] = kind,
        };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static ShipCrew Hire(ShipCrew crew, string name, long id, int rank = 3) =>
        crew.Apply(Event("CrewHire", ("Name", name), ("CrewID", id), ("CombatRank", rank)));

    [Fact]
    public void HiringPutsSomebodyOnTheBooks()
    {
        var crew = Hire(ShipCrew.Empty, "Vance Ilo", 1, 4);

        var member = Assert.Single(crew.Members);
        Assert.Equal("Vance Ilo", member.Name);
        Assert.Equal(4, member.CombatRank);
        Assert.False(member.Active);
        Assert.Null(crew.Active);
    }

    [Fact]
    public void HiringTheSameIdTwiceDoesNotDoubleThemUp()
    {
        // Elite writes CrewHire again on some session restores, and a roster that doubled would report two of
        // the same person.
        var crew = Hire(Hire(ShipCrew.Empty, "Vance", 1), "Vance", 1);

        Assert.Single(crew.Members);
    }

    [Fact]
    public void OnlyOnePilotIsEverOnDuty()
    {
        // Elite allows exactly one in the fighter bay, which is why Active is a flag on the member rather
        // than a field on the roster.
        var crew = Hire(Hire(ShipCrew.Empty, "Vance", 1), "Ilse", 2);

        crew = crew.Apply(Event("CrewAssign", ("Name", "Vance"), ("CrewID", 1L), ("Role", "Active")));
        Assert.Equal("Vance", crew.Active?.Name);

        crew = crew.Apply(Event("CrewAssign", ("Name", "Ilse"), ("CrewID", 2L), ("Role", "Active")));
        Assert.Equal("Ilse", crew.Active?.Name);
        Assert.Single(crew.Members, m => m.Active);
    }

    [Fact]
    public void ShoreLeaveTakesThemOffDutyWithoutFiringThem()
    {
        var crew = Hire(ShipCrew.Empty, "Vance", 1)
            .Apply(Event("CrewAssign", ("Name", "Vance"), ("CrewID", 1L), ("Role", "Active")))
            .Apply(Event("CrewAssign", ("Name", "Vance"), ("CrewID", 1L), ("Role", "OnShoreLeave")));

        Assert.Single(crew.Members);
        Assert.Null(crew.Active);
    }

    [Fact]
    public void FiringTakesThemOff()
    {
        var crew = Hire(Hire(ShipCrew.Empty, "Vance", 1), "Ilse", 2)
            .Apply(Event("CrewFire", ("Name", "Vance"), ("CrewID", 1L)));

        Assert.Equal("Ilse", Assert.Single(crew.Members).Name);
    }

    [Fact]
    public void ThePostingIsDerivedFromTheHullTheyWereAssignedAboard()
    {
        // Elite's CrewAssign names no ship — the hired crew are a pool and the active one flies in whatever
        // hull the Commander is in.
        var crew = Hire(ShipCrew.Empty, "Vance", 1)
            .Apply(Event("CrewAssign", ("Name", "Vance"), ("CrewID", 1L), ("Role", "Active")), "Long Way Home");

        Assert.Equal("Long Way Home", crew.Active?.PostedTo);
        Assert.Single(crew.For("Long Way Home"));
        Assert.Empty(crew.For("Some Other Ship"));
        Assert.Empty(crew.For(null));
    }

    [Fact]
    public void ARankChangeUpdatesThemInPlace()
    {
        var crew = Hire(ShipCrew.Empty, "Vance", 1, 3)
            .Apply(Event("NpcCrewRank", ("NpcCrewName", "Vance"), ("NpcCrewId", 1L), ("RankCombat", 7)));

        Assert.Equal(7, Assert.Single(crew.Members).CombatRank);
    }

    [Fact]
    public void ARankChangeLeavesEveryoneElseAsTheyWere()
    {
        var crew = Hire(Hire(ShipCrew.Empty, "Vance", 1, 2), "Ilse", 2, 4)
            .Apply(Event("NpcCrewRank", ("NpcCrewName", "Vance"), ("NpcCrewId", 1L), ("RankCombat", 3)));

        Assert.Equal([3, 4], crew.Members.Select(m => m.CombatRank));
    }

    [Fact]
    public void ARankChangeForSomebodyNotOnTheRosterChangesNothing()
    {
        var hired = Hire(ShipCrew.Empty, "Vance", 1, 2);

        var crew = hired.Apply(Event("NpcCrewRank", ("NpcCrewName", "Nobody"), ("NpcCrewId", 99L), ("RankCombat", 5)));

        Assert.Equal(hired.Members, crew.Members);
    }

    [Fact]
    public void ARankAboveEliteIsUnknown()
    {
        var crew = Hire(ShipCrew.Empty, "Vance", 1, 9);

        Assert.Null(Assert.Single(crew.Members).CombatRank);
    }

    [Fact]
    public void ARankIsSaidWithItsNumberAndNameOutOfEight()
    {
        var member = Assert.Single(Hire(ShipCrew.Empty, "Ilse Voss", 7, 2).Members);

        Assert.Equal("Novice", member.CombatRankName);
        Assert.Equal("Ilse Voss (Combat Rank 2 of 8, Novice)", member.Describe());
        Assert.Contains("Your combat rating is Combat Rank 2 of 8, Novice.", D47.Core.Persona.CrewAddressing.Brief(member, null));
        Assert.Equal("Elite", new CrewMember("Ed", 2, 8).CombatRankName);
    }

    [Fact]
    public void AnEventMissingItsFieldsIsSkippedRatherThanFatal()
    {
 // A schema change is a morning's work, not an outage.
        var crew = ShipCrew.Empty.Apply(Event("CrewHire", ("Name", "Vance")));

        Assert.Empty(crew.Members);
    }
}

/// <summary>A hired pilot's brief.</summary>
public class CrewAddressingTests
{
    [Fact]
    public void ACrewMemberIsBriefedAsAPersonRatherThanAsACore()
    {
        // The crew are human pilots hired at a station.
        var brief = CrewAddressing.Brief(new CrewMember("Vance", 1, 4, Active: true), "Long Way Home");

        Assert.Contains("You are Vance", brief, StringComparison.Ordinal);
        Assert.Contains("human being", brief, StringComparison.Ordinal);
        Assert.Contains("Long Way Home", brief, StringComparison.Ordinal);
        Assert.Contains("Combat Rank 4 of 8, Expert", brief, StringComparison.Ordinal);

        // No Guardian material anywhere in it.
        Assert.DoesNotContain("Guardian", brief, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Directive 47", brief, StringComparison.Ordinal);
    }

    [Fact]
    public void ABriefedCrewMemberIsToldTheyHaveNoToolsAndNoDatabase()
    {
        // Anti-invention reaches them too.
        var brief = CrewAddressing.Brief(new CrewMember("Vance", 1), null);

        Assert.Contains("no tools", brief, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("say so plainly", brief, StringComparison.OrdinalIgnoreCase);
    }
}
