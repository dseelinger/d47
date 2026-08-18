using D47.Core.Journal;

namespace D47.Core.Habits;

/// <summary>
/// Dying on foot, at a settlement, to things that were already there (list.md Phase 32).
/// <para>
/// <b>The Commander called this "poking the bear" and the journals agree.</b> Eleven of twenty-three
/// deaths in thirteen months are to suit AI or to a settlement's own turrets, and they do not spread
/// out: five of them fall on a single afternoon, and three other days carry two each. That
/// clustering is the claim — not that walking into a settlement is dangerous, but that this
/// Commander goes back in.
/// </para>
/// <para>
/// <b>The denominator is deaths rather than settlement visits</b>, and that is deliberate. Counting
/// visits would produce a reassuringly tiny rate over hundreds of harmless approaches and would
/// answer a question nobody asked. The question is what kills this Commander, and the honest
/// denominator for that is everything that has.
/// </para>
/// <para>
/// The killer is read from <c>KillerShip</c>, which for on-foot deaths is a suit or turret symbol
/// — <c>rangedsuitai_class3</c>, <c>ps_turretbasesmall_3m</c> — rather than a hull. A symbol match
/// rather than a name match, for the reason the Phase 15 research recorded about NPC comms: the
/// symbols are a closed vocabulary and immune to the game's language setting.
/// </para>
/// </summary>
public sealed class OnFootDeathDetector : HabitDetector
{
    /// <summary>
    /// What an on-foot killer's symbol contains. Suit AI of every class, and the fixed defences a
    /// settlement brings to the argument.
    /// </summary>
    private static readonly string[] OnFootKillers = ["suitai", "turret"];

    public override string Key => "on-foot-death";

    public override string Subject => "dying on foot at settlements";

    protected override string Observation => "Settlement security has killed you on foot";

    protected override HabitOccasion Occasion => HabitOccasion.ArrivingOnFoot;

    protected override string Denominator => "deaths";

    public override void Observe(JournalEvent journalEvent)
    {
        if (!string.Equals(journalEvent.Kind, "Died", StringComparison.Ordinal))
        {
            return;
        }

        Opportunities++;

        var killer = journalEvent.Raw.String("KillerShip");

        if (killer is not null &&
            OnFootKillers.Any(symbol => killer.Contains(symbol, StringComparison.OrdinalIgnoreCase)))
        {
            Occurred(journalEvent.Timestamp);
        }
    }
}
