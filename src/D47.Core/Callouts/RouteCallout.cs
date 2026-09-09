using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Callouts;

/// <summary>
/// Progress along a plotted route (Phase 8, "Route Progress"): how many jumps are left, what the next
/// system is, and what is hazardous ahead.
/// </summary>
/// <param name="logger">
/// Where to say that neither the journal nor the remembered loadouts can describe the ship (#337).
/// </param>
public sealed class RouteCallout(ILogger? logger = null) : ICallout
{
    public string Id => "route";

    /// <summary>How often to say how far there is left to go, in jumps.</summary>
    public int EveryNJumps { get; set; } = 5;

    private string? _lastAnnouncedSystem;
    private int _jumpsSinceProgress;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.State is not { } state)
        {
            yield break;
        }

        // Counted from the events rather than from the route file's own remaining count: the file is not
        // rewritten as the Commander advances, so it cannot say how far along they are.
        var jumped = context.Events.Any(journalEvent => journalEvent.Kind == "FSDJump");

        if (!jumped)
        {
            yield break;
        }

        _jumpsSinceProgress++;

        if (context.IsPriming)
        {
            yield break;
        }

        var current = state.Location.StarSystem;
        var ahead = context.Route.Ahead(current);

        if (ahead.Count == 0)
        {
            // Arriving at the end of a plotted route is worth one line, and only one — hence the guard on the
            // system having changed.
            if (context.Route.IsPlotted && !string.Equals(_lastAnnouncedSystem, current, StringComparison.OrdinalIgnoreCase))
            {
                _lastAnnouncedSystem = current;
                _jumpsSinceProgress = 0;

                yield return new Announcement(
                    "route.arrived", $"Route complete. {current}.");
            }

            yield break;
        }

        _lastAnnouncedSystem = current;

        // A hazard on the very next jump outranks the progress report and is said whether or not this was a
        // reporting jump.
        var next = ahead[0];

        if (next.Hazardous)
        {
            yield return new Announcement(
                "route.hazard",
                $"Next jump is {StarClasses.Speak(next.StarClass)} at {next.StarSystem}. "
                + "Throttle down on arrival.",
                CalloutUrgency.Urgent)
            {
                Cooldown = TimeSpan.FromMinutes(1),
            };
        }

        if (_jumpsSinceProgress < EveryNJumps || EveryNJumps <= 0)
        {
            yield break;
        }

        _jumpsSinceProgress = 0;

        var remaining = ahead.Count;
        var report = $"{remaining} jump{(remaining == 1 ? "" : "s")} remaining. Next is {next.StarSystem}";

        // Scoopability of the next star, stated only when the class is actually known.
        var scoopable = state.Fitted(ShipLoadout.FuelScoop, "FSDJump", logger) is false
            ? null
            : next.Scoopable;

        report += scoopable switch
        {
            true => ", scoopable.",
            false => $", {StarClasses.Speak(next.StarClass)} — not scoopable.",
            null => ".",
        };

        yield return new Announcement("route.progress", report)
        {
            Cooldown = TimeSpan.FromSeconds(20),
        };

        // Hazards further out, so a neutron star four jumps away is not a surprise.
        var upcoming = ahead.Skip(1).Take(EveryNJumps).Where(hop => hop.Hazardous).ToArray();

        if (upcoming.Length > 0)
        {
            var named = string.Join(", ", upcoming.Select(hop => $"{hop.StarSystem} ({StarClasses.Speak(hop.StarClass)})"));

            yield return new Announcement("route.ahead", $"Ahead on the route: {named}.")
            {
                Cooldown = TimeSpan.FromMinutes(2),
            };
        }
    }
}

/// <summary>
/// Flavour during a longer-than-normal hyperspace jump (Phase 8, "Remark on an unusually long jump").
/// </summary>
public sealed class LongJumpCallout : ICallout
{
    public string Id => "long-jump";

    /// <summary>Configurable, defaulting to the 20 seconds the checklist specifies.</summary>
    public TimeSpan Threshold { get; set; } = TimeSpan.FromSeconds(20);

    private DateTimeOffset? _enteredHyperspaceAt;
    private bool _remarked;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        foreach (var journalEvent in context.Events)
        {
            // Hyperspace only.
            if (journalEvent.Kind == "StartJump" && journalEvent.String("JumpType") == "Hyperspace")
            {
                _enteredHyperspaceAt = context.Now;
                _remarked = false;
            }
            else if (journalEvent.Kind == "FSDJump")
            {
                // Arrived.
                _enteredHyperspaceAt = null;
                _remarked = false;
            }
        }

        if (_remarked || context.IsPriming || _enteredHyperspaceAt is not { } entered)
        {
            yield break;
        }

        var elapsed = context.Now - entered;

        if (elapsed < Threshold)
        {
            yield break;
        }

        _remarked = true;

        var destination = context.State?.Location.NextJumpSystem;

        yield return new Announcement(
            "jump.long",
            destination is null
                ? $"That is a long one. {(int)elapsed.TotalSeconds} seconds in the tunnel so far."
                : $"A long crossing to {destination}. {(int)elapsed.TotalSeconds} seconds so far.");
    }
}
