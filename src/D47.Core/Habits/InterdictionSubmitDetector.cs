using D47.Core.Journal;

namespace D47.Core.Habits;

/// <summary>
/// What the Commander does when somebody pulls them out of supercruise (list.md Phase 32).
/// <para>
/// <b>50 submissions out of 52 across thirteen months</b>, with three escapes among the rest. That
/// is not a criticism and the line does not pretend it is one — submitting keeps the FSD cool and
/// is frequently the right call. It is a fact about how this Commander flies, delivered at the one
/// moment it is worth knowing: the instant the tug-of-war starts.
/// </para>
/// <para>
/// The cleanest detector in the phase, because Elite states the answer. <c>Interdicted</c> carries
/// <c>Submitted</c>, so there is no inference at all — just the count and the denominator the item
/// insists on, which here happen to be the same event.
/// </para>
/// </summary>
public sealed class InterdictionSubmitDetector : HabitDetector
{
    public override string Key => "interdiction-submit";

    public override string Subject => "submitting to interdictions";

    protected override string Observation => "You submit rather than run";

    protected override HabitOccasion Occasion => HabitOccasion.BeingInterdicted;

    protected override string Denominator => "interdictions";

    public override void Observe(JournalEvent journalEvent)
    {
        if (!string.Equals(journalEvent.Kind, "Interdicted", StringComparison.Ordinal))
        {
            return;
        }

        Opportunities++;

        if (journalEvent.Raw.Bool("Submitted"))
        {
            Occurred(journalEvent.Timestamp);
        }
    }
}
