using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>
/// The pilots the Commander actually hired (list.md Phase 11, "Ship Crew").
/// </summary>
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

    private static ShipCrew Hire(ShipCrew crew, string name, long id, string rank = "Competent") =>
        crew.Apply(Event("CrewHire", ("Name", name), ("CrewID", id), ("CombatRank", rank)));

    [Fact]
    public void HiringPutsSomebodyOnTheBooks()
    {
        var crew = Hire(ShipCrew.Empty, "Vance Ilo", 1, "Expert");

        var member = Assert.Single(crew.Members);
        Assert.Equal("Vance Ilo", member.Name);
        Assert.Equal("Expert", member.CombatRank);
        Assert.False(member.Active);
        Assert.Null(crew.Active);
    }

    [Fact]
    public void HiringTheSameIdTwiceDoesNotDoubleThemUp()
    {
        // Elite writes CrewHire again on some session restores, and a roster that doubled would
        // report two of the same person.
        var crew = Hire(Hire(ShipCrew.Empty, "Vance", 1), "Vance", 1);

        Assert.Single(crew.Members);
    }

    [Fact]
    public void OnlyOnePilotIsEverOnDuty()
    {
        // Elite allows exactly one in the fighter bay, which is why Active is a flag on the
        // member rather than a field on the roster.
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
        // Elite's CrewAssign names no ship — the hired crew are a pool and the active one flies
        // in whatever hull the Commander is in. This is as far as "per-ship rosters" can honestly
        // go, and a ship d47 saw no assignment aboard reports nobody rather than everybody.
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
        var crew = Hire(ShipCrew.Empty, "Vance", 1, "Competent")
            .Apply(Event("NpcCrewRank", ("Name", "Vance"), ("CrewID", 1L), ("RankCombat", "Deadly")));

        Assert.Equal("Deadly", Assert.Single(crew.Members).CombatRank);
    }

    [Fact]
    public void AnEventMissingItsFieldsIsSkippedRatherThanFatal()
    {
        // A schema change is a morning's work, not an outage (list.md Phase 2).
        var crew = ShipCrew.Empty.Apply(Event("CrewHire", ("Name", "Vance")));

        Assert.Empty(crew.Members);
    }
}

/// <summary>
/// Working out who the Commander was talking to (list.md Phase 11, "Crew is addressable").
/// </summary>
public class CrewAddressingTests
{
    private static ShipCrew Crew(params string[] names) => new()
    {
        Members = [.. names.Select((name, index) => new CrewMember(name, index + 1))],
    };

    [Fact]
    public void AnOpeningNameHandsTheTurnToThatPerson()
    {
        var addressed = CrewAddressing.Match("Vance, what's the fighter looking like?", Crew("Vance", "Ilse"));

        Assert.NotNull(addressed);
        Assert.Equal("Vance", addressed.Member.Name);
        Assert.Equal("what's the fighter looking like?", addressed.Question);
    }

    [Fact]
    public void ANameInTheMiddleIsAQuestionForTheShipsAiAboutThem()
    {
        // A real distinction, and the reason this is not a contains check.
        Assert.Null(CrewAddressing.Match("what does Vance think of the fighter", Crew("Vance")));
    }

    [Fact]
    public void ABareNameIsStillATurn()
    {
        // It is how you get somebody's attention.
        var addressed = CrewAddressing.Match("Vance", Crew("Vance"));

        Assert.NotNull(addressed);
        Assert.Equal(string.Empty, addressed.Question);
    }

    [Fact]
    public void AShortNameDoesNotClaimEveryWordStartingWithIt()
    {
        // Without a word boundary, a crew member called "Al" takes every turn beginning
        // "always".
        Assert.Null(CrewAddressing.Match("always plot the shortest route", Crew("Al")));
        Assert.NotNull(CrewAddressing.Match("Al, status", Crew("Al")));
    }

    [Fact]
    public void TheLongerNameWinsWhenBothWouldMatch()
    {
        var addressed = CrewAddressing.Match("Vance Ilo, report", Crew("Vance", "Vance Ilo"));

        Assert.Equal("Vance Ilo", addressed?.Member.Name);
    }

    [Fact]
    public void WithNoCrewNothingIsEverAddressedToThem()
    {
        Assert.Null(CrewAddressing.Match("Vance, status", ShipCrew.Empty));
        Assert.Null(CrewAddressing.Match(null, Crew("Vance")));
        Assert.Null(CrewAddressing.Match("   ", Crew("Vance")));
    }

    [Fact]
    public void ACrewMemberIsBriefedAsAPersonRatherThanAsACore()
    {
        // The crew are human pilots hired at a station. Handing one of them a Guardian persona
        // would put a core in two places at once, which is the one thing the isolation model
        // cannot survive.
        var brief = CrewAddressing.Brief(new CrewMember("Vance", 1, "Expert", Active: true), "Long Way Home");

        Assert.Contains("You are Vance", brief, StringComparison.Ordinal);
        Assert.Contains("human being", brief, StringComparison.Ordinal);
        Assert.Contains("Long Way Home", brief, StringComparison.Ordinal);
        Assert.Contains("Expert", brief, StringComparison.Ordinal);

        // No Guardian material anywhere in it.
        Assert.DoesNotContain("Guardian", brief, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Directive 47", brief, StringComparison.Ordinal);
    }

    [Fact]
    public void ABriefedCrewMemberIsToldTheyHaveNoToolsAndNoDatabase()
    {
        // Anti-invention reaches them too. A pilot in a fighter bay who confidently answers a
        // question about a station four hundred light years away is the same failure the
        // guardrails exist to prevent, wearing a different hat.
        var brief = CrewAddressing.Brief(new CrewMember("Vance", 1), null);

        Assert.Contains("no tools", brief, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("say so plainly", brief, StringComparison.OrdinalIgnoreCase);
    }
}
