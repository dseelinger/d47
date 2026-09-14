using System.Collections.Concurrent;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Core.Callouts;

/// <summary>On arrival, the bodies Spansh has surveyed biology on that reach the threshold.</summary>
public sealed class SurveyedBiologyCallout(ILogger<SurveyedBiologyCallout> logger) : ICallout
{
    private readonly ConcurrentQueue<Arrival> _arrived = new();

    // Tick thread only.
    private readonly HashSet<(long SystemAddress, int BodyId)> _reported = [];

    public string Id => "surveyed-biology";

    /// <summary>The service to ask, or null when galaxy search is off.</summary>
    public Func<IGalaxyService?> Galaxy { get; set; } = () => null;

    /// <summary>The least value, in credits, that is said.</summary>
    public Func<long> Threshold { get; set; } = () => BiologyCallout.DefaultThreshold;

    /// <summary>Whether another callout has already named the body.</summary>
    public Func<long, int, bool> AlreadySaid { get; set; } = (_, _) => false;

    /// <summary>Starts the lookup off the tick thread; must return without waiting for it.</summary>
    public Action<Func<Task>> Dispatch { get; set; } = work => _ = Task.Run(work);

    /// <summary>Whether this callout has named the body. Tick thread only.</summary>
    public bool Reported(long systemAddress, int bodyId) => _reported.Contains((systemAddress, bodyId));

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var here = context.State?.Location.SystemAddress;

        while (_arrived.TryDequeue(out var arrival))
        {
            if (arrival.SystemAddress == here && Announce(arrival) is { } announcement)
            {
                yield return announcement;
            }
        }

        if (context.IsPriming)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind is not ("FSDJump" or "CarrierJump")
                || journalEvent.Long("SystemAddress") is not { } systemAddress
                || Galaxy() is not { } galaxy)
            {
                continue;
            }

            var system = journalEvent.String("StarSystem");

            Dispatch(() => LookUp(galaxy, systemAddress, system));
        }
    }

    private async Task LookUp(IGalaxyService galaxy, long systemAddress, string? system)
    {
        try
        {
            var biology = await galaxy.SystemBiologyAsync(systemAddress, CancellationToken.None).ConfigureAwait(false);

            _arrived.Enqueue(new Arrival(systemAddress, system, biology));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read the surveyed biology for system {SystemAddress}", systemAddress);
        }
    }

    private Announcement? Announce(Arrival arrival)
    {
        var threshold = Threshold();

        var bodies = arrival.Biology.Bodies
            .Where(body => body.LandmarkValue >= threshold
                && !Reported(arrival.SystemAddress, body.BodyId)
                && !AlreadySaid(arrival.SystemAddress, body.BodyId))
            .OrderByDescending(body => body.LandmarkValue)
            .ToList();

        if (bodies.Count == 0)
        {
            return null;
        }

        foreach (var body in bodies)
        {
            _reported.Add((arrival.SystemAddress, body.BodyId));
        }

        var named = bodies.Select(body =>
            $"{BiologyCallout.Short(body.Name, arrival.System)} at {BiologyCallout.Credits(body.LandmarkValue)}");

        return new Announcement(
            $"surveyed-biology.{arrival.SystemAddress}",
            $"Spansh has surveyed biology here: {string.Join(", ", named)}.");
    }

    private readonly record struct Arrival(long SystemAddress, string? System, SystemBiology Biology);
}
