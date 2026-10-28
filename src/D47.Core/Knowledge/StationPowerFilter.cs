using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>A galaxy service whose station answers leave out Stronghold Carriers of Powers the Commander is not pledged to.</summary>
public sealed class StationPowerFilter(IGalaxyService inner, Func<PowerplayPledge?> pledge) : IGalaxyService
{
    private const int OverAsk = 3;

    public async Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
    {
        var power = pledge()?.Power;

        var asked = await inner
            .FindStationsAsync(query with { Size = Math.Min(GalaxyQuery.MaxSize, query.Size * OverAsk) }, cancellationToken)
            .ConfigureAwait(false);

        var kept = new List<StationSummary>();
        var leftOut = 0;

        foreach (var station in asked.Stations)
        {
            if (kept.Count >= query.Size)
            {
                break;
            }

            if (station.IsOtherPowersStrongholdCarrier(power))
            {
                leftOut++;
            }
            else
            {
                kept.Add(station);
            }
        }

        var dropped = asked.Stations.Count(station => station.IsOtherPowersStrongholdCarrier(power));

        return asked with
        {
            Total = Math.Max(kept.Count, asked.Total - dropped),
            Stations = kept,
            LeftOut = leftOut,
        };
    }

    public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
        inner.SearchAsync(query, cancellationToken);

    public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
        inner.DistanceAsync(from, to, cancellationToken);

    public Task<StarPosition?> PositionAsync(string system, CancellationToken cancellationToken) =>
        inner.PositionAsync(system, cancellationToken);

    public Task<IReadOnlyList<OutfitterStation>> FindOutfittersAsync(
        IReadOnlyList<OutfittingItem> items,
        string referenceSystem,
        double maxDistance,
        PadSize? minPad,
        CancellationToken cancellationToken) =>
        inner.FindOutfittersAsync(items, referenceSystem, maxDistance, minPad, cancellationToken);

    public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
        inner.FindBodiesAsync(query, cancellationToken);

    public Task<ColonisationScan> ScanForColonisationAsync(
        ColonisationQuery query, CancellationToken cancellationToken) =>
        inner.ScanForColonisationAsync(query, cancellationToken);

    public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) =>
        inner.SystemBodiesAsync(systemAddress, cancellationToken);

    public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
        inner.SystemBiologyAsync(systemAddress, cancellationToken);

    public Task<MissionStationScan> FindMissionStationsAsync(
        MissionStationQuery query, CancellationToken cancellationToken) =>
        inner.FindMissionStationsAsync(query, cancellationToken);
}
