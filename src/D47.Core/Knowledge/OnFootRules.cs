namespace D47.Core.Knowledge;

/// <summary>
/// The fixed numbers Elite applies on foot, hand-written for the same reason
/// <see cref="EngineeringRules"/> is: these change when the <em>game</em> changes, and a routine
/// refresh of a generated table must not be able to move them.
/// <para>
/// <b>Two axes, and neither one is the ship model.</b> Grade 1 to 5 is bought at Pioneer Supplies
/// for a fixed material list and a fixed price — no roll, no quality, nothing random. Modifications
/// come from an engineer and are ungraded, binary and permanent. So none of the floor-and-rolls
/// hedging the ship side needs applies here, and every figure below is exact.
/// </para>
/// <para>
/// Measured against a 912-journal corpus — see <c>docs/spikes/journal-corpus-on-foot.md</c>, which
/// is where all four of these come from.
/// </para>
/// </summary>
public static class OnFootRules
{
    /// <summary>
    /// Modification slots on an item at a grade. <b>Grade 1 has none.</b>
    /// <para>
    /// This is the ordering constraint ships have no analogue for, and it is why a plan says
    /// "upgrade first" as a step rather than as a footnote: a grade 1 item cannot be modified at
    /// all, so a trip to an engineer before Pioneer Supplies is a wasted trip.
    /// </para>
    /// </summary>
    public static int SlotsAt(int grade) => grade is < 1 or > 5 ? 0 : grade - 1;

    /// <summary>The most any item can ever carry, and the reason a wrong choice is permanent.</summary>
    public const int MaxSlots = 4;

    /// <summary>The grade at which an item can first be modified at all.</summary>
    public const int ModifiableFrom = 2;

    /// <summary>
    /// What reaching a grade costs in credits: the item's grade 1 price times a fixed multiplier,
    /// or null for a grade that is not bought.
    /// <para>
    /// <b>Per step, not cumulative</b> — so grade 1 to 5 in full is 99 times the base price.
    /// Measured exactly on 12 weapon upgrades across five weapons and all four grades, and on 4
    /// suit upgrades at grades 4 and 5.
    /// </para>
    /// <para>
    /// <b>Buying a higher grade outright is priced differently and must not be conflated</b>: a
    /// class 2 suit sells for 750,000 where upgrading into grade 2 costs 600,000.
    /// </para>
    /// </summary>
    public static int? UpgradeMultiplier(int grade) => grade switch
    {
        2 => 4,
        3 => 15,
        4 => 30,
        5 => 50,
        _ => null,
    };

    /// <summary>
    /// Credits to take an item from <paramref name="from"/> to <paramref name="to"/>, or null when
    /// the base price is unknown or the grades are not a climb.
    /// <para>
    /// Null rather than a partial sum, because the base price is blank for six of the eleven
    /// weapons — nothing permissive publishes them — and a total missing one step reads exactly
    /// like a total that is not.
    /// </para>
    /// </summary>
    public static long? UpgradeCost(long? basePrice, int from, int to)
    {
        if (basePrice is not { } price || from is < 1 or > 5 || to is < 1 or > 5 || to <= from)
        {
            return null;
        }

        var total = 0L;

        for (var grade = from + 1; grade <= to; grade++)
        {
            if (UpgradeMultiplier(grade) is not { } multiplier)
            {
                return null;
            }

            total += price * multiplier;
        }

        return total;
    }

    /// <summary>
    /// How much of one category the ship locker holds: <b>1,000, per category rather than per
    /// item</b>.
    /// <para>
    /// The sources conflicted and the game settles it. Across 28,148 locker snapshots, Components
    /// total exactly 1,000 in 7,931 of them and never once more, while the largest single component
    /// ever held is 94 — which refutes a per-item cap outright, since that would allow a category
    /// total in the tens of thousands.
    /// </para>
    /// <para>
    /// Stated with its limit: only Components was ever filled, so for the other three the number is
    /// the mechanic rather than an observation.
    /// </para>
    /// </summary>
    public const int LockerCapacityPerCategory = 1000;

    /// <summary>
    /// The separate cap Consumables carry, <b>per item</b> rather than per category. Six kinds,
    /// every one observed topping out at exactly this, and a category total topping out at 600.
    /// </summary>
    public const int ConsumableCapacityPerItem = 100;

    /// <summary>
    /// What the Bartender gives for a trade: offered barter value in, wanted item out, rounded
    /// down.
    /// <para>
    /// <b>This is arithmetic where the ship material trader's rate never was.</b> The Bartender
    /// takes Components for a fixed barter value each and sells Components for a fixed barter cost,
    /// and both numbers are published per material. Exact on 49 of 49 real trades.
    /// </para>
    /// <para>
    /// <b>Leftover value is lost.</b> Crediting the remainder to the next trade at the same market
    /// was modelled and refused — it drops the score from 49 to 30. Each trade is priced on its own.
    /// </para>
    /// <para>
    /// Only Components can be bartered at all. Items and Data are sold for credits, and illegal
    /// goods are refused outside Anarchy-controlled systems.
    /// </para>
    /// </summary>
    public static int? BarterYield(int offeredValue, int? wantedCost) =>
        offeredValue < 0 || wantedCost is not { } cost || cost <= 0 ? null : offeredValue / cost;

    /// <summary>
    /// How much of <paramref name="offered"/> to hand over for <paramref name="wanted"/> units of
    /// something, or null when either side cannot be bartered.
    /// <para>
    /// Rounded up, and the rounding is the Commander's loss rather than the Bartender's: whatever
    /// value the last unit does not consume is gone.
    /// </para>
    /// </summary>
    public static int? BarterCostOf(int wanted, int? offeredValue, int? wantedCost) =>
        wanted <= 0 || offeredValue is not { } value || value <= 0 || wantedCost is not { } cost
        || cost <= 0
            ? null
            : (int)Math.Ceiling((double)wanted * cost / value);
}
