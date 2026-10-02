using System.Text.Json;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>Factions' influence and conflicts per system, from <c>FSDJump</c>, <c>Location</c> and <c>CarrierJump</c> (#731).</summary>
public class ASystemKeepsItsFactionsAndConflictsTests
{
    private const string Civil =
        """{ "WarType":"civilwar", "Status":"active", "Faction1":{ "Name":"Havalokul Gold Drug Empire", "Stake":"", "WonDays":0 }, "Faction2":{ "Name":"Labour of Havalokul", "Stake":"Hahm's Barracks", "WonDays":0 } }""";

    [Fact]
    public void AnActiveCivilWarIsKeptWithBothSidesAtZeroDays()
    {
        var standings = SystemStandings.Empty.Apply(Jump("2026-09-01T10:00:00Z", "Havalokul", 0.4, Civil));

        var conflict = Assert.Single(standings.Latest("Havalokul")!.Conflicts);
        Assert.Equal("civilwar", conflict.WarType);
        Assert.Equal("active", conflict.Status);
        Assert.Equal("Havalokul Gold Drug Empire", conflict.Faction1.Name);
        Assert.Equal("Labour of Havalokul", conflict.Faction2.Name);
        Assert.Equal("Hahm's Barracks", conflict.Faction2.Stake);
        Assert.Equal((0, 0), (conflict.Faction1.WonDays, conflict.Faction2.WonDays));
        Assert.False(conflict.Ended);
        Assert.Null(conflict.Winner);
    }

    [Fact]
    public void AnEndedConflictNamesTheSideWithMoreWonDays()
    {
        var ended = Civil.Replace("\"active\"", "\"\"").Replace("\"WonDays\":0 }, \"Faction2\"", "\"WonDays\":4 }, \"Faction2\"")
            .Replace("\"Hahm's Barracks\", \"WonDays\":0", "\"Hahm's Barracks\", \"WonDays\":2");

        var conflict = Assert.Single(SystemStandings.Empty.Apply(Jump("2026-09-01T10:00:00Z", "Havalokul", 0.4, ended)).Latest("Havalokul")!.Conflicts);

        Assert.True(conflict.Ended);
        Assert.Equal("Havalokul Gold Drug Empire", conflict.Winner);
    }

    [Fact]
    public void AnEndedConflictWithEqualDaysHasNoWinner()
    {
        var conflict = Assert.Single(SystemStandings.Empty.Apply(Jump("2026-09-01T10:00:00Z", "Havalokul", 0.4, Civil.Replace("\"active\"", "\"\""))).Latest("Havalokul")!.Conflicts);

        Assert.Null(conflict.Winner);
    }

    [Fact]
    public void InfluenceReadADayApartGivesTheChange()
    {
        var standings = SystemStandings.Empty
            .Apply(Jump("2026-09-01T10:00:00Z", "Havalokul", 0.40))
            .Apply(Jump("2026-09-02T10:00:00Z", "Havalokul", 0.46));

        Assert.Equal(0.06, standings.InfluenceChange("Havalokul", "Labour of Havalokul")!.Value, 6);
    }

    [Fact]
    public void TwoVisitsOnOneDayAreOneReadingAndGiveNoChange()
    {
        var standings = SystemStandings.Empty
            .Apply(Jump("2026-09-01T10:00:00Z", "Havalokul", 0.40))
            .Apply(Jump("2026-09-01T18:00:00Z", "Havalokul", 0.41));

        Assert.Null(standings.InfluenceChange("Havalokul", "Labour of Havalokul"));
        Assert.Equal(0.41, standings.Latest("Havalokul")!.InfluenceOf("Labour of Havalokul"));
    }

    [Fact]
    public void ALaterVisitComparesWithTheDayBeforeItNotTheOldest()
    {
        var standings = SystemStandings.Empty
            .Apply(Jump("2026-09-01T10:00:00Z", "Havalokul", 0.10))
            .Apply(Jump("2026-09-02T10:00:00Z", "Havalokul", 0.30))
            .Apply(Jump("2026-09-03T10:00:00Z", "Havalokul", 0.35));

        Assert.Equal(0.05, standings.InfluenceChange("Havalokul", "Labour of Havalokul")!.Value, 6);
    }

    [Fact]
    public void AnEventWithoutFactionsLeavesTheSystemAlone()
    {
        var standings = SystemStandings.Empty.Apply(new JournalEvent(
            DateTimeOffset.Parse("2026-09-01T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            "FSDJump",
            JsonDocument.Parse("""{ "event":"FSDJump", "StarSystem":"Empty" }""").RootElement));

        Assert.False(standings.IsKnown);
    }

    [Fact]
    public void ReplayingTheEventsGivesTheStateWatchingThemLiveDoes()
    {
        var events = new[]
        {
            Jump("2026-09-01T10:00:00Z", "Havalokul", 0.40, Civil),
            Jump("2026-09-02T10:00:00Z", "Havalokul", 0.46),
        };

        var live = new CommanderGameState(new CommanderIdentity("F1", "CMDR"));
        foreach (var e in events)
        {
            live.Apply(e);
        }

        var replay = events.Aggregate(SystemStandings.Empty, (s, e) => s.Apply(e));

        Assert.Equal(replay.Latest("Havalokul"), live.Standings.Latest("Havalokul"), new ReadingComparer());
        Assert.Equal(0.06, live.Standings.InfluenceChange("Havalokul", "Labour of Havalokul")!.Value, 6);
    }

    private sealed class ReadingComparer : IEqualityComparer<SystemReading?>
    {
        public bool Equals(SystemReading? x, SystemReading? y) =>
            x!.SeenAt == y!.SeenAt && x.Factions.SequenceEqual(y.Factions) && x.Conflicts.SequenceEqual(y.Conflicts);

        public int GetHashCode(SystemReading? obj) => obj!.SeenAt.GetHashCode();
    }

    private static JournalEvent Jump(string at, string system, double influence, string? conflict = null)
    {
        var conflicts = conflict is null ? string.Empty : $", \"Conflicts\":[ {conflict} ]";
        var json = $$"""
            { "timestamp":"{{at}}", "event":"FSDJump", "StarSystem":"{{system}}",
              "Factions":[ { "Name":"Labour of Havalokul", "Influence":{{influence.ToString(System.Globalization.CultureInfo.InvariantCulture)}} },
                           { "Name":"Havalokul Gold Drug Empire", "Influence":0.2 } ]{{conflicts}} }
            """;

        return new JournalEvent(
            DateTimeOffset.Parse(at, System.Globalization.CultureInfo.InvariantCulture),
            "FSDJump",
            JsonDocument.Parse(json).RootElement);
    }
}
