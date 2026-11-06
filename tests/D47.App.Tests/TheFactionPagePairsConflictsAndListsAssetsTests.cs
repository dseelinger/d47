using System.Globalization;
using System.Text.Json;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Conflicts and Assets sections of the Faction page, without drawing them.</summary>
public sealed class TheFactionPagePairsConflictsAndListsAssetsTests
{
    private const string Us = "Kestrel Reach Compact";

    private static readonly DateTimeOffset Reported = DateTimeOffset.Parse("2026-11-01T12:00:00Z", CultureInfo.InvariantCulture);

    private static FactionNeighbour Other(string name, string[]? active = null, string[]? pending = null) =>
        new(name, 0.1) { ActiveStates = active ?? [], PendingStates = pending ?? [] };

    private static FactionSystem Sys(string name, string[]? active = null, string[]? pending = null, params FactionNeighbour[] others) =>
        new()
        {
            Name = name,
            Influence = 0.3,
            ActiveStates = active ?? [],
            PendingStates = pending ?? [],
            Others = others,
            ReportedAt = Reported,
        };

    private static FactionReport Report(params FactionSystem[] systems) => new(Us, "Confederacy", "Independent", systems, []);

    private static FactionReport Stations(params FactionStation[] stations) => new(Us, null, null, [], stations);

    private static FactionStation Station(string name, string system, string? type, PadSize? pad = null, double? distance = null) =>
        new(name, type, pad, distance, system);

    private static SystemStandings Saw(string system, string at, string conflicts, params string[] others)
    {
        var factions = string.Join(",", new[] { $"{{\"Name\":\"{Us}\",\"Influence\":0.3,\"ActiveStates\":[{{\"State\":\"War\"}}]}}" }.Concat(others.Select(name => $"{{\"Name\":\"{name}\",\"Influence\":0.1,\"ActiveStates\":[{{\"State\":\"War\"}}]}}")));
        using var document = JsonDocument.Parse($"{{\"timestamp\":\"{at}\",\"event\":\"FSDJump\",\"StarSystem\":\"{system}\",\"Factions\":[{factions}],\"Conflicts\":[{conflicts}]}}");

        return SystemStandings.Empty.Apply(new JournalEvent(DateTimeOffset.Parse(at, CultureInfo.InvariantCulture), "FSDJump", document.RootElement.Clone()));
    }

    private static string Conflict(string status, string one, string oneStake, int oneDays, string two, string twoStake, int twoDays) =>
        $"{{\"WarType\":\"war\",\"Status\":\"{status}\",\"Faction1\":{{\"Name\":\"{one}\",\"Stake\":\"{oneStake}\",\"WonDays\":{oneDays}}},"
        + $"\"Faction2\":{{\"Name\":\"{two}\",\"Stake\":\"{twoStake}\",\"WonDays\":{twoDays}}}}}";

    [Fact]
    public void OneOtherFactionInTheSameStateIsTheOpponent()
    {
        var report = Report(Sys("Alpha", active: ["War"], others: [Other("Red", ["War"]), Other("Blue", ["Boom"])]));

        var row = Assert.Single(FactionConflicts.Of(report, SystemStandings.Empty));

        Assert.Equal((OpponentKind.Named, "Red", false), (row.Kind, row.Opponent, row.Visited));
    }

    [Fact]
    public void AWarWithThreeCandidatesAndNoReadingSaysOneOfThree()
    {
        var report = Report(Sys("Alpha", active: ["War"], others: [Other("Red", ["War"]), Other("Blue", ["War"]), Other("Green", ["War"])]));

        var row = Assert.Single(FactionConflicts.Of(report, SystemStandings.Empty));

        Assert.Equal(OpponentKind.OneOf, row.Kind);
        Assert.Equal("One of 3 factions", row.OneOfText);
        Assert.Equal("Red · Blue · Green", row.CandidateText);
    }

    [Fact]
    public void FourCandidatesNameTheFirstThenCountTheRest()
    {
        var report = Report(Sys("Alpha", active: ["War"], others: [Other("A", ["War"]), Other("B", ["War"]), Other("C", ["War"]), Other("D", ["War"])]));

        var row = Assert.Single(FactionConflicts.Of(report, SystemStandings.Empty));

        Assert.Equal("A · B · C and 1 more", row.CandidateText);
    }

    [Fact]
    public void APendingConflictWhoseOpponentShowsNoStateIsNotKnownYet()
    {
        var report = Report(Sys("Alpha", pending: ["Election"], others: [Other("Red"), Other("Blue", ["Boom"])]));

        var row = Assert.Single(FactionConflicts.Of(report, SystemStandings.Empty));

        Assert.Equal((OpponentKind.Unknown, false), (row.Kind, row.Active));
        Assert.Equal("Opponent not known yet", FactionConflicts.OpponentUnknownText);
    }

    [Fact]
    public void TheCommandersReadingNamesTheOpponentAmongSeveralAndFillsDaysAndStakes()
    {
        var report = Report(Sys("Alpha", active: ["War"], others: [Other("Red", ["War"]), Other("Blue", ["War"])]));
        var standings = Saw("Alpha", "2026-11-05T10:00:00Z", Conflict("active", Us, "Hub", 1, "Blue", "Camp", 2), "Red", "Blue");

        var row = Assert.Single(FactionConflicts.Of(report, standings));

        Assert.Equal((OpponentKind.Named, "Blue", true), (row.Kind, row.Opponent, row.Visited));
        Assert.Equal("1 – 2", row.DaysText);
        Assert.Equal(("Hub", "Camp"), (row.OurStake, row.TheirStake));
    }

    [Fact]
    public void ASystemWithTwoConflictsPairsOursWithItsOwnOpponent()
    {
        var report = Report(Sys("Alpha", active: ["War"], others: [Other("Red", ["War"]), Other("Blue", ["War"]), Other("Green", ["War"])]));
        var both = Conflict("active", "Red", "x", 0, "Green", "y", 0) + "," + Conflict("active", "Blue", "Dock", 3, Us, "Port", 4);
        var standings = Saw("Alpha", "2026-11-05T10:00:00Z", both, "Red", "Blue", "Green");

        var row = Assert.Single(FactionConflicts.Of(report, standings));

        Assert.Equal("Blue", row.Opponent);
        Assert.Equal("4 – 3", row.DaysText);
        Assert.Equal(("Port", "Dock"), (row.OurStake, row.TheirStake));
    }

    [Fact]
    public void ActiveConflictsComeBeforePendingThenBySystemName()
    {
        var report = Report(
            Sys("Delta", pending: ["War"]),
            Sys("Charlie", active: ["Civil war"]),
            Sys("Bravo", pending: ["Election"]),
            Sys("Alpha", active: ["War"]),
            Sys("Echo", active: ["Boom"]));

        var rows = FactionConflicts.Of(report, SystemStandings.Empty);

        Assert.Equal(["Alpha", "Charlie", "Bravo", "Delta"], rows.Select(r => r.System));
        Assert.Equal(rows.Count, FactionModel.Overview(report, FactionModel.Rows(report, SystemStandings.Empty)).InConflict);
    }

    [Fact]
    public void KindOptionsListAllFirstThenLargestCountWithNoTypeLast()
    {
        var stations = Stations(
            Station("A", "S", "Outpost"),
            Station("B", "S", null),
            Station("C", "S", "Coriolis Starport"),
            Station("D", "S", "Coriolis Starport"),
            Station("E", "S", "Planetary Construction Depot"),
            Station("F", "S", "Space Construction Depot")).Stations;

        var options = FactionAssets.Options(stations);

        Assert.Equal(
            [("All kinds", 6), ("Construction depots", 2), ("Coriolis", 2), ("Outposts", 1), ("No type given", 1)],
            options.Select(o => (o.Label, o.Count)));
    }

    [Fact]
    public void AssetsAreFilteredByKindAndSearchedByStationOrSystemName()
    {
        var stations = Stations(
            Station("Hub One", "Alpha", "Outpost"),
            Station("Port Two", "Beta", "Outpost"),
            Station("Camp", "Alpha", "Settlement")).Stations;

        Assert.Equal(["Hub One", "Camp"], FactionAssets.Matching(stations, "alpha", FactionAssets.AllId).Select(s => s.Name));
        Assert.Equal(["Hub One"], FactionAssets.Matching(stations, "alpha", "Outposts").Select(s => s.Name));
        Assert.Equal(["Port Two"], FactionAssets.Matching(stations, "two", FactionAssets.AllId).Select(s => s.Name));
    }

    [Fact]
    public void AssetsComeBySystemThenNameAndSortFromEveryHeading()
    {
        var stations = FactionAssets.Of(Stations(
            Station("Zed", "Alpha", "Outpost", PadSize.Medium, 300),
            Station("Ace", "Alpha", null, null, null),
            Station("Mid", "Beta", "Coriolis Starport", PadSize.Large, 50)));
        var head = new SortHeader<FactionStation>("T", FactionAssets.Heads, FactionAssets.DefaultOrder);

        Assert.Equal(["Ace", "Zed", "Mid"], stations.Select(s => s.Name));

        head.Press(2);
        Assert.Equal(["Mid", "Zed", "Ace"], head.Sort(stations).Select(s => s.Name));

        head.Press(3);
        Assert.Equal(["Mid", "Zed", "Ace"], head.Sort(stations).Select(s => s.Name));

        head.Press(4);
        Assert.Equal(["Mid", "Zed", "Ace"], head.Sort(stations).Select(s => s.Name));

        head.Press(4);
        Assert.Equal(["Zed", "Mid", "Ace"], head.Sort(stations).Select(s => s.Name));
    }

    [Theory]
    [InlineData(650, 650, "650 STATIONS")]
    [InlineData(177, 650, "177 OF 650 STATIONS")]
    public void TheCountReadsAllOrTheFilteredShare(int shown, int all, string expected) =>
        Assert.Equal(expected, FactionAssets.CountText(shown, all));

    [Fact]
    public void ADistanceReadsWithThousandsAndLightSeconds() =>
        Assert.Equal("1,240 ls", FactionAssets.Distance(1240.4));
}
