using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// Interdiction, shields, hull, heat and a full hold (Phase 8, "Call out danger without waiting for a
/// turn").
/// </summary>
public sealed class DangerCallout : ICallout
{
    public string Id => "danger";

    /// <summary>
    /// Long enough that a firefight does not become a monologue, short enough that a second genuine
    /// emergency is not swallowed by the first.
    /// </summary>
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    private bool _shieldsWereUp = true;
    private bool _wasOverheating;
    private bool _wasBeingInterdicted;
    private bool _holdWasFull;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        foreach (var announcement in FromEvents(context))
        {
            yield return announcement;
        }

        foreach (var announcement in FromStatus(context))
        {
            yield return announcement;
        }
    }

    /// <summary>
    /// Whether the ship the Commander is sitting in has no shield generator fitted, so a shields-down
    /// report is the ship rather than an event (remediation.md 17, item 6).
    /// </summary>
    private static bool HasNoShields(CalloutContext context) =>
        context.Status.IsKnown
        && context.Status.InShip
        && context.State?.Ship is { IsKnown: true, Modules.Count: > 0 } loadout
        && !loadout.Modules.Any(module =>
            module.Item.StartsWith("int_shieldgenerator", StringComparison.OrdinalIgnoreCase));

    /// <summary>Transitions.</summary>
    private static IEnumerable<Announcement> FromEvents(CalloutContext context)
    {
        foreach (var journalEvent in context.Events)
        {
            switch (journalEvent.Kind)
            {
                case "Interdicted":
                {
                    var by = journalEvent.String("Interdictor");
                    var pilot = journalEvent.Bool("IsPlayer") ? "Commander" : "NPC";

                    // Submitting is a choice the Commander made.
                    if (journalEvent.Bool("Submitted"))
                    {
                        continue;
                    }

                    yield return Urgent(
                        "danger.interdicted",
                        by is null
                            ? "Interdiction. Someone has pulled us out of supercruise."
                            : $"Interdicted by {pilot} {by}.",
                        AlertCue.Interdiction);
                    break;
                }

                case "HullDamage":
                {
                    // Fighter and crew damage arrive on the same event.
                    if (journalEvent.Bool("Fighter") && !journalEvent.Bool("PlayerPilot"))
                    {
                        continue;
                    }

                    var health = journalEvent.Double("Health");

                    yield return Urgent(
                        "danger.hull",
                        health is { } fraction
                            ? $"Hull damage. {Math.Round(fraction * 100)} percent integrity."
                            : "Hull damage.",
                        AlertCue.UnderFire);
                    break;
                }

                case "HeatDamage":
                    yield return Urgent("danger.heat", "Taking heat damage.", AlertCue.Overheating);
                    break;

                case "ShieldState" when !journalEvent.Bool("ShieldsUp") && !HasNoShields(context):
                    yield return Urgent("danger.shields", "Shields are down.", AlertCue.UnderFire);
                    break;

                case "UnderAttack":
                    yield return Urgent("danger.attack", "We are under attack.", AlertCue.UnderFire);
                    break;

                case "Died":
                    // Not urgent in the interrupting sense — there is nothing left to warn about — but worth
                    // saying rather than letting the rebuy screen speak for itself.
                    yield return new Announcement(
                        "danger.died", "We have been destroyed.", CalloutUrgency.Routine)
                    {
                        Cooldown = Cooldown,
                    };
                    break;
            }
        }
    }

    /// <summary>Conditions, each announced on the edge into the condition rather than while it holds.</summary>
    private IEnumerable<Announcement> FromStatus(CalloutContext context)
    {
        var status = context.Status;

        if (!status.IsKnown)
        {
            yield break;
        }

        // Only in the ship.
        if (!status.InShip)
        {
            yield break;
        }

        var shieldsUp = status.ShieldsUp;

        // The edge is still recorded when the ship has no shields — only the saying of it is declined
        // (remediation.md 17, item 6).
        if (_shieldsWereUp && !shieldsUp && !context.IsPriming && !HasNoShields(context))
        {
            yield return Urgent("danger.shields", "Shields are down.", AlertCue.UnderFire);
        }

        _shieldsWereUp = shieldsUp;

        var overheating = status.Has(StatusFlags.Overheating);

        if (!_wasOverheating && overheating && !context.IsPriming)
        {
            yield return Urgent("danger.overheat", "Overheating.", AlertCue.Overheating);
        }

        _wasOverheating = overheating;

        var interdicted = status.Has(StatusFlags.BeingInterdicted);

        if (!_wasBeingInterdicted && interdicted && !context.IsPriming)
        {
            // Ahead of the Interdicted event, which Elite writes once the pull succeeds.
            yield return Urgent("danger.interdiction", "We are being interdicted.", AlertCue.Interdiction);
        }

        _wasBeingInterdicted = interdicted;

        // A full hold, which is a danger only in the sense that it silently stops the thing the Commander is
        // doing.
        var capacity = context.State?.Ship.CargoCapacity;
        var holdFull = capacity is { } limit and > 0 && status.Cargo is { } cargo && cargo >= limit;

        if (!_holdWasFull && holdFull && !context.IsPriming)
        {
            yield return new Announcement(
                "danger.cargo", $"Cargo hold is full. {capacity} tonnes.", CalloutUrgency.Routine)
            {
                Cooldown = Cooldown,
            };
        }

        _holdWasFull = holdFull;
    }

    /// <summary>
    /// One urgent warning, with the marker that says which kind it is before the sentence has arrived
    /// (#136).
    /// </summary>
    private static Announcement Urgent(string key, string text, AlertCue cue) =>
        new(key, text, CalloutUrgency.Urgent) { Cooldown = Cooldown, Cue = cue };
}
