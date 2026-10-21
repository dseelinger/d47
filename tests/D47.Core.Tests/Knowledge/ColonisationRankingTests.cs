using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>Ordering colonisation candidates by how good a colony they would make.</summary>
public class ColonisationRankingTests
{
    private static ColonisationSystem System(
        string name,
        double distance = 5,
        int bodies = 10,
        params (string Subtype, int Count)[] planets) => new()
        {
            Name = name,
            Distance = distance,
            BodyCount = bodies,
            Planets = planets.Length > 0 ? planets : [("Rocky body", 4)],
        };

    private static BodySummary Body(
        string system,
        string name,
        string subtype = "Icy body",
        bool landable = false,
        double arrival = 100,
        string? reserve = null,
        params string[] rings) => new()
        {
            Name = $"{system} {name}",
            SystemName = system,
            Subtype = subtype,
            IsLandable = landable,
            DistanceToArrival = arrival,
            ReserveLevel = reserve,
            Rings = [.. rings.Select(type => new RingSummary($"{system} {name} Ring", type))],
        };

    private static Dictionary<string, IReadOnlyList<BodySummary>> Bodies(params (string System, BodySummary[] Bodies)[] entries) =>
        entries.ToDictionary(entry => entry.System, entry => (IReadOnlyList<BodySummary>)entry.Bodies, StringComparer.Ordinal);

    private static string First(IReadOnlyList<ColonisationSystem> systems, Dictionary<string, IReadOnlyList<BodySummary>> bodies) =>
        ColonisationRanking.Rank(systems, bodies)[0].System.Name;

    [Fact]
    public void ASystemWithAnEarthLikeWorldRanksAboveAnOtherwiseEqualSystemWithNone()
    {
        var bodies = Bodies(
            ("Plain", [Body("Plain", "1")]),
            ("Garden", [Body("Garden", "1", "Earth-like world")]));

        Assert.Equal("Garden", First([System("Plain", 1), System("Garden", 9)], bodies));
    }

    [Fact]
    public void APristineMetallicRingRanksAboveADepletedIcyOne()
    {
        var bodies = Bodies(
            ("Icy", [Body("Icy", "1", reserve: "Depleted", rings: "Icy")]),
            ("Metal", [Body("Metal", "1", reserve: "Pristine", rings: "Metallic")]));

        Assert.Equal("Metal", First([System("Icy", 1), System("Metal", 9)], bodies));
    }

    [Fact]
    public void FourLandableRockyBodiesRankAboveFourLandableIcyOnes()
    {
        var bodies = Bodies(
            ("Ice", [.. Enumerable.Range(1, 4).Select(n => Body("Ice", n.ToString(), "Icy body", landable: true))]),
            ("Rock", [.. Enumerable.Range(1, 4).Select(n => Body("Rock", n.ToString(), "Rocky body", landable: true))]));

        Assert.Equal("Rock", First([System("Ice", 1), System("Rock", 9)], bodies));
    }

    [Fact]
    public void ASystemWhoseBodiesAllLieBeyondTwoThousandLightSecondsRanksBelowOneWithinIt()
    {
        var bodies = Bodies(
            ("Far", [Body("Far", "A", arrival: 0), Body("Far", "1", arrival: 5_000), Body("Far", "2", arrival: 9_000)]),
            ("Close", [Body("Close", "A", arrival: 0), Body("Close", "1", arrival: 300), Body("Close", "2", arrival: 900)]));

        Assert.Equal("Close", First([System("Far", 1), System("Close", 9)], bodies));
    }

    [Fact]
    public void EqualScoresFallToTheNearerSystem()
    {
        var bodies = Bodies(
            ("Near", [Body("Near", "1")]),
            ("Distant", [Body("Distant", "1")]));

        Assert.Equal("Near", First([System("Distant", 9), System("Near", 2)], bodies));
    }

    [Fact]
    public void ASystemWithNoBodiesIsRankedOnTheScanAlone()
    {
        var ranked = ColonisationRanking.Rank(
            [System("Plain", 1, planets: [("Icy body", 4)]), System("Garden", 9, planets: [("Earth-like world", 1)])],
            new Dictionary<string, IReadOnlyList<BodySummary>>());

        Assert.Equal("Garden", ranked[0].System.Name);
        Assert.Contains(ranked[0].Reasons, reason => reason.Contains("Earth-like", StringComparison.Ordinal));
    }

    [Fact]
    public void TheShortlistHoldsAtMostTwelveSystemsBestScanScoreFirst()
    {
        var systems = Enumerable.Range(1, 20)
            .Select(n => System($"S{n:00}", n, planets: n == 15 ? [("Earth-like world", 1)] : [("Icy body", 1)]))
            .ToList();

        var shortlist = ColonisationRanking.Shortlist(systems);

        Assert.Equal(12, shortlist.Count);
        Assert.Equal("S15", shortlist[0].Name);
    }

    [Fact]
    public void TheShortlistStopsBeforeItsBodyCountsPassTheSearchPage()
    {
        var systems = new[] { System("A", 1, 300), System("B", 2, 150), System("C", 3, 100) };

        var shortlist = ColonisationRanking.Shortlist(systems);

        Assert.True(shortlist.Sum(system => system.BodyCount) <= ColonisationRanking.BodyPage);
        Assert.Equal(["A", "B"], shortlist.Select(system => system.Name));
    }

    [Fact]
    public void TheShortlistAlwaysReturnsTheFirstSystemEvenWhenItAloneIsOverThePage()
    {
        var shortlist = ColonisationRanking.Shortlist([System("Huge", 1, 900), System("Small", 2, 5)]);

        Assert.Equal(["Huge"], shortlist.Select(system => system.Name));
    }
}
