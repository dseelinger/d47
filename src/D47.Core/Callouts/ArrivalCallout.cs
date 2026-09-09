using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>Where you have just arrived (Phase 8, "Recognize where you have arrived" and "Home System").</summary>
public sealed class ArrivalCallout : ICallout
{
    public string Id => "arrival";

    /// <summary>The Commander's home system, as they typed it.</summary>
    public string? HomeSystem { get; set; }

    private string? _lastSystem;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.State is not { } state)
        {
            yield break;
        }

        foreach (var announcement in OnArrival(context, state))
        {
            yield return announcement;
        }
    }

    private IEnumerable<Announcement> OnArrival(CalloutContext context, CommanderGameState state)
    {
        var system = state.Location.StarSystem;

        if (system is null || string.Equals(system, _lastSystem, StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        // Updated even while priming, so the first live tick compares against where the backlog left the
        // Commander rather than against nowhere — which would announce an arrival in the system they have
        // been sitting in for an hour.
        _lastSystem = system;

        if (context.IsPriming)
        {
            yield break;
        }

        if (HomeSystem is { } home && string.Equals(system, home, StringComparison.OrdinalIgnoreCase))
        {
            yield return new Announcement("arrival.home", $"Home. Welcome back to {system}.")
            {
                Cooldown = TimeSpan.FromMinutes(5),
            };
        }

        if (state.Carrier is { Owned: true, StarSystem: { } carrierSystem } carrier &&
            string.Equals(system, carrierSystem, StringComparison.OrdinalIgnoreCase))
        {
            var name = carrier.Name ?? carrier.CallSign;

            yield return new Announcement("arrival.carrier", $"{name} is here.")
            {
                Cooldown = TimeSpan.FromMinutes(5),
            };
        }

        var stored = state.Fleet.Ships
            .Where(ship => string.Equals(ship.StarSystem, system, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (stored.Length > 0)
        {
            var what = stored.Length == 1
                ? $"{stored[0].Describe()} is stored here."
                : $"{stored.Length} of your ships are stored here.";

            yield return new Announcement("arrival.ships", what)
            {
                Cooldown = TimeSpan.FromMinutes(5),
            };
        }
    }

}
