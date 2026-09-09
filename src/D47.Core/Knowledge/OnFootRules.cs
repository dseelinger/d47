namespace D47.Core.Knowledge;

/// <summary>
/// The fixed numbers Elite applies on foot, hand-written for the same reason <see
/// cref="EngineeringRules"/> is: these change when the game changes, and a routine refresh of a
/// generated table must not be able to move them.
/// </summary>
public static class OnFootRules
{
    /// <summary>Modification slots on an item at a grade.</summary>
    public static int SlotsAt(int grade) => grade is < 1 or > 5 ? 0 : grade - 1;

    /// <summary>The most any item can ever carry, and the reason a wrong choice is permanent.</summary>
    public const int MaxSlots = 4;

    /// <summary>The grade at which an item can first be modified at all.</summary>
    public const int ModifiableFrom = 2;

    /// <summary>
    /// What reaching a grade costs in credits: the item's grade 1 price times a fixed multiplier, or
    /// null for a grade that is not bought.
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
    /// Credits to take an item from <paramref name="from"/> to <paramref name="to"/>, or null when the
    /// base price is unknown or the grades are not a climb.
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
    /// How much of one category the ship locker holds: 1,000, per category rather than per item.
    /// </summary>
    public const int LockerCapacityPerCategory = 1000;

    /// <summary>The separate cap Consumables carry, per item rather than per category.</summary>
    public const int ConsumableCapacityPerItem = 100;

    /// <summary>
    /// What the Bartender gives for a trade: offered barter value in, wanted item out, rounded down.
    /// </summary>
    public static int? BarterYield(int offeredValue, int? wantedCost) =>
        offeredValue < 0 || wantedCost is not { } cost || cost <= 0 ? null : offeredValue / cost;

    /// <summary>
    /// How much of <paramref name="offered"/> to hand over for <paramref name="wanted"/> units of
    /// something, or null when either side cannot be bartered.
    /// </summary>
    public static int? BarterCostOf(int wanted, int? offeredValue, int? wantedCost) =>
        wanted <= 0 || offeredValue is not { } value || value <= 0 || wantedCost is not { } cost
        || cost <= 0
            ? null
            : (int)Math.Ceiling((double)wanted * cost / value);
}
