using D47.Core.Journal;

namespace D47.Core.Habits;

/// <summary>
/// Dropping out of supercruise short of where you meant to be, and going round again — the
/// manoeuvre every Commander knows by name (Phase 32).
/// <para>
/// <b>Measured before it was written: 69 of these across 914 journals, and every single one at a
/// planet.</b> Not one at a station, which is worth stating because the loop of shame is folklore
/// about stations. On this Commander's data it is a planetary-approach problem, so the callout
/// fires on <see cref="HabitOccasion.ApproachingAPlanet"/> rather than on a docking.
/// </para>
/// <para>
/// The signature is exact and needs no thresholds beyond a clock: drop out, go back to supercruise
/// within <see cref="GoingRoundAgain"/>, and drop out again <em>at the same body</em>. A jump, a
/// docking or a landing in between means the first drop was where the Commander wanted to be, and
/// clears the pending state rather than counting.
/// </para>
/// </summary>
public sealed class OvershootDetector : HabitDetector
{
    /// <summary>
    /// How quickly the Commander has to be back in supercruise for it to be an overshoot rather
    /// than a change of plan. The median gap in the corpus is 41 seconds.
    /// </summary>
    public static readonly TimeSpan GoingRoundAgain = TimeSpan.FromMinutes(4);

    private string? _droppedAt;
    private DateTimeOffset _droppedWhen;
    private bool _climbing;

    public override string Key => "overshoot";

    public override string Subject => "overshooting and going round again";

    protected override string Observation =>
        "You have dropped short of where you were going and had to come back";

    protected override HabitOccasion Occasion => HabitOccasion.ApproachingAPlanet;

    protected override string Denominator => "approaches";

    public override void Observe(JournalEvent journalEvent)
    {
        var at = journalEvent.Timestamp;

        switch (journalEvent.Kind)
        {
            case "SupercruiseExit":
                var body = journalEvent.Raw.String("Body");

                // The second drop, at the body the first one was at. That is the loop closed.
                if (_climbing && body is not null && string.Equals(body, _droppedAt, StringComparison.Ordinal))
                {
                    Occurred(at);
                }

                _climbing = false;

                // Only a destination counts as a chance to overshoot. A drop at a star is a fuel
                // stop or an emergency, and neither is a Commander missing their mark.
                if (journalEvent.Raw.String("BodyType") is "Planet" or "Station")
                {
                    Opportunities++;
                    _droppedAt = body;
                    _droppedWhen = at;
                }
                else
                {
                    _droppedAt = null;
                }

                return;

            case "SupercruiseEntry":
                _climbing = _droppedAt is not null && at - _droppedWhen <= GoingRoundAgain;
                return;

            // Arrived, or gone somewhere else entirely. Either way the drop that is pending was
            // not an overshoot.
            case "Docked":
            case "Touchdown":
            case "FSDJump":
                _droppedAt = null;
                _climbing = false;
                return;

            default:
                return;
        }
    }
}
