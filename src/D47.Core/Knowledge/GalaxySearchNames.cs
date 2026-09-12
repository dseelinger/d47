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

    public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
        inner.FindStationsAsync(query, cancellationToken);

    public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
        inner.FindBodiesAsync(query, cancellationToken);

    public Task<ColonisationScan> ScanForColonisationAsync(
        ColonisationQuery query, CancellationToken cancellationToken) =>
        inner.ScanForColonisationAsync(query, cancellationToken);
}
