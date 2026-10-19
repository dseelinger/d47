using D47.Core.Journal;

namespace D47.Core.Stories;

/// <summary>How strongly each <see cref="Career"/> pulls on a chapter's draw.</summary>
public sealed class CareerWeights
{
    public const int Default = 50;

    public const int Used = 75;

    private static readonly IReadOnlyDictionary<Career, string[]> Figures = new Dictionary<Career, string[]>
    {
        [Career.Combat] = ["Combat.Bounties_Claimed", "Combat.Combat_Bonds", "Combat.Assassinations"],
        [Career.Trading] = ["Trading.Market_Profits"],
        [Career.Mining] = ["Mining.Quantity_Mined"],
        [Career.Exploration] = ["Exploration.Exploration_Profits"],
        [Career.Exobiology] = ["Exobiology.Organic_Data"],
        [Career.Passengers] = ["Passengers.Passengers_Missions_Delivered"],
        [Career.SearchAndRescue] = ["Search_And_Rescue.SearchRescue_Count"],
    };

    private readonly Dictionary<Career, double> weights;

    private CareerWeights(Dictionary<Career, double> weights) => this.weights = weights;

    /// <summary>Every career at <see cref="Default"/>.</summary>
    public static CareerWeights Even { get; } = From(null, CareerStatistics.Empty);

    public double Weight(Career career) => weights[career];

    /// <summary>
    /// Ratings are 1 to 100, <see cref="Default"/> where absent. While every rating but Crime's is <see cref="Default"/>, a career
    /// with activity in <paramref name="statistics"/> weighs <see cref="Used"/>. Crime weighs <see cref="Default"/>.
    /// </summary>
    public static CareerWeights From(IReadOnlyDictionary<Career, int>? ratings, CareerStatistics statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);

        int Rating(Career career) => Math.Clamp(ratings is not null && ratings.TryGetValue(career, out var rating) ? rating : Default, 1, 100);

        var untouched = Figures.Keys.All(career => Rating(career) == Default);
        var result = new Dictionary<Career, double>();

        foreach (var career in Enum.GetValues<Career>())
        {
            if (career == Career.Crime)
            {
                result[career] = Default;
                continue;
            }

            var done = Figures[career].Sum(figure => statistics.Read(figure) ?? 0) > 0;

            result[career] = untouched && done ? Used : Rating(career);
        }

        return new CareerWeights(result);
    }
}
