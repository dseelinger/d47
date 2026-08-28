using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// Interdiction, shields, hull, heat and a full hold (Phase 8, "Call out danger without
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
    /// Whether the ship the Commander is sitting in has no shield generator fitted, so a
    /// shields-down report is the ship rather than an event (remediation.md 17, item 6).
    /// <para>
    /// Reported as *"no need to announce this on a ship without shields"*. A mining, exploration
    /// or hauling build routinely flies unshielded, and the flag is then false for the whole
    /// session; the edge into it is crossed on boarding, which is not a moment anything dangerous
    /// happened. Measured in the 916-journal corpus: <b>527 of 2,853 Loadouts fit no generator</b>,
    /// across 22 different ships.
    /// </para>
    /// <para>
    /// <b>Only in the ship, and this is the half the corpus had to be asked about.</b> A
    /// <c>ShieldState</c> event does not always describe the ship: the Commander's Hauler carries
    /// no generator and an SRV bay, and it reports shields going down <em>and coming back</em> —
    /// those are the SRV's shields, which are real and can be shot away. 22 such events under one
    /// hull that has never had a generator, plus 12 more inside an explicit <c>LaunchSRV</c>. So
    /// the ship's loadout answers for the ship and for nothing else, and in an SRV or a fighter
    /// the warning stands.
    /// </para>
    /// <para>
    /// <b>Every unknown says the warning.</b> A loadout not yet read, an empty module list, a
    /// status that has not been seen — each of those means d47 cannot show the ship has no
    /// shields, and a missed real shields-down call costs far more than one spurious line. This
    /// suppresses only where the evidence is positive.
    /// </para>
    /// </summary>
    private static bool HasNoShields(CalloutContext context) =>
        context.Status.IsKnown
        && context.Status.InShip
        && context.State?.Ship is { IsKnown: true, Modules.Count: > 0 } loadout
        && !loadout.Modules.Any(module =>
            module.Item.StartsWith("int_shieldgenerator", StringComparison.OrdinalIgnoreCase));

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

                case "ShieldState" when !journalEvent.Bool("ShieldsUp") && !HasNoShields(context):
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

        // The edge is still recorded when the ship has no shields — only the saying of it is
        // declined (remediation.md 17, item 6). Skipping the assignment would leave the field
        // remembering a ship the Commander is no longer in, and the first real loss on the next
        // shielded hull would then be the edge that never came.
        if (_shieldsWereUp && !shieldsUp && !context.IsPriming && !HasNoShields(context))
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
