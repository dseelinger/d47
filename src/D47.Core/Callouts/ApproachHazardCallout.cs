using System.Collections.Concurrent;
using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Core.Callouts;

/// <summary>On every approach to a body, high surface gravity or heat, from the scan or else the galaxy record.</summary>
public sealed class ApproachHazardCallout(ILogger<ApproachHazardCallout> logger) : ICallout
{
    private const double StandardGravity = 9.80665;

    // Tick thread only.
    private readonly HashSet<long> _asked = [];
    private readonly List<(long SystemAddress, int BodyId)> _waiting = [];

    // Written by the lookup, read on the tick; a null profile is a system the record does not have.
    private readonly ConcurrentDictionary<long, StarSystemProfile?> _profiles = new();

    public string Id => "approach-hazard";

    /// <summary>The service to ask, or null when there is none.</summary>
    public Func<IStarSystemService?> StarSystems { get; set; } = () => null;

    /// <summary>Starts the lookup off the tick thread; must return without waiting for it.</summary>
    public Action<Func<Task>> Dispatch { get; set; } = work => _ = Task.Run(work);

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var here = context.State?.Location.SystemAddress;

        _waiting.RemoveAll(entry => entry.SystemAddress != here);

        if (context.IsPriming || context.State is not { } state)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind is "Touchdown" or "LeaveBody")
            {
                var landed = journalEvent.Int("BodyID");
                _waiting.RemoveAll(entry => landed is null || entry.BodyId == landed);
                continue;
            }

            if (journalEvent.Kind != "ApproachBody"
                || journalEvent.Long("SystemAddress") is not { } systemAddress
                || journalEvent.Int("BodyID") is not { } bodyId)
            {
                continue;
            }

            if (state.Scans.For(systemAddress, bodyId) is { } scan
                && (scan.SurfaceGravity is not null || scan.SurfaceTemperature is not null))
            {
                var heavy = scan.SurfaceGravity / StandardGravity > 1.0;
                var gravity = heavy ? TargetAnswer.GravityFromScan(scan.SurfaceGravity!.Value) : null;

                if (Warn(systemAddress, bodyId, scan.BodyName, heavy, gravity, scan.SurfaceTemperature) is { } fromScan)
                {
                    yield return fromScan;
                }

                continue;
            }

            if (StarSystems() is not { } service || _waiting.Contains((systemAddress, bodyId)))
            {
                continue;
            }

            _waiting.Add((systemAddress, bodyId));

            if (!_profiles.ContainsKey(systemAddress) && _asked.Add(systemAddress))
            {
                Dispatch(() => LookUp(service, systemAddress));
            }
        }

        for (var i = _waiting.Count - 1; i >= 0; i--)
        {
            var (address, bodyId) = _waiting[i];

            if (!_profiles.TryGetValue(address, out var profile))
            {
                continue;
            }

            _waiting.RemoveAt(i);

            if (profile?.Bodies.FirstOrDefault(body => body.BodyId == bodyId) is { } body
                && Warn(
                    address,
                    bodyId,
                    body.Name,
                    body.Gravity > 1.0,
                    body.Gravity is { } g ? TargetAnswer.Gravity(g) : null,
                    body.SurfaceTemperature) is { } announcement)
            {
                yield return announcement;
            }
        }
    }

    private async Task LookUp(IStarSystemService service, long address)
    {
        try
        {
            _profiles[address] = await service.ProfileAsync(address, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read the galaxy record of system {SystemAddress} for a landing hazard", address);
            _profiles[address] = null;
        }
    }

    private static Announcement? Warn(long systemAddress, int bodyId, string name, bool heavy, string? gravity, double? kelvin)
    {
        var hot = SurfaceHeat.IsHostile(kelvin, landable: true);
        if (!heavy && !hot)
        {
            return null;
        }

        var heat = hot ? kelvin!.Value.ToString("N0", CultureInfo.InvariantCulture) + " K" : null;
        var text = (heavy, hot) switch
        {
            (true, true) => $"High gravity and a hot surface on {name}: {gravity}, {heat}.",
            (true, false) => $"High gravity on {name}: {gravity}.",
            _ => $"Hot surface on {name}: {heat}.",
        };

        return new Announcement($"approach-hazard.{systemAddress}.{bodyId}", text);
    }
}
