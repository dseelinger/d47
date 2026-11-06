using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Ships;

/// <summary>One hull that fits the budget, with the best build the search found for it from an empty hull.</summary>
/// <param name="Cost">Hull (nothing if owned) plus every module at full price; the reserve is not included.</param>
public sealed record HullChoice(
    ShipSpecification Hull,
    double Score,
    long Cost,
    BuildResult Result,
    double LadenJump,
    double Cargo,
    double Survivability,
    double Damage);

/// <summary>The hulls that fit, best first.</summary>
public sealed record BestShipResult(IReadOnlyList<HullChoice> Kept)
{
    /// <summary>The most hulls an answer names.</summary>
    public const int MostNamed = 3;

    public IReadOnlyList<HullChoice> Named => [.. Kept.Take(MostNamed)];
}

/// <summary>
/// Ranks hulls for a goal and a budget. Each hull is searched from an empty hull with every module at full price,
/// which overstates the cost. Each role is normalised across the hulls that fit, so hulls are comparable. It makes
/// no model call and reads no clock, so the same input gives the same hulls.
/// </summary>
public static class BestShip
{
    /// <summary>Whether the Commander can buy this hull: it has a price and no unmet gate.</summary>
    public static bool Available(ShipSpecification hull, RankState ranks)
    {
        ArgumentNullException.ThrowIfNull(hull);
        ArgumentNullException.ThrowIfNull(ranks);

        if (hull.Cost is null
            || string.Equals(hull.Requirement, "early-adoption", StringComparison.Ordinal)
            || string.Equals(hull.Requirement, "arx-early-access", StringComparison.Ordinal))
        {
            return false;
        }

        return NavalRanks.Gate(hull.Requirement) is not { } gate
               || (ranks.For(gate.Navy.ToString())?.Rank ?? 0) >= gate.Rank;
    }

    /// <summary>The hull symbols the Commander already owns, as <see cref="EliteSpecifications"/> spells them.</summary>
    public static IReadOnlySet<string> Owned(CommanderGameState? state)
    {
        var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (state is null)
        {
            return owned;
        }

        var types = state.Fleet.Ships.Select(ship => ship.Type);

        if (state.Ship is { IsKnown: true } flying)
        {
            types = types.Append(flying.Type!);
        }

        foreach (var type in types)
        {
            if (EliteSpecifications.Ship(type)?.Symbol is { } symbol)
            {
                owned.Add(symbol);
            }
        }

        return owned;
    }

    /// <summary>
    /// The hulls that fit <paramref name="credits"/> (null for no limit), best first. Blocks; run it on the pool.
    /// <paramref name="goal"/> is read as a weighted goal whose <see cref="BuildGoal.Credits"/> is ignored.
    /// </summary>
    public static BestShipResult Choose(
        IEnumerable<ShipSpecification> hulls,
        BuildGoal goal,
        RankState ranks,
        IReadOnlySet<string> owned,
        long? credits,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hulls);
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(owned);

        var ordered = hulls.OrderBy(hull => hull.Symbol, StringComparer.Ordinal).ToArray();
        var searched = new (ShipSpecification Hull, BuildResult Result, long Cost, BuildSearch.RoleFigures Figures)?[ordered.Length];

        // Each search is pure, so running hulls at once changes nothing but the time taken.
        Parallel.For(
            0,
            ordered.Length,
            new ParallelOptions { CancellationToken = cancellationToken },
            index => searched[index] = Search(ordered[index], goal, ranks, owned, credits, cancellationToken));

        var found = searched.OfType<(ShipSpecification Hull, BuildResult Result, long Cost, BuildSearch.RoleFigures Figures)>().ToList();

        if (found.Count == 0)
        {
            return new BestShipResult([]);
        }

        var weights = Enum.GetValues<BuildRole>()
            .Select(role => (Role: role, Weight: goal.Weights.TryGetValue(role, out var weight) && weight > 0 ? weight : 0))
            .ToList();

        var total = weights.Sum(pair => pair.Weight);
        var (survivable, armed) = BuildSearch.CombatMix(goal.Combat);

        double Share(BuildRole role) => total > 0 ? weights.First(pair => pair.Role == role).Weight / total : 0;

        double Top(Func<BuildSearch.RoleFigures, double> figure) => found.Max(entry => figure(entry.Figures));

        double Ratio(double figure, double top) => top > 0 ? figure / top : 0;

        var topJump = Top(figures => figures.LadenJump);
        var topCargo = Top(figures => figures.Cargo);
        var topSurvivability = Top(figures => figures.Survivability);
        var topDamage = Top(figures => figures.Damage);

        var kept = found
            .Select(entry =>
            {
                var figures = entry.Figures;

                var score = Share(BuildRole.Exploration) * Ratio(figures.LadenJump, topJump)
                            + Share(BuildRole.Cargo) * Ratio(figures.Cargo, topCargo)
                            + Share(BuildRole.Combat) * (survivable * Ratio(figures.Survivability, topSurvivability)
                                                         + armed * Ratio(figures.Damage, topDamage));

                return new HullChoice(
                    entry.Hull,
                    score,
                    entry.Cost,
                    entry.Result,
                    figures.LadenJump,
                    figures.Cargo,
                    figures.Survivability,
                    figures.Damage);
            })
            .OrderByDescending(choice => choice.Score)
            .ThenBy(choice => choice.Hull.Symbol, StringComparer.Ordinal)
            .ToList();

        return new BestShipResult(kept);
    }

    private static (ShipSpecification Hull, BuildResult Result, long Cost, BuildSearch.RoleFigures Figures)? Search(
        ShipSpecification hull,
        BuildGoal goal,
        RankState ranks,
        IReadOnlySet<string> owned,
        long? credits,
        CancellationToken cancellationToken)
    {
        if (!Available(hull, ranks) || EliteSpecifications.Slots(hull.Symbol).Count == 0)
        {
            return null;
        }

        var price = owned.Contains(hull.Symbol) ? 0 : hull.Cost!.Value;

        if (credits is { } have && price > have)
        {
            return null;
        }

        var searched = goal with
        {
            Mode = GoalMode.Weighted,
            Target = null,
            Credits = credits is { } limit ? limit - price : null,
        };

        var result = BuildSearch.Find(
            new ShipBuild(string.Empty, hull.Symbol, hull.Symbol), null, searched, cancellationToken, fillCoreInternals: true);

        var cost = price + result.Credits;

        if (result.Unmet is not null
            || result.Missing.Count > 0
            || (credits is { } most && cost + BuildSearch.RebuyShare * (hull.Cost!.Value + result.Credits) > most))
        {
            return null;
        }

        return (hull, result, cost, BuildSearch.FiguresOf(result.Plan, goal.Combat));
    }

    /// <summary>The answer in words, from the figures; the named hulls only.</summary>
    public static string Say(BestShipResult result, BuildGoal goal, long? credits, bool balanceKnown)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(goal);

        string N(double value, string format = "0.#") => value.ToString(format, CultureInfo.InvariantCulture);

        var limit = credits is { } amount
            ? $"with {amount.ToString("N0", CultureInfo.InvariantCulture)} credits"
            : balanceKnown
                ? "with no spending limit"
                : "with no spending limit, because you named no amount and your balance is not known";

        if (result.Kept.Count == 0)
        {
            return $"No hull I know of fits that goal {limit}.";
        }

        var lines = new List<string>();

        foreach (var (choice, index) in result.Named.Select((choice, index) => (choice, index)))
        {
            var figures = new List<string>();

            if (goal.Weights.GetValueOrDefault(BuildRole.Exploration) > 0)
            {
                figures.Add($"laden jump {N(choice.LadenJump)} light years");
            }

            if (goal.Weights.GetValueOrDefault(BuildRole.Cargo) > 0)
            {
                figures.Add($"{N(choice.Cargo)} tons of cargo");
            }

            if (goal.Weights.GetValueOrDefault(BuildRole.Combat) > 0 && choice.Result.Gauges.Combat is { } combat)
            {
                if (combat.Shields is { } shields)
                {
                    figures.Add($"{N(shields, "0")} megajoules of shields");
                }

                figures.Add($"{N(combat.SustainedDps)} sustained damage a second");
            }

            lines.Add(
                $"{(index + 1).ToString(CultureInfo.InvariantCulture)}. {choice.Hull.Name}, "
                + $"{choice.Cost.ToString("N0", CultureInfo.InvariantCulture)} credits"
                + (figures.Count > 0 ? $" ({string.Join(", ", figures)})" : string.Empty));
        }

        return $"Best hulls for that {limit}, best first: {string.Join("; ", lines)}. "
               + "Costs price every module at full cost, so they are the most it would be.";
    }
}
