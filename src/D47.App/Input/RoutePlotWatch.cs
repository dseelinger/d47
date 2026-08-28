using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>
/// One galaxy-map plotting attempt's view of NavRoute.json (Phase 10, "Galaxy Map").
/// <para>
/// Opened before the first key is sent, and it remembers when the file was last written at that
/// moment. Afterwards it polls for up to <see cref="Patience"/> and says yes only to a route
/// that ends at the named system <em>and</em> was written later than that. A route that was
/// already there is not evidence the keys did anything — on 2026-08-21 the Commander plotted a
/// route by hand, asked for the same one by voice, and heard "course plotted" for a macro that
/// had plotted nothing.
/// </para>
/// <para>
/// In the app rather than in Core because it waits, and no Core component reads the clock. It
/// polls the reader directly rather than joining the tick loop: this is a question with a
/// beginning and an end, asked by one caller, and a tick subscriber for it would outlive the
/// question by the rest of the session.
/// </para>
/// <para>
/// Null rather than false when the file never becomes readable at all — "I cannot tell" and
/// "it did not work" send the Commander to different places.
/// </para>
/// </summary>
public sealed class RoutePlotWatch : IPlotWatch
{
    /// <summary>
    /// How long to wait for the route. Elite writes NavRoute.json as the route is accepted,
    /// which is quick, but the map animates first. Six seconds is long enough to cover that and
    /// short enough that a Commander waiting on the answer has not already looked.
    /// </summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(6);

    private readonly NavRouteReader _route;
    private readonly ILogger _logger;
    private readonly TimeSpan _patience;
    private readonly DateTimeOffset? _writtenBefore;
    private readonly string? _endedAtBefore;

    public RoutePlotWatch(NavRouteReader route, ILogger logger, TimeSpan? patience = null)
    {
        _route = route;
        _logger = logger;
        _patience = patience ?? Patience;

        // The tick loop re-reads the file ten times a second, so Current is fresh enough to be
        // the "before" without a second poller racing the loop over one stamp.
        _writtenBefore = route.Current.ReadAt;
        _endedAtBefore = LastHop(route.Current);
    }

    public async Task<bool?> ConfirmAsync(string system, CancellationToken cancellationToken)
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
                    return true;
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
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

        return sawTheFile ? false : null;
    }

    private static string? LastHop(NavRoute route) =>
        route.Hops.Count == 0 ? null : route.Hops[^1].StarSystem;
}
