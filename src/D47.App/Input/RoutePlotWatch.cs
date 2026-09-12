using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>One galaxy-map plotting attempt's view of NavRoute.json (Phase 10, "Galaxy Map").</summary>
public sealed class RoutePlotWatch : IPlotWatch
{
    /// <summary>How long to wait for the route.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(6);

    private readonly NavRouteReader _route;
    private readonly ILogger _logger;
    private readonly Func<string?> _currentSystem;
    private readonly TimeSpan _patience;
    private readonly DateTimeOffset? _writtenBefore;
    private readonly string? _endedAtBefore;

    public RoutePlotWatch(NavRouteReader route, ILogger logger, Func<string?> currentSystem, TimeSpan? patience = null)
    {
        _route = route;
        _logger = logger;
        _currentSystem = currentSystem;
        _patience = patience ?? Patience;

        // The tick loop re-reads the file ten times a second, so Current is fresh enough to be the "before"
        // without a second poller racing the loop over one stamp.
        _writtenBefore = route.Current.ReadAt;
        _endedAtBefore = LastHop(route.Current);
    }

    public async Task<PlotConfirmation> ConfirmAsync(string system, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.Now + _patience;
        var sawTheFile = false;

        while (DateTimeOffset.Now < deadline)
        {
            _route.Poll();
            var current = _route.Current;

            if (current.ReadAt is { } written)
            {
                sawTheFile = true;

                var fresh = _writtenBefore is null || written > _writtenBefore;

                if (fresh && string.Equals(LastHop(current), system, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation(
                        "Plot check: a route to {System} was written at {Written} (file was last written {Before}, ending at {EndedBefore})",
                        system,
                        written,
                        _writtenBefore,
                        _endedAtBefore ?? "nothing");

                    var progress = RouteProgress.For(current, _currentSystem());

                    return new PlotConfirmation(true, progress.JumpsRemaining, progress.DistanceRemaining);
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return new PlotConfirmation(null, null, null);
            }
        }

        var after = _route.Current;

        _logger.LogInformation(
            "Plot check: no new route to {System} within {Patience}s; file {Readable}, last written {Written} (was {Before}), ends at {EndedAfter}",
            system,
            _patience.TotalSeconds,
            sawTheFile ? "readable" : "never readable",
            after.ReadAt,
            _writtenBefore,
            LastHop(after) ?? "nothing");

        return new PlotConfirmation(sawTheFile ? false : null, null, null);
    }

    /// <summary>Where the route already went when this watch was opened.</summary>
    public string? EndsAt => _endedAtBefore;

    /// <summary>
    /// The route file as it stands right now, for the input trace to record beside the keys (#365).
    /// </summary>
    public string Describe()
    {
        var current = _route.Current;

        return $"written {current.ReadAt?.ToString("O") ?? "never"}, ends at {LastHop(current) ?? "nothing"}";
    }

    private static string? LastHop(NavRoute route) =>
        route.Hops.Count == 0 ? null : route.Hops[^1].StarSystem;
}
