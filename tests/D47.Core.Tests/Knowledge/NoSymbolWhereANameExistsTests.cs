using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>No shown or spoken string is composed from a symbol where a name exists, and where no name exists the sentence says so rather than prettifying a symbol into something that looks like one.</summary>
public class NoSymbolWhereANameExistsTests
{
    private static ShipLoadout Flying(string ship, string? name = null)
    {
        var loadout = name is null
            ? $$"""{ "timestamp":"2026-08-19T10:00:00Z", "event":"Loadout", "Ship":"{{ship}}", "ShipID":53, "Modules":[] }"""
            : $$"""
               { "timestamp":"2026-08-19T10:00:00Z", "event":"Loadout", "Ship":"{{ship}}",
                 "ShipID":53, "ShipName":"{{name}}", "Modules":[] }
               """;

        return Applied(
            """{ "timestamp":"2026-08-19T09:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
            loadout);
    }

    private static ShipLoadout Applied(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!.Ship;
    }

    [Fact]
    public void AHullTheJournalDoesNotLocaliseIsStillNamed()
    {
        // Remediation 15 item 14, reported as: "Oxen, a type9_military is not bound to a core, so whoever is
        // aboard stays aboard." Frontier does not always send `Ship_Localised`, and a Type-10 Defender is one
        // of the hulls it omits — its symbol is `type9_military`, which is not even the hull it names.
        Assert.Equal("Type-10 Defender", EliteSpecifications.Ship("type9_military")?.Name);

        Assert.Equal("Oxen, a Type-10 Defender", Flying("type9_military", "Oxen").Describe());
        Assert.Equal("Type-10 Defender", Flying("type9_military").Describe());
    }

    [Fact]
    public void TheJournalsOwnLocalisedNameStillWins()
    {
        // The table is the fallback, not the authority.
        var ship = Applied(
            """{ "timestamp":"2026-08-19T09:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
            """
            { "timestamp":"2026-08-19T10:00:00Z", "event":"Loadout", "Ship":"krait_mkii",
              "Ship_Localised":"Krait Mk II", "ShipID":7, "ShipName":"Gemini", "Modules":[] }
            """);

        Assert.Equal("Gemini, a Krait Mk II", ship.Describe());
    }

    [Fact]
    public void AHullNoTableKnowsReadsAsTheSymbolRatherThanAsAGuess()
    {
        // The other half of the rule.
        Assert.Null(EliteSpecifications.Ship("not_a_hull"));
        Assert.Equal("not_a_hull", Flying("not_a_hull").Describe());
    }
}
