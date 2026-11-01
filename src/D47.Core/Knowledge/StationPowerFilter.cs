using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>A galaxy service whose station answers leave out Stronghold Carriers of Powers the Commander is not pledged to.</summary>
public sealed class StationPowerFilter(IGalaxyService inner, Func<PowerplayPledge?> pledge) : IGalaxyService
{
    private const int OverAsk = 3;

    public async Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
    {
        var power = pledge()?.Power;

        // Page 1 asks for three times Size so dropped carriers do not leave it short; a later page asks for
        // exactly that service page at Size.
        var later = query.Page > 1;

        var asked = await inner
            .FindStationsAsync(
                later ? query : query with { Size = Math.Min(GalaxyQuery.MaxSize, query.Size * OverAsk) },
                cancellationToken)
            .ConfigureAwait(false);

        bool Drops(StationSummary station) =>
            station.IsOtherPowersStrongholdCarrier(power)
            && !query.StrongholdCarriers
            && !NamesPower(query, station.ControllingPower);

        var kept = new List<StationSummary>();
        var leftOut = 0;
        var read = 0;

        foreach (var station in asked.Stations)
        {
            if (!later && kept.Count >= query.Size)
            {
                break;
            }

            read++;

            if (Drops(station))
            {
                leftOut++;
            }
            else
            {
                kept.Add(station);
            }
        }

        var dropped = asked.Stations.Count(Drops);

        return asked with
        {
            Total = Math.Max(kept.Count, asked.Total - dropped),
            Stations = kept,
            LeftOut = leftOut,
            More = asked.More || read < asked.Stations.Count,
        };
    }

    private static bool NamesPower(StationQuery query, string? power) =>
        power is { Length: > 0 }
        && query.Criteria.Any(criterion =>
            criterion.Filter.Name == "power"
            && criterion.Choices.Contains(power, StringComparer.OrdinalIgnoreCase));

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
        int? maxReportAgeHours,
        CancellationToken cancellationToken) =>
        inner.FindOutfittersAsync(items, referenceSystem, maxDistance, minPad, maxReportAgeHours, cancellationToken);

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
