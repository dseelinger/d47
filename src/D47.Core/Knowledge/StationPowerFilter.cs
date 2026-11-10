using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>A galaxy service whose station answers leave out Stronghold Carriers the Commander cannot dock at.</summary>
public sealed class StationPowerFilter(IGalaxyService inner, Func<PowerplayPledge?> pledge) : IGalaxyService
{
    private const int OverAsk = 3;

    /// <summary>The sentence a Stronghold carrier search gets from a Commander pledged to no Power.</summary>
    public const string UnpledgedCarriers =
        "Stronghold Carriers dock only Commanders pledged to their Power, and you are not pledged.";

    public async Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
    {
        var power = pledge()?.Power is { Length: > 0 } pledged ? pledged : null;

        if (query.StrongholdCarriers)
        {
            if (power is null)
            {
                return Refused(UnpledgedCarriers);
            }

            if (OnlyThatPower(query, power) is not { } narrowed)
            {
                return Refused($"You can dock only at {power}'s Stronghold Carriers.");
            }

            query = narrowed;
        }

        // Page 1 asks for three times Size so dropped carriers do not leave it short; a later page asks for
        // exactly that service page at Size.
        var later = query.Page > 1;

        var asked = await inner
            .FindStationsAsync(
                later ? query : query with { Size = Math.Min(GalaxyQuery.MaxSize, query.Size * OverAsk) },
                cancellationToken)
            .ConfigureAwait(false);

        bool Drops(StationSummary station) => station.IsOtherPowersStrongholdCarrier(power);

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
            Pledged = power is not null,
            More = asked.More || read < asked.Stations.Count,
        };
    }

    private static StationSearchResult Refused(string sentence) =>
        new(null, 0, []) { Refusal = sentence };

    /// <summary>The query restricted to <paramref name="power"/>'s systems, or null when its own power filter excludes that Power.</summary>
    private static StationQuery? OnlyThatPower(StationQuery query, string power)
    {
        var named = query.Criteria.Where(criterion => criterion.Filter.Name == "power").ToList();

        if (named.Count == 0)
        {
            return query with
            {
                Criteria =
                [
                    .. query.Criteria,
                    new GalaxyCriterion { Filter = GalaxyFilters.Find("power")!, Choices = [power] },
                ],
            };
        }

        if (named.Any(criterion => !criterion.Choices.Contains(power, StringComparer.OrdinalIgnoreCase)))
        {
            return null;
        }

        return query with
        {
            Criteria =
            [
                .. query.Criteria.Except(named),
                .. named.Select(criterion => criterion with { Choices = [power] }),
            ],
        };
    }

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
        var power = pledge()?.Power;

        var found = await inner
            .FindOutfittersAsync(items, referenceSystem, maxDistance, minPad, maxReportAgeHours, cancellationToken)
            .ConfigureAwait(false);

        return [.. found.Where(outfitter => !outfitter.Station.IsOtherPowersStrongholdCarrier(power))];
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
        var power = pledge()?.Power;

        var scan = await inner.FindMissionStationsAsync(query, cancellationToken).ConfigureAwait(false);

        return scan with
        {
            Systems =
            [
                .. scan.Systems.Select(system => system with
                {
                    Stations = [.. system.Stations.Where(station => !station.IsOtherPowersStrongholdCarrier(power))],
                }),
            ],
        };
    }

    public Task<FactionReport> FactionAsync(string faction, CancellationToken cancellationToken, bool refresh = false, IProgress<FactionProgress>? progress = null) =>
        inner.FactionAsync(faction, cancellationToken, refresh, progress);
}
