using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>A career rated 0 has no row drawn; Crime weighs its rating like the other careers.</summary>
public sealed class ACareerRatedZeroIsNeverDrawnTests
{
    private const int Seeds = 1000;

    private static readonly ShipLoadout Ship = new() { Type = "cobramkiii", CargoCapacity = 64 };

    private static readonly RankState Ranks = new()
    {
        TakenAt = DateTimeOffset.UnixEpoch,
        Standings = [new RankStanding("Combat", 0), new RankStanding("Soldier", 0)],
    };

    private static readonly IReadOnlyList<Func<DrawableActivity, bool>> MissionOnly =
        [row => row.Kind == TriggerKind.Mission, row => row.Kind == TriggerKind.Mission, row => row.Kind == TriggerKind.Mission];

    private static CareerWeights Rated(int crime, int others = 50) =>
        CareerWeights.From(Enum.GetValues<Career>().ToDictionary(career => career, career => career == Career.Crime ? crime : others), CareerStatistics.Empty);

    private static IReadOnlyList<DrawnActivity> Draw(
        CareerWeights weights, uint seed, bool comfort = false, bool limited = false, bool neverCompleted = false) =>
        ChapterDraw.Draw(
            weights, Ranks, Ship, odyssey: true, [], null, comfort, CareerStatistics.Empty, 0, seed,
            neverCompleted: neverCompleted ? _ => true : null,
            limits: limited ? MissionOnly : null);

    private static bool IsCrime(DrawnActivity slot) =>
        ChapterActivities.All.Any(row => row.Career == Career.Crime && row.Kind == slot.Kind && row.Family == slot.Family);

    [Fact]
    public void CrimeWeighsItsRating()
    {
        Assert.Equal(1, Rated(1).Weight(Career.Crime));
        Assert.Equal(0, Rated(0).Weight(Career.Crime));
        Assert.Equal(100, Rated(500).Weight(Career.Crime));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void CrimeAtZeroNeverDrawsACrimeRow(bool limited, bool comfort, bool neverCompleted)
    {
        var weights = Rated(0);

        for (uint seed = 0; seed < Seeds; seed++)
        {
            Assert.DoesNotContain(Draw(weights, seed, comfort, limited, neverCompleted), IsCrime);
        }
    }

    [Fact]
    public void CrimeAtOneIsDrawnLessOftenThanCrimeAtFifty()
    {
        int Crimes(int rating) => Enumerable.Range(0, Seeds).Sum(seed => Draw(Rated(rating), (uint)seed).Count(IsCrime));

        var low = Crimes(1);

        Assert.True(Crimes(50) > low);
        Assert.True(low > 0 || Crimes(50) > 0);
    }

    [Fact]
    public void EveryRatingAtZeroDrawsNothingAndDoesNotThrow()
    {
        var weights = Rated(0, others: 0);

        for (uint seed = 0; seed < 100; seed++)
        {
            Assert.Empty(Draw(weights, seed));
            Assert.Empty(Draw(weights, seed, comfort: true, limited: true, neverCompleted: true));
        }
    }

    [Fact]
    public void ExplorationAtZeroNeverSightsees()
    {
        var weights = CareerWeights.From(new Dictionary<Career, int> { [Career.Exploration] = 0 }, CareerStatistics.Empty);

        Assert.Equal(0, ChapterDraw.SightChance(weights, previousWasSight: false));
    }
}
