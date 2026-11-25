using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>A galaxy service whose station answers carry the trader type the Commander's trades and d47's table give, in place of the service's.</summary>
public sealed class TraderTypeCorrection(IGalaxyService inner, Func<long, string?> knownType) : IGalaxyService
{
    public async Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
    {
        var asked = await inner.FindStationsAsync(query, cancellationToken).ConfigureAwait(false);

        if (asked.Refusal is not null)
        {
            return asked;
        }

        var kind = query.TraderType;
        var kept = new List<StationSummary>();

        foreach (var station in asked.Stations)
        {
            var corrected = Corrected(station);

            if (kind is null || SameKind(corrected.TraderType, kind))
            {
                kept.Add(corrected);
            }
        }

        var dropped = asked.Stations.Count - kept.Count;
        var added = 0;

        if (kind is not null && query.Page == 1 && query.Sort is null)
        {
            var found = await MissedAsync(query, kind, asked, cancellationToken).ConfigureAwait(false);

            added = found.Count;
            kept.AddRange(found);
            kept = [.. kept.OrderBy(station => station.Distance ?? double.MaxValue)];
        }

        return asked with { Total = Math.Max(kept.Count, asked.Total - dropped + added), Stations = kept };
    }

    /// <summary>The stations of <paramref name="kind"/> the service filed under another kind, within the reach of <paramref name="first"/>.</summary>
    private async Task<List<StationSummary>> MissedAsync(
        StationQuery query, string kind, StationSearchResult first, CancellationToken cancellationToken)
    {
        StationSearchResult all;

        try
        {
            all = await inner
                .FindStationsAsync(
                    query with { TraderType = null, IsAboutTraders = true, Size = GalaxyQuery.MaxSize },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return [];
        }

        if (all.Refusal is not null)
        {
            return [];
        }

        var returned = first.Stations.Where(station => station.MarketId is not null).Select(station => station.MarketId).ToHashSet();
        var reach = first.More && first.Stations.Count > 0
            ? first.Stations.Max(station => station.Distance ?? double.MaxValue)
            : double.MaxValue;
        var found = new List<StationSummary>();

        foreach (var station in all.Stations)
        {
            var corrected = Corrected(station);

            if (SameKind(corrected.TraderType, kind)
                && !(corrected.MarketId is { } id && returned.Contains(id))
                && !found.Any(other => other.MarketId is not null && other.MarketId == corrected.MarketId)
                && (!first.More || corrected.Distance <= reach))
            {
                found.Add(corrected);
            }
        }

        return found;
    }

    private static bool SameKind(string? type, string kind) =>
        string.Equals(type, kind, StringComparison.OrdinalIgnoreCase);

    private StationSummary Corrected(StationSummary station) =>
        station.MarketId is { } id && knownType(id) is { } type && type != station.TraderType
            ? station with { TraderType = type }
            : station;

    public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
        inner.SearchAsync(query, cancellationToken);

    public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
        inner.DistanceAsync(from, to, cancellationToken);

    public Task<StarPosition?> PositionAsync(string system, CancellationToken cancellationToken) =>
        inner.PositionAsync(system, cancellationToken);

    public async Task<IReadOnlyList<OutfitterStation>> FindOutfittersAsync(
        IReadOnlyList<OutfittingItem> items,
        string referenceSystem,
        double maxDistance,
        PadSize? minPad,
        int? maxReportAgeHours,
        CancellationToken cancellationToken)
    {
        var found = await inner
            .FindOutfittersAsync(items, referenceSystem, maxDistance, minPad, maxReportAgeHours, cancellationToken)
            .ConfigureAwait(false);

        return [.. found.Select(outfitter => outfitter with { Station = Corrected(outfitter.Station) })];
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

        return scan with
        {
            Systems =
            [
                .. scan.Systems.Select(system => system with { Stations = [.. system.Stations.Select(Corrected)] }),
            ],
        };
    }

    public Task<FactionReport> FactionAsync(string faction, CancellationToken cancellationToken, bool refresh = false, IProgress<FactionProgress>? progress = null) =>
        inner.FactionAsync(faction, cancellationToken, refresh, progress);
}
