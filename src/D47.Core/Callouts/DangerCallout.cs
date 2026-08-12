using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// Interdiction, shields, hull, heat and a full hold (list.md Phase 8, "Call out danger without
/// waiting for a turn").
/// <para>
/// <b>These fire on the event, never at the model's discretion.</b> That is the checklist's
/// wording and it is the design: an alert routed through a turn is an alert that arrives after
/// the model has finished thinking, which for an interdiction is after it is over. Nothing here
/// consults the language model, and nothing here can be talked out of firing by anything in the
/// journal — which matters, because journal content is untrusted (architecture.md §7).
/// </para>
/// <para>
/// Two sources, deliberately. The journal reports transitions — shields went down, hull was
/// hit — and Status.json reports conditions — shields are still down, fuel is still low. A
/// warning built on events alone goes quiet the moment the game stops repeating itself.
/// </para>
/// </summary>
public sealed class DangerCallout : ICallout
{
    public string Id => "danger";

    /// <summary>
    /// Long enough that a firefight does not become a monologue, short enough that a second
    /// genuine emergency is not swallowed by the first.
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
    /// Transitions. Each of these is a thing that just happened, which is why they are worth
    /// interrupting for even though the condition may already have passed.
    /// </summary>
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

                    // Submitting is a choice the Commander made. Announcing it back to them as
                    // an emergency is noise.
                    if (journalEvent.Bool("Submitted"))
                    {
                        continue;
                    }

                    yield return Urgent(
                        "danger.interdicted",
                        by is null
                            ? "Interdiction. Someone has pulled us out of supercruise."
                            : $"Interdicted by {pilot} {by}.");
                    break;
                }

                case "HullDamage":
                {
                    // Fighter and crew damage arrive on the same event. The Commander's own hull
                    // is the one worth interrupting for.
                    if (journalEvent.Bool("Fighter") && !journalEvent.Bool("PlayerPilot"))
                    {
                        continue;
                    }

                    var health = journalEvent.Double("Health");

                    yield return Urgent(
                        "danger.hull",
                        health is { } fraction
                            ? $"Hull damage. {Math.Round(fraction * 100)} percent integrity."
                            : "Hull damage.");
                    break;
                }

                case "HeatDamage":
                    yield return Urgent("danger.heat", "Taking heat damage.");
                    break;

                case "ShieldState" when !journalEvent.Bool("ShieldsUp"):
                    yield return Urgent("danger.shields", "Shields are down.");
                    break;

                case "UnderAttack":
                    yield return Urgent("danger.attack", "We are under attack.");
                    break;

                case "Died":
                    // Not urgent in the interrupting sense — there is nothing left to warn
                    // about — but worth saying rather than letting the rebuy screen speak for
                    // itself.
                    yield return new Announcement(
                        "danger.died", "We have been destroyed.", CalloutUrgency.Routine)
                    {
                        Cooldown = Cooldown,
                    };
                    break;
            }
        }
    }

    /// <summary>
    /// Conditions, each announced on the edge into the condition rather than while it holds.
    /// Status.json is rewritten several times a second, so announcing on the level rather than
    /// the edge would be a warning per tick.
    /// </summary>
    private IEnumerable<Announcement> FromStatus(CalloutContext context)
    {
        var status = context.Status;

        if (!status.IsKnown)
        {
            yield break;
        }

        // Only in the ship. On foot these flags mean something else or nothing at all, and a
        // shields-down warning while walking around a concourse is noise.
        if (!status.InShip)
        {
            yield break;
        }

        var shieldsUp = status.ShieldsUp;

        if (_shieldsWereUp && !shieldsUp && !context.IsPriming)
        {
            yield return Urgent("danger.shields", "Shields are down.");
        }

        _shieldsWereUp = shieldsUp;

        var overheating = status.Has(StatusFlags.Overheating);

        if (!_wasOverheating && overheating && !context.IsPriming)
        {
            yield return Urgent("danger.overheat", "Overheating.");
        }

        _wasOverheating = overheating;

        var interdicted = status.Has(StatusFlags.BeingInterdicted);

        if (!_wasBeingInterdicted && interdicted && !context.IsPriming)
        {
            // Ahead of the Interdicted event, which Elite writes once the pull succeeds. This is
            // the warning that arrives while there is still something to do about it.
            yield return Urgent("danger.interdiction", "We are being interdicted.");
        }

        _wasBeingInterdicted = interdicted;

        // A full hold, which is a danger only in the sense that it silently stops the thing the
        // Commander is doing. Capacity comes from the Loadout event; Status.json reports the
        // tonnage and never the capacity.
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

    private static Announcement Urgent(string key, string text) =>
        new(key, text, CalloutUrgency.Urgent) { Cooldown = Cooldown };
}
