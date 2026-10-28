namespace D47.Core.Knowledge;

/// <summary>A galaxy service that remembers every system name its searches have returned this session.</summary>
public sealed class GalaxySearchNames(IGalaxyService inner) : IGalaxyService
{
    private readonly Lock _gate = new();

    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The names returned so far, as a copy.</summary>
    public IReadOnlyCollection<string> Names
    {
        get
        {
            lock (_gate)
            {
                return [.. _names];
            }
        }
    }

    public async Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
    {
        var result = await inner.SearchAsync(query, cancellationToken).ConfigureAwait(false);

        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(result.Reference))
            {
                _names.Add(result.Reference);
            }

            foreach (var system in result.Systems)
            {
                _names.Add(system.Name);
            }
        }

        return result;
    }

    public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
        inner.DistanceAsync(from, to, cancellationToken);

    public Task<D47.Core.Journal.StarPosition?> PositionAsync(string system, CancellationToken cancellationToken) =>
        inner.PositionAsync(system, cancellationToken);

    public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
        inner.FindStationsAsync(query, cancellationToken);

    public async Task<IReadOnlyList<OutfitterStation>> FindOutfittersAsync(
        IReadOnlyList<OutfittingItem> items,
        string referenceSystem,
        double maxDistance,
        PadSize? minPad,
        CancellationToken cancellationToken)
    {
        var found = await inner
            .FindOutfittersAsync(items, referenceSystem, maxDistance, minPad, cancellationToken)
            .ConfigureAwait(false);

        lock (_gate)
        {
            foreach (var outfitter in found)
            {
                _names.Add(outfitter.Station.SystemName);
            }
        }

        return found;
    }

    public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
        inner.FindBodiesAsync(query, cancellationToken);

    public Task<ColonisationScan> ScanForColonisationAsync(
        ColonisationQuery query, CancellationToken cancellationToken) =>
        inner.ScanForColonisationAsync(query, cancellationToken);

    public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) =>
        inner.SystemBodiesAsync(systemAddress, cancellationToken);

    public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
        inner.SystemBiologyAsync(systemAddress, cancellationToken);

    public async Task<MissionStationScan> FindMissionStationsAsync(
        MissionStationQuery query, CancellationToken cancellationToken)
    {
        var scan = await inner.FindMissionStationsAsync(query, cancellationToken).ConfigureAwait(false);

        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(scan.Reference))
            {
                _names.Add(scan.Reference);
            }

            foreach (var system in scan.Systems)
            {
                _names.Add(system.System.Name);
            }
        }

        return scan;
    }
}
