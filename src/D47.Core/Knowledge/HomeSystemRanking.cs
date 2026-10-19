using D47.Core.Stories;

namespace D47.Core.Knowledge;

/// <summary>Why a home system was suggested.</summary>
public enum HomeSystemReason
{
    Architect,
    SquadronAndPower,
    Squadron,
    Power,
    Interests,
}

/// <summary>One system that could be suggested, with what the ranking reads from it.</summary>
public sealed record HomeSystemCandidate(string Name)
{
    public long? SystemAddress { get; init; }

    /// <summary>Light years from the Commander.</summary>
    public double? Distance { get; init; }

    /// <summary>The Commander is architect of this system.</summary>
    public bool Claimed { get; init; }

    public bool InSquadronSet { get; init; }

    public bool InPowerSet { get; init; }

    /// <summary>Combat scores only in a populated system.</summary>
    public bool Populated { get; init; }

    /// <summary>Fixed stations with a market and a large pad.</summary>
    public int LargePadMarkets { get; init; }

    public bool HasTourismStation { get; init; }

    public IReadOnlyList<BodySummary> Bodies { get; init; } = [];
}

/// <summary>A career's weighted share of a system's score, and the features behind it.</summary>
public sealed record CareerStrength(Career Career, double Score, string Feature);

/// <summary>A suggested home system with its reason and its two strongest careers.</summary>
public sealed record HomeSystemSuggestion(string Name, HomeSystemReason Reason, IReadOnlyList<CareerStrength> Strengths);

/// <summary>Orders home system candidates; reads no service.</summary>
public static class HomeSystemRanking
{
    public const int Suggestions = 3;

    /// <summary>Credits of mapping value that count as one body in the Exploration signature.</summary>
    private const double MappingValuePerPoint = 100_000;

    /// <summary>
    /// Architect systems replace every other candidate. Then systems in more of the squadron and power sets come
    /// first, and the sum over careers of weight times signature, each signature scaled to 0–1 across the
    /// candidates, orders the rest.
    /// </summary>
    public static IReadOnlyList<HomeSystemSuggestion> Rank(IReadOnlyList<HomeSystemCandidate> candidates, CareerWeights weights)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(weights);

        var pool = candidates.Any(candidate => candidate.Claimed) ? [.. candidates.Where(candidate => candidate.Claimed)] : candidates;

        var signatures = pool.ToDictionary(candidate => candidate, Signatures);
        var highest = Enum.GetValues<Career>().ToDictionary(
            career => career, career => pool.Count == 0 ? 0 : pool.Max(candidate => signatures[candidate][career].Raw));

        return
        [
            .. pool
                .Select(candidate =>
                {
                    var strengths = Enum.GetValues<Career>()
                        .Select(career => new CareerStrength(
                            career,
                            highest[career] > 0 ? weights.Weight(career) * signatures[candidate][career].Raw / highest[career] : 0,
                            signatures[candidate][career].Feature))
                        .Where(strength => strength.Score > 0)
                        .OrderByDescending(strength => strength.Score)
                        .ToList();

                    return (Candidate: candidate, Total: strengths.Sum(strength => strength.Score), Strengths: strengths);
                })
                .OrderByDescending(scored => Membership(scored.Candidate))
                .ThenByDescending(scored => scored.Total)
                .ThenBy(scored => scored.Candidate.Distance ?? double.MaxValue)
                .ThenBy(scored => scored.Candidate.Name, StringComparer.Ordinal)
                .Take(Suggestions)
                .Select(scored => new HomeSystemSuggestion(scored.Candidate.Name, Reason(scored.Candidate), [.. scored.Strengths.Take(2)])),
        ];
    }

    private static int Membership(HomeSystemCandidate candidate) => (candidate.InSquadronSet ? 1 : 0) + (candidate.InPowerSet ? 1 : 0);

    private static HomeSystemReason Reason(HomeSystemCandidate candidate) => candidate switch
    {
        { Claimed: true } => HomeSystemReason.Architect,
        { InSquadronSet: true, InPowerSet: true } => HomeSystemReason.SquadronAndPower,
        { InSquadronSet: true } => HomeSystemReason.Squadron,
        { InPowerSet: true } => HomeSystemReason.Power,
        _ => HomeSystemReason.Interests,
    };

    private static Dictionary<Career, (double Raw, string Feature)> Signatures(HomeSystemCandidate candidate)
    {
        var bodies = candidate.Bodies;

        var hotspotRings = bodies
            .Where(body => body.Rings.Any(ring => ring.Hotspots.Count > 0))
            .Select(body => (Rings: body.Rings.Count(ring => ring.Hotspots.Count > 0), Rich: IsRich(body.ReserveLevel)))
            .ToList();

        var biological = bodies.Sum(body => body.Signals.Where(signal => signal.Kind.Contains("Biological", StringComparison.OrdinalIgnoreCase)).Sum(signal => signal.Count));

        var special = bodies.Count(body =>
            body.TerraformingState?.Contains("Candidate", StringComparison.OrdinalIgnoreCase) == true
            || body.Subtype is "Earth-like world" or "Water world" or "Ammonia world");

        var mapping = bodies.Sum(body => body.MappingValue ?? 0);
        var ringed = bodies.Count(body => body.Rings.Count > 0);

        return new Dictionary<Career, (double, string)>
        {
            [Career.Mining] = (hotspotRings.Sum(ring => ring.Rings * (ring.Rich ? 2 : 1)), $"{hotspotRings.Sum(ring => ring.Rings)} rings with hotspots"),
            [Career.Exobiology] = (biological, $"{biological} biological signals"),
            [Career.Exploration] = (bodies.Count + special + mapping / MappingValuePerPoint, $"{bodies.Count} bodies, {special} terraformable, earth-like, water or ammonia worlds"),
            [Career.Combat] = (candidate.Populated ? ringed : 0, $"{ringed} ringed bodies"),
            [Career.Trading] = (candidate.LargePadMarkets, $"{candidate.LargePadMarkets} stations with a market and a large pad"),
            [Career.Passengers] = (candidate.HasTourismStation ? 1 : 0, "a station with a Tourism economy"),
            [Career.SearchAndRescue] = (0, string.Empty),
            [Career.Crime] = (0, string.Empty),
        };
    }

    private static bool IsRich(string? reserve) =>
        reserve is not null && (reserve.Equals("Pristine", StringComparison.OrdinalIgnoreCase) || reserve.Equals("Major", StringComparison.OrdinalIgnoreCase));
}
