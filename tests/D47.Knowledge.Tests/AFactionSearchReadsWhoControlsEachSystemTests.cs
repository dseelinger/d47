using System.Text.Json;
using D47.Core.Knowledge;
using D47.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>Faction fields read from live system searches for Eurybia Blue Mafia, recorded 2026-09-25.</summary>
public class AFactionSearchReadsWhoControlsEachSystemTests
{
    private const string Faction = "Eurybia Blue Mafia";

    private static GalaxySearchResult Read(string fixture)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", $"spansh-search-faction-{fixture}-eurybia-blue-mafia.json")));

        return SpanshResponse.ReadSearch(document);
    }

    [Fact]
    public void EachSystemCarriesItsControllerItsFactionsAndWhenItWasReported()
    {
        var eurybia = Read("present").Systems[0];

        Assert.Equal("Eurybia", eurybia.Name);
        Assert.Equal(Faction, eurybia.ControllingFaction);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 1, 15, 32, TimeSpan.Zero), eurybia.ReportedAt);
        Assert.Contains(new FactionPresence("Democrats of Eurybia", 0.121335), eurybia.Factions);
    }

    [Fact]
    public void InfluenceArrivesAsAFractionOfOne()
    {
        var influences = Read("present").Systems
            .SelectMany(system => system.Factions)
            .Select(faction => faction.Influence)
            .ToList();

        Assert.NotEmpty(influences);
        Assert.All(influences, influence => Assert.InRange(influence!.Value, 0, 1));
        Assert.Equal(0.235592, Read("present").Systems[0].Factions.Single(f => f.Name == Faction).Influence);
    }

    [Fact]
    public void TheSystemsAFactionControlsAreAmongThoseItIsPresentIn()
    {
        var present = Read("present");
        var controlling = Read("controlling");

        Assert.True(controlling.Total <= present.Total, $"{controlling.Total} controlled of {present.Total} present");
        Assert.All(controlling.Systems, system =>
        {
            Assert.Equal(Faction, system.ControllingFaction);
            Assert.Contains(system.Factions, faction => faction.Name == Faction);
        });
        Assert.Contains(present.Systems, system => system.ControllingFaction != Faction);
    }
}
