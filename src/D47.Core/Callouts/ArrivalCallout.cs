using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// Where you have just arrived (Phase 8, "Recognize where you have arrived" and
/// "Home System").
/// <para>
/// Everything this recognises is derived from the Commander's own data — the home system they
/// typed into settings, where their carrier is, and where their ships are stored. <b>No table of
/// engineer bases or notable stations is shipped.</b> A hardcoded list of which engineer lives
/// where is game data that goes stale on every update and that d47 has no source for; inventing
/// one would be exactly the confident wrong answer the guardrails exist to prevent. What the
/// journal states, d47 says.
/// </para>
/// <para>
/// <b>Docking used to announce that the station offered engineering, and that has been
/// removed.</b> It is not a fact about the station: 3,726 of the 3,759 dockings in the corpus
/// advertise the service, and all 33 that do not are construction depots, where nobody expects
/// it. A callout that fires on almost every docking says nothing, and inverting it would have
/// said nothing about a construction site instead.
/// </para>
/// </summary>
public sealed class ArrivalCallout : ICallout
{
    public string Id => "arrival";

    /// <summary>
    /// The Commander's home system, as they typed it. A setting rather than anything inferred:
    /// "home" is a matter of how someone feels about a place, and no journal event reports that.
    /// </summary>
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

        // Updated even while priming, so the first live tick compares against where the backlog
        // left the Commander rather than against nowhere — which would announce an arrival in
        // the system they have been sitting in for an hour.
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
