using System.Globalization;

namespace D47.Core.Knowledge;

/// <summary>A candidate system with its score and the features that earned it.</summary>
public sealed record RankedColonisationSystem(ColonisationSystem System, double Score, IReadOnlyList<string> Reasons);

/// <summary>Orders colonisation candidates by how good a colony they would make; reads no service.</summary>
public static class ColonisationRanking
{
    public const int DetailPool = 12;

    /// <summary>The body search's page size, which one call to it cannot exceed.</summary>
    public const int BodyPage = 500;

    private const double ReachLightSeconds = 2_000;

    private const int ReasonCount = 3;

    private static readonly string[] SettlementSubtypes =
        ["Rocky body", "Rocky Ice world", "High metal content world", "Metal-rich body"];

    private enum Signature
    {
        Population,
        Settlement,
        Rings,
        Terraformable,
        Biology,
        Size,
        Reach,
    }

    private static readonly Dictionary<Signature, double> Weights = new()
    {
        [Signature.Population] = 3,
        [Signature.Settlement] = 2,
        [Signature.Rings] = 2,
        [Signature.Terraformable] = 1,
        [Signature.Biology] = 1,
        [Signature.Size] = 1,
        [Signature.Reach] = 1,
    };

    /// <summary>
    /// The systems that get the body call: highest scan-only score first, at most <see cref="DetailPool"/>,
    /// stopping before their body counts pass <see cref="BodyPage"/>. The first is always kept.
    /// </summary>
    public static IReadOnlyList<ColonisationSystem> Shortlist(IReadOnlyList<ColonisationSystem> systems)
    {
        ArgumentNullException.ThrowIfNull(systems);

        var ordered = Rank(systems, new Dictionary<string, IReadOnlyList<BodySummary>>());
        var kept = new List<ColonisationSystem>();
        var bodies = 0;

        foreach (var ranked in ordered)
        {
            if (kept.Count >= DetailPool || (kept.Count > 0 && bodies + ranked.System.BodyCount > BodyPage))
            {
                break;
            }

            kept.Add(ranked.System);
            bodies += ranked.System.BodyCount;
        }

        return kept;
    }

    /// <summary>The final order. A system with no entry in <paramref name="bodies"/> is scored on the scan alone.</summary>
    public static IReadOnlyList<RankedColonisationSystem> Rank(
        IReadOnlyList<ColonisationSystem> systems,
        IReadOnlyDictionary<string, IReadOnlyList<BodySummary>> bodies)
    {
        ArgumentNullException.ThrowIfNull(systems);
        ArgumentNullException.ThrowIfNull(bodies);

        var raws = systems.ToDictionary(
            system => system,
            system => bodies.TryGetValue(system.Name, out var found) ? FromBodies(system, found) : FromScan(system));

        var highest = Weights.Keys.ToDictionary(
            signature => signature, signature => raws.Count == 0 ? 0 : raws.Values.Max(raw => raw[signature].Raw));

        return
        [
            .. systems
                .Select(system =>
                {
                    var parts = Weights.Keys
                        .Select(signature => (
                            Score: highest[signature] > 0 ? Weights[signature] * raws[system][signature].Raw / highest[signature] : 0,
                            Feature: raws[system][signature].Feature))
                        .Where(part => part.Score > 0)
                        .OrderByDescending(part => part.Score)
                        .ToList();

                    return new RankedColonisationSystem(
                        system, parts.Sum(part => part.Score), [.. parts.Take(ReasonCount).Select(part => part.Feature)]);
                })
                .OrderByDescending(ranked => ranked.Score)
                .ThenBy(ranked => ranked.System.Distance ?? double.MaxValue)
                .ThenBy(ranked => ranked.System.Name, StringComparer.Ordinal),
        ];
    }

    private static Dictionary<Signature, (double Raw, string Feature)> FromScan(ColonisationSystem system)
    {
        var worlds = system.Planets.Where(planet => IsPopulationWorld(planet.Subtype)).Sum(planet => planet.Count);

        var ground = system.Planets
            .Where(planet => SettlementSubtypes.Contains(planet.Subtype, StringComparer.OrdinalIgnoreCase))
            .Sum(planet => planet.Count);

        return new Dictionary<Signature, (double, string)>
        {
            [Signature.Population] = (worlds, Worlds(worlds)),
            [Signature.Settlement] = (ground, $"{ground} rocky or metal-rich planet{Plural(ground)}"),
            [Signature.Rings] = (0, string.Empty),
            [Signature.Terraformable] = (system.Terraformable, Candidates(system.Terraformable)),
            [Signature.Biology] = (0, string.Empty),
            [Signature.Size] = (system.BodyCount, Bodies(system.BodyCount)),
            [Signature.Reach] = (0, string.Empty),
        };
    }

    private static Dictionary<Signature, (double Raw, string Feature)> FromBodies(
        ColonisationSystem system, IReadOnlyList<BodySummary> bodies)
    {
        var worlds = bodies.Count(body => IsPopulationWorld(body.Subtype));
        var landable = bodies.Count(body => body.IsLandable);

        var ground = landable + bodies.Count(body =>
            body.IsLandable && body.Subtype is not null
            && SettlementSubtypes.Contains(body.Subtype, StringComparer.OrdinalIgnoreCase));

        double rings = 0;
        var ringCount = 0;
        var metallic = 0;
        var rich = 0;

        foreach (var body in bodies)
        {
            var pristine = body.ReserveLevel is not null
                && (body.ReserveLevel.Equals("Pristine", StringComparison.OrdinalIgnoreCase)
                    || body.ReserveLevel.Equals("Major", StringComparison.OrdinalIgnoreCase));

            foreach (var ring in body.Rings)
            {
                var metal = ring.Type is "Metallic" or "Metal Rich";

                rings += (metal ? 2 : 1) * (pristine ? 2 : 1);
                ringCount++;
                metallic += metal ? 1 : 0;
                rich += pristine ? 1 : 0;
            }
        }

        var terraformable = bodies.Count(body =>
            body.TerraformingState?.Contains("Candidate", StringComparison.OrdinalIgnoreCase) == true);

        var biological = bodies.Sum(body => body.Signals
            .Where(signal => signal.Kind.Contains("Biological", StringComparison.OrdinalIgnoreCase))
            .Sum(signal => signal.Count));

        var others = bodies.Where(body => body.DistanceToArrival != 0).ToList();
        var near = others.Count(body => body.DistanceToArrival is <= ReachLightSeconds);
        var reach = others.Count == 0 ? 0 : (double)near / others.Count;

        var ringText = $"{ringCount} ring{Plural(ringCount)}"
            + (metallic > 0 ? $", {metallic} metallic" : string.Empty)
            + (rich > 0 ? $", {rich} with rich reserves" : string.Empty);

        return new Dictionary<Signature, (double, string)>
        {
            [Signature.Population] = (worlds, Worlds(worlds)),
            [Signature.Settlement] = (ground, $"{landable} landable bod{(landable == 1 ? "y" : "ies")}"),
            [Signature.Rings] = (rings, ringText),
            [Signature.Terraformable] = (terraformable, Candidates(terraformable)),
            [Signature.Biology] = (biological, $"{biological} biological signal{Plural(biological)}"),
            [Signature.Size] = (system.BodyCount, Bodies(system.BodyCount)),
            [Signature.Reach] = (
                reach,
                $"{Math.Round(reach * 100).ToString("0", CultureInfo.InvariantCulture)}% of bodies within 2,000 ls of arrival"),
        };
    }

    private static bool IsPopulationWorld(string? subtype) =>
        subtype is not null
        && (subtype.Equals("Earth-like world", StringComparison.OrdinalIgnoreCase)
            || subtype.Equals("Water world", StringComparison.OrdinalIgnoreCase));

    private static string Plural(int count) => count == 1 ? string.Empty : "s";

    private static string Worlds(int count) =>
        count == 1 ? "an Earth-like or water world" : $"{count} Earth-like or water worlds";

    private static string Candidates(int count) => $"{count} terraforming candidate{Plural(count)}";

    private static string Bodies(int count) => $"{count} bod{(count == 1 ? "y" : "ies")}";
}
