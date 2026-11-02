using D47.Core.Knowledge;

namespace D47.App.Tests;

/// <summary>Every member throws; a fake overrides the ones its test uses.</summary>
public class StubGalaxyService : IGalaxyService
{
    public virtual Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<ColonisationScan> ScanForColonisationAsync(
        ColonisationQuery query,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(
        long systemAddress,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<MissionStationScan> FindMissionStationsAsync(
        MissionStationQuery query,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

/// <summary>Every member throws; a fake overrides the ones its test uses.</summary>
public class StubStarSystemService : IStarSystemService
{
    public virtual Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<PowerplayNeighbourhood> PowerplayNearAsync(
        string system,
        double lightYears,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
