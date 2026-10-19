using D47.Core.Journal;
using D47.Core.Stories;

namespace D47.Core.Knowledge;

/// <summary>What the Commander's records say about where they might settle.</summary>
public sealed record HomeSystemRequest
{
    public required string CurrentSystem { get; init; }

    public required CareerWeights Weights { get; init; }

    public IReadOnlyList<ColonisationClaim> Claims { get; init; } = [];

    /// <summary>The pledged power's name, or null when unpledged.</summary>
    public string? Power { get; init; }

    /// <summary>The squadron's minor faction; null outside a squadron or while none is named.</summary>
    public string? SquadronFaction { get; init; }

    /// <summary>The smallest pad the ship aboard needs; null skips the station rule.</summary>
    public PadSize? Pad { get; init; }
}

/// <summary>Gathers home system candidates from <see cref="IGalaxyService"/> and ranks them. Awaits the network: call it off the tick.</summary>
public sealed class HomeSystemGatherer(IGalaxyService galaxy)
{
    public const int MaxCandidates = 12;

    public const double Radius = 200;

    private const int MaxStationLookups = 40;

    private const int SearchSize = 100;

    public async Task<IReadOnlyList<HomeSystemSuggestion>> SuggestAsync(HomeSystemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var power = request.Power is { Length: > 0 } pledged && PowerCapitals.TryGet(pledged, out _) ? pledged : null;
        var faction = string.IsNullOrWhiteSpace(request.SquadronFaction) ? null : request.SquadronFaction.Trim();

        var squadronSystems = faction is null
            ? []
            : await SystemsAsync(request.CurrentSystem, new Dictionary<string, string> { ["controlling_faction"] = faction }, cancellationToken).ConfigureAwait(false);

        var powerSystems = power is null ? [] : await PowerSystemsAsync(request.CurrentSystem, power, cancellationToken).ConfigureAwait(false);

        var squadronNames = Names(squadronSystems);
        var powerNames = Names(powerSystems);

        var claimed = request.Claims
            .Where(claim => claim.BeaconDeployedAt is not null)
            .OrderByDescending(claim => claim.BeaconDeployedAt)
            .Take(MaxCandidates)
            .Select(claim => new Found(claim.StarSystem, claim.SystemAddress, null, true))
            .ToList();

        List<Found> found;

        if (claimed.Count > 0)
        {
            found = claimed;
        }
        else if (faction is not null && power is not null)
        {
            found = From([.. squadronSystems, .. powerSystems]);
        }
        else if (faction is not null)
        {
            found = From(squadronSystems);
        }
        else if (power is not null)
        {
            found = From(powerSystems);
        }
        else
        {
            found = await NearbyAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var candidates = new List<HomeSystemCandidate>();
        var lookups = 0;

        foreach (var system in found.DistinctBy(system => system.Name, StringComparer.OrdinalIgnoreCase).OrderByDescending(system => Membership(system, squadronNames, powerNames))
            .ThenBy(system => system.Distance ?? double.MaxValue))
        {
            if (candidates.Count >= MaxCandidates || (!system.Claimed && lookups >= MaxStationLookups))
            {
                break;
            }

            var candidate = new HomeSystemCandidate(system.Name)
            {
                SystemAddress = system.Address,
                Distance = system.Distance,
                Claimed = system.Claimed,
                InSquadronSet = squadronNames.Contains(system.Name),
                InPowerSet = powerNames.Contains(system.Name),
                Populated = !system.Claimed,
            };

            if (!system.Claimed)
            {
                lookups++;
                var stations = await FixedStationsAsync(system.Name, cancellationToken).ConfigureAwait(false);

                if (stations.Count == 0 || (request.Pad is { } pad && !stations.Any(station => station.Admits(pad))))
                {
                    continue;
                }

                candidate = candidate with
                {
                    SystemAddress = candidate.SystemAddress ?? stations.Select(station => station.SystemAddress).FirstOrDefault(address => address is not null),
                    LargePadMarkets = stations.Count(station => (station.HasLargePad || station.LargePads > 0) && station.Services.Contains("Market", StringComparer.OrdinalIgnoreCase)),
                    HasTourismStation = await HasTourismAsync(system.Name, cancellationToken).ConfigureAwait(false),
                };
            }

            candidates.Add(candidate);
        }

        var scored = new List<HomeSystemCandidate>();

        foreach (var candidate in candidates)
        {
            var bodies = candidate.SystemAddress is { } address
                ? await galaxy.SystemBodiesAsync(address, cancellationToken).ConfigureAwait(false)
                : [];

            scored.Add(candidate with { Bodies = bodies });
        }

        return HomeSystemRanking.Rank(scored, request.Weights);
    }

    private static int Membership(Found system, HashSet<string> squadron, HashSet<string> power) =>
        (squadron.Contains(system.Name) ? 1 : 0) + (power.Contains(system.Name) ? 1 : 0);

    private static HashSet<string> Names(IReadOnlyList<SystemSummary> systems) =>
        new(systems.Select(system => system.Name), StringComparer.OrdinalIgnoreCase);

    private static List<Found> From(IReadOnlyList<SystemSummary> systems) =>
        [.. systems.Select(system => new Found(system.Name, system.SystemAddress, system.Distance, false))];

    private async Task<IReadOnlyList<SystemSummary>> SystemsAsync(
        string reference, Dictionary<string, string> filters, CancellationToken cancellationToken)
    {
        if (!GalaxyQuery.TryParse(reference, filters, SearchSize, out var query, out var failure))
        {
            throw new InvalidOperationException(failure);
        }

        return (await galaxy.SearchAsync(query, cancellationToken).ConfigureAwait(false)).Systems;
    }

    private async Task<IReadOnlyList<SystemSummary>> PowerSystemsAsync(string reference, string power, CancellationToken cancellationToken)
    {
        var strongholds = await SystemsAsync(
            reference, new Dictionary<string, string> { ["power"] = power, ["power_state"] = "Stronghold" }, cancellationToken).ConfigureAwait(false);

        if (!PowerCapitals.TryGet(power, out var capital) || strongholds.Any(system => system.Name.Equals(capital, StringComparison.OrdinalIgnoreCase)))
        {
            return strongholds;
        }

        var near = await SystemsAsync(capital, new Dictionary<string, string> { ["distance"] = "1" }, cancellationToken).ConfigureAwait(false);
        var home = near.FirstOrDefault(system => system.Name.Equals(capital, StringComparison.OrdinalIgnoreCase))
                   ?? new SystemSummary { Name = capital };

        var away = await galaxy.DistanceAsync(reference, capital, cancellationToken).ConfigureAwait(false);

        return [home with { Distance = away }, .. strongholds];
    }

    /// <summary>
    /// Populated systems within <see cref="Radius"/> light years: those the Mining or Exobiology body searches
    /// find, when either career is among the top three weights, and the populated systems nearest the Commander.
    /// </summary>
    private async Task<List<Found>> NearbyAsync(HomeSystemRequest request, CancellationToken cancellationToken)
    {
        var found = new List<Found>();

        var topThree = Enum.GetValues<Career>()
            .Where(career => career is not (Career.Crime or Career.SearchAndRescue))
            .OrderByDescending(request.Weights.Weight)
            .Take(3)
            .ToList();

        foreach (var career in topThree.Where(career => career is Career.Mining or Career.Exobiology))
        {
            var bodyRequest = new BodyRequest
            {
                ReferenceSystem = request.CurrentSystem,
                MaxDistance = Radius,
                Size = SearchSize,
                ReserveLevel = career == Career.Mining ? "Pristine" : null,
                Signal = career == Career.Exobiology ? "Biological" : null,
            };

            if (!BodyQuery.TryParse(bodyRequest, out var query, out var failure))
            {
                throw new InvalidOperationException(failure);
            }

            var bodies = (await galaxy.FindBodiesAsync(query, cancellationToken).ConfigureAwait(false)).Bodies;

            found.AddRange(bodies
                .GroupBy(body => body.SystemName, StringComparer.OrdinalIgnoreCase)
                .Select(group => new Found(group.Key, group.Select(body => body.SystemAddress).FirstOrDefault(address => address is not null), group.Min(body => body.Distance), false)));
        }

        var populated = await SystemsAsync(
            request.CurrentSystem,
            new Dictionary<string, string> { ["distance"] = Radius.ToString(System.Globalization.CultureInfo.InvariantCulture), ["population"] = "1-" },
            cancellationToken).ConfigureAwait(false);

        var ordered = found.OrderBy(system => system.Distance ?? double.MaxValue).ToList();
        ordered.AddRange(From(populated));

        return ordered;
    }

    private async Task<IReadOnlyList<StationSummary>> FixedStationsAsync(string system, CancellationToken cancellationToken)
    {
        var result = await galaxy.FindStationsAsync(StationQuery.Near(system, 1, 20), cancellationToken).ConfigureAwait(false);

        return [.. result.Stations.Where(station => station.IsFixed && station.SystemName.Equals(system, StringComparison.OrdinalIgnoreCase))];
    }

    private async Task<bool> HasTourismAsync(string system, CancellationToken cancellationToken)
    {
        var search = new StationSearch
        {
            ReferenceSystem = system,
            MaxDistance = 1,
            Size = 20,
            Filters = new Dictionary<string, string> { ["station_economy"] = "Tourism" },
        };

        if (!StationQuery.TryParse(search, out var query, out var failure))
        {
            throw new InvalidOperationException(failure);
        }

        var result = await galaxy.FindStationsAsync(query, cancellationToken).ConfigureAwait(false);

        return result.Stations.Any(station => station.IsFixed && station.SystemName.Equals(system, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record Found(string Name, long? Address, double? Distance, bool Claimed);
}
