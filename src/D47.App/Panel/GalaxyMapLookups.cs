using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Panel;

/// <summary>Where a system's position lookup stands.</summary>
public enum GalaxyMapLookup
{
    NotAsked,
    Waiting,
    Failed,
    Found,
    Unknown,
}

/// <summary>
/// The Galaxy map's galaxy search answers for the session (#1409): the systems nearest each reference system, and
/// positions found by name. Requests run on the thread pool, a request in flight is not sent again, and a failed one
/// is logged and may be asked again.
/// </summary>
public sealed class GalaxyMapLookups(ILogger? logger = null)
{
    /// <summary>The most positions one opening of the page asks for.</summary>
    public const int MostPerOpening = 25;

    private static readonly Dictionary<string, string> NoCriteria = [];

    private readonly ILogger _logger = logger ?? NullLogger.Instance;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Task<IReadOnlyList<SystemSummary>>> _nearby = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<StarPosition?>> _positions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _failed = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Task> _sent = [];
    private int _generation;

    /// <summary>Changes each time a request finishes, answered or not.</summary>
    public int Generation => Volatile.Read(ref _generation);

    /// <summary>Asks for the systems nearest <paramref name="system"/> unless they are held or on their way.</summary>
    public void AskNearby(IGalaxyService galaxy, string system)
    {
        lock (_gate)
        {
            if (_nearby.ContainsKey(system))
            {
                return;
            }

            var request = Task.Run(() => NearbyAsync(galaxy, system));
            _nearby[system] = request;
            _sent.Add(Landed(request));
        }
    }

    /// <summary>Asks for the positions of up to <see cref="MostPerOpening"/> of these systems, in order, that are neither found nor on their way.</summary>
    public void AskPositions(IGalaxyService galaxy, IEnumerable<string> systems)
    {
        lock (_gate)
        {
            var asked = 0;

            foreach (var system in systems)
            {
                if (asked == MostPerOpening)
                {
                    return;
                }

                if (_positions.ContainsKey(system))
                {
                    continue;
                }

                _failed.Remove(system);

                var request = Task.Run(() => PositionAsync(galaxy, system));
                _positions[system] = request;
                _sent.Add(Landed(request));
                asked++;
            }
        }
    }

    /// <summary>Every system answered for any reference system so far, once each.</summary>
    public IReadOnlyList<SystemSummary> Nearby()
    {
        lock (_gate)
        {
            return [.. _nearby.Values
                .Where(request => request.IsCompletedSuccessfully)
                .SelectMany(request => request.Result)
                .DistinctBy(system => system.Name, StringComparer.OrdinalIgnoreCase)];
        }
    }

    public (GalaxyMapLookup State, StarPosition? At) PositionOf(string system)
    {
        lock (_gate)
        {
            if (_positions.TryGetValue(system, out var request))
            {
                return !request.IsCompletedSuccessfully
                    ? (GalaxyMapLookup.Waiting, null)
                    : request.Result is { } at ? (GalaxyMapLookup.Found, at) : (GalaxyMapLookup.Unknown, null);
            }

            return (_failed.Contains(system) ? GalaxyMapLookup.Failed : GalaxyMapLookup.NotAsked, null);
        }
    }

    /// <summary>Completes when every request sent so far has finished.</summary>
    internal Task Settled()
    {
        lock (_gate)
        {
            return Task.WhenAll(_sent);
        }
    }

    /// <summary>Moves the generation on once the request has completed, so a reader that sees the new generation sees the answer.</summary>
    private Task Landed(Task request) =>
        request.ContinueWith(_ => Interlocked.Increment(ref _generation), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);

    private async Task<IReadOnlyList<SystemSummary>> NearbyAsync(IGalaxyService galaxy, string system)
    {
        try
        {
            if (!GalaxyQuery.TryParse(system, NoCriteria, GalaxyQuery.MaxSize, out var query, out _))
            {
                return [];
            }

            return (await galaxy.SearchAsync(query, CancellationToken.None).ConfigureAwait(false)).Systems;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The galaxy map could not find the systems near {System}", system);

            lock (_gate)
            {
                _nearby.Remove(system);
            }

            return [];
        }
    }

    private async Task<StarPosition?> PositionAsync(IGalaxyService galaxy, string system)
    {
        try
        {
            return await galaxy.PositionAsync(system, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The galaxy map could not find where {System} is", system);

            lock (_gate)
            {
                _positions.Remove(system);
                _failed.Add(system);
            }

            return null;
        }
    }
}
