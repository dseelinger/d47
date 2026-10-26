using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>The sight list and the sightseeing draw a sightseeing chapter is built from.</summary>
public sealed class SightseeingPiecesTests
{
    private static NotablePlace Place(string name, string type, string system, double x = 100) =>
        new(name, type, system, null, new StarPosition(x, 0, 0));

    private static CareerWeights Exploring(int rating) =>
        CareerWeights.From(new Dictionary<Career, int> { [Career.Exploration] = rating }, CareerStatistics.Empty);

    [Fact]
    public void PickKeepsOnlyTheSixSightTypes()
    {
        var near = NotableSights.Types.Select((type, index) => Place($"P{index}", type, $"S{index}")).Append(Place("Other", "Black Holes", "Sx")).ToList();

        var picked = NotableSights.Pick(near, [], null);

        Assert.Equal(6, picked.Count);
        Assert.DoesNotContain(picked, place => place.Name == "Other");
    }

    [Fact]
    public void PickMatchesTypesExactly() =>
        Assert.Empty(NotableSights.Pick([Place("A", "nebulae", "S")], [], null));

    [Fact]
    public void PickDropsSeenSystemsIgnoringCase() =>
        Assert.Equal(["B"], NotableSights.Pick([Place("A", "Nebulae", "Sol"), Place("B", "Nebulae", "Alpha")], ["SOL"], null).Select(place => place.Name));

    [Fact]
    public void PickAppliesTheBandWhenGiven()
    {
        var near = new[] { Place("In", "Nebulae", "A", 200), Place("Low", "Nebulae", "B", 99), Place("High", "Nebulae", "C", 301), Place("Edge", "Nebulae", "D", 300) };

        Assert.Equal(["In", "Edge"], NotableSights.Pick(near, [], new ChapterBand.Range(100, 300)).Select(place => place.Name));
        Assert.Equal(4, NotableSights.Pick(near, [], null).Count);
    }

    [Fact]
    public void PickKeepsInputOrderAndStopsAtMost()
    {
        var near = Enumerable.Range(0, 12).Select(index => Place($"P{index}", "Nebulae", $"S{index}")).ToList();

        var picked = NotableSights.Pick(near, [], null);

        Assert.Equal(NotableSights.Most, picked.Count);
        Assert.Equal(near.Take(NotableSights.Most).Select(place => place.Name), picked.Select(place => place.Name));
    }

    [Fact]
    public void ASightChapterIsOneInFiveAtAnEvenWeight() =>
        Assert.Equal(0.2, ChapterDraw.SightChance(CareerWeights.Even, false), 10);

    [Fact]
    public void TheSightChanceScalesWithTheExplorationWeight()
    {
        Assert.Equal(0.1, ChapterDraw.SightChance(Exploring(25), false), 10);
        Assert.Equal(0.4, ChapterDraw.SightChance(Exploring(100), false), 10);
    }

    [Fact]
    public void TheSightChanceNeverPassesTheCap() =>
        Assert.True(ChapterDraw.SightChance(Exploring(100), false) <= ChapterDraw.MostSightShare);

    [Fact]
    public void ASightChapterMakesTheNextLessLikely() =>
        Assert.Equal(0.2 * ChapterDraw.RecentFactor, ChapterDraw.SightChance(CareerWeights.Even, true), 10);

    [Fact]
    public void TheSameSeedGivesTheSameSightseeingAnswer()
    {
        for (uint seed = 0; seed < 50; seed++)
        {
            Assert.Equal(ChapterDraw.Sightsee(CareerWeights.Even, false, seed), ChapterDraw.Sightsee(CareerWeights.Even, false, seed));
        }
    }

    [Fact]
    public void SightseeingFollowsTheChance()
    {
        var hits = Enumerable.Range(0, 2000).Count(index => ChapterDraw.Sightsee(CareerWeights.Even, false, ChapterDraw.SightSeed("story", index)));

        Assert.InRange(hits, 300, 500);
    }

    [Fact]
    public void TheSightSeedDiffersFromTheChapterSeed()
    {
        for (var chapter = 1; chapter < 30; chapter++)
        {
            Assert.NotEqual(ChapterDraw.Seed("story", chapter), ChapterDraw.SightSeed("story", chapter));
            Assert.NotEqual(ChapterDraw.Seed("story", chapter, 0), ChapterDraw.SightSeed("story", chapter));
        }
    }

    [Fact]
    public void ASightseeingChapterServesALongHaul() =>
        Assert.True(ChapterFit.ServesLongHaul(null, null, sightseeing: true));
}
