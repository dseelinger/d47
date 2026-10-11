namespace D47.Core.Knowledge;

/// <summary>One system from a mission station search, with every faction present and every station.</summary>
public sealed record MissionSystem(SystemSummary System, IReadOnlyList<StationSummary> Stations);

/// <summary>What a mission station search returned, before ranking.</summary>
/// <param name="Reference">The system distances were measured from, as the service resolved it.</param>
/// <param name="Total">How many systems matched in total; more than <paramref name="Systems"/> holds when the scan was cut.</param>
public sealed record MissionStationScan(string? Reference, int Total, IReadOnlyList<MissionSystem> Systems);

/// <summary>One ranked station, with how many factions in its system are in a matching state or under a matching government.</summary>
public sealed record MissionStationCandidate(StationSummary Station, int MatchingStates, int MatchingGovernments, int Score);

/// <summary>A validated search for the stations likeliest to have one mission type on their boards.</summary>
public sealed record MissionStationQuery
{
    private MissionStationQuery()
    {
    }

    public const double DefaultDistance = 50;

    public const double MaxDistanceCeiling = 500;

    /// <summary>How many systems, nearest first, the search returns for ranking.</summary>
    public const int ScanSize = 30;

    /// <summary>How many stations an answer names.</summary>
    public const int Size = 5;

    public required string ReferenceSystem { get; init; }

    public double MaxDistance { get; init; } = DefaultDistance;

    /// <summary>The faction states the type's names were recorded under.</summary>
    public IReadOnlyList<string> States { get; init; } = [];

    public IReadOnlyList<string> Governments { get; init; } = [];

    /// <summary>The system primary economies the type's names were recorded under.</summary>
    public IReadOnlyList<string> Economies { get; init; } = [];

    /// <summary>Whether every name is an on-foot mission, which is taken in a station's social space.</summary>
    public bool OnFoot { get; init; }

    /// <summary>The states the search filters factions on, or the governments where there are no states; empty for no filter.</summary>
    public IReadOnlyList<string> FactionFilter => States.Count > 0 ? States : Governments;

    /// <summary>Whether <see cref="FactionFilter"/> holds states rather than governments.</summary>
    public bool FiltersOnStates => States.Count > 0;

    public static bool TryParse(
        string? referenceSystem,
        IReadOnlyCollection<MissionName> names,
        double? maxDistance,
        out MissionStationQuery query,
        out string failure)
    {
        query = new MissionStationQuery { ReferenceSystem = string.Empty };
        failure = string.Empty;

        if (string.IsNullOrWhiteSpace(referenceSystem))
        {
            failure = "I don't know where the Commander is right now, so I need a system to search out from.";
            return false;
        }

        if (names.Count == 0)
        {
            failure = "I need a mission type to search for.";
            return false;
        }

        query = new MissionStationQuery
        {
            ReferenceSystem = referenceSystem.Trim(),
            MaxDistance = Math.Clamp(maxDistance is > 0 ? maxDistance.Value : DefaultDistance, 1, MaxDistanceCeiling),
            States = Distinct(names, name => name.State),
            Governments = Distinct(names, name => name.Government),
            Economies = Distinct(names, name => name.Economy),
            OnFoot = names.All(name => name.Family.StartsWith("Mission_OnFoot_", StringComparison.OrdinalIgnoreCase)),
        };

        return true;
    }

    private static string[] Distinct(IEnumerable<MissionName> names, Func<MissionName, string> pick) =>
        [.. names.Select(pick).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)];
}

/// <summary>Ranks the stations of a mission station search by how likely their boards are to carry the type.</summary>
public static class MissionStationRanking
{
    private const string Missions = "Missions";

    private const string SocialSpace = "Social Space";

    /// <summary>
    /// Stations with a mission board, scored by the factions present in a matching state, plus those under a
    /// matching government, plus one for a matching primary economy; highest score first, then nearest.
    /// </summary>
    public static IReadOnlyList<MissionStationCandidate> Rank(MissionStationQuery query, MissionStationScan scan, int count) =>
        [.. scan.Systems
            .SelectMany(system => Candidates(query, system))
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Station.Distance ?? double.MaxValue)
            .ThenBy(candidate => candidate.Station.DistanceToArrival ?? double.MaxValue)
            .ThenBy(candidate => candidate.Station.Name, StringComparer.Ordinal)
            .Take(count)];

    private static IEnumerable<MissionStationCandidate> Candidates(MissionStationQuery query, MissionSystem system)
    {
        var states = system.System.Factions.Count(faction => Matches(query.States, faction.State));
        var governments = system.System.Factions.Count(faction => Matches(query.Governments, faction.Government));
        var economy = Matches(query.Economies, system.System.PrimaryEconomy) ? 1 : 0;
        var score = states + governments + economy;

        return system.Stations
            .Where(station => HasBoard(station, query.OnFoot))
            .Select(station => new MissionStationCandidate(
                station with { SystemName = system.System.Name, Distance = system.System.Distance },
                states,
                governments,
                score));
    }

    private static bool HasBoard(StationSummary station, bool onFoot) =>
        HasService(station, Missions) && (!onFoot || IsSettlement(station) || HasService(station, SocialSpace));

    private static bool HasService(StationSummary station, string service) =>
        station.Services.Contains(service, StringComparer.OrdinalIgnoreCase);

    private static bool IsSettlement(StationSummary station) =>
        station.Type is { } type
        && (string.Equals(type, "Settlement", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "Surface Settlement", StringComparison.OrdinalIgnoreCase));

    private static bool Matches(IReadOnlyList<string> wanted, string? value) =>
        value is not null && wanted.Contains(value, StringComparer.OrdinalIgnoreCase);
}
