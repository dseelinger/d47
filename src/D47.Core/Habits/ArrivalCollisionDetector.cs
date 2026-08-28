using D47.Core.Journal;

namespace D47.Core.Habits;

/// <summary>
/// Hull damage taken on arrival with nobody shooting (Phase 32).
/// <para>
/// <b>The strongest signal in the phase, and the one the checklist could not have named.</b> The
/// Commander asked for a warning about missing the game's own impact warnings on a set-and-forget
/// approach. There is no impact, proximity or collision event in the journal and there never has
/// been — the same shape as Phase 15's contacts-panel finding — so the warning cannot be observed.
/// <b>The consequence can.</b> Over 914 journals this fires 31 times, eighteen of them within
/// seconds of dropping out of supercruise, and three of the Commander's twenty-three deaths are
/// these with no attacker anywhere in the window.
/// </para>
/// <para>
/// <b>Nobody shooting is the whole of the inference</b>, and it is made backwards rather than
/// forwards. Elite writes <c>UnderAttack</c> when something starts firing, so an attack precedes
/// its damage; a hit with no attack behind it inside <see cref="Peace"/> is the Commander having
/// hit something. Looking forwards would need a lookahead the fold does not have and would buy
/// nothing, because damage does not arrive before the shot that caused it.
/// </para>
/// <para>
/// <b>One collision is one occurrence, not four.</b> Flying into a station writes a hull-damage
/// event per hit — the worst of these wrote four in the same second, ending in a death — and
/// counting each would inflate a single mistake into a habit. Hits inside
/// <see cref="OneAccident"/> of each other collapse.
/// </para>
/// </summary>
public sealed class ArrivalCollisionDetector : HabitDetector
{
    /// <summary>How long after an attack a hull hit is still assumed to be that attack's.</summary>
    public static readonly TimeSpan Peace = TimeSpan.FromMinutes(2);

    /// <summary>How close two hull hits have to be to be the same accident.</summary>
    public static readonly TimeSpan OneAccident = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long after arriving somewhere a hit still counts as being about the arrival. Generous,
    /// because docking approaches are minutes long and the whole point is the last of them.
    /// </summary>
    public static readonly TimeSpan Arriving = TimeSpan.FromMinutes(5);

    private DateTimeOffset _attackedAt = DateTimeOffset.MinValue;
    private DateTimeOffset _arrivedAt = DateTimeOffset.MinValue;
    private DateTimeOffset _countedAt = DateTimeOffset.MinValue;

    public override string Key => "arrival-collision";

    public override string Subject => "flying into things on the way in";

    protected override string Observation =>
        "You have put your hull into something on the way in, with nobody shooting at you";

    protected override HabitOccasion Occasion => HabitOccasion.ArrivingAtAStation;

    protected override string Denominator => "arrivals";

    public override void Observe(JournalEvent journalEvent)
    {
        var at = journalEvent.Timestamp;

        switch (journalEvent.Kind)
        {
            // An arrival, and the denominator. Every drop out of supercruise is a chance to arrive
            // badly, which is what makes the rate an honest one.
            case "SupercruiseExit":
                Opportunities++;
                _arrivedAt = at;
                return;

            // Not opportunities — approaching a planet and touching down are stages of an arrival
            // already counted — but they do extend the window, because a glide and a landing are
            // where the other thirteen of these happened.
            case "ApproachBody":
            case "Touchdown":
            case "Liftoff":
            case "DockingGranted":
                _arrivedAt = at;
                return;

            case "UnderAttack":
            case "Interdicted":
            case "Scanned":
                _attackedAt = at;
                return;

            case "HullDamage":
                break;

            default:
                return;
        }

        // A fighter's hull or an NPC crew member flying it is not the Commander's mistake.
        if (journalEvent.Raw.Bool("Fighter") || !PlayerPiloted(journalEvent))
        {
            return;
        }

        if (at - _attackedAt <= Peace || at - _arrivedAt > Arriving)
        {
            return;
        }

        if (at - _countedAt <= OneAccident)
        {
            return;
        }

        _countedAt = at;
        Occurred(at);
    }

    /// <summary>
    /// Whether the Commander was flying. <c>PlayerPilot</c> is absent on older lines, and absent
    /// means the Commander — the event predates multi-crew ever having written it otherwise.
    /// </summary>
    private static bool PlayerPiloted(JournalEvent journalEvent) =>
        journalEvent.Raw.TryGetProperty("PlayerPilot", out var pilot)
            ? pilot.ValueKind != System.Text.Json.JsonValueKind.False
            : true;
}
