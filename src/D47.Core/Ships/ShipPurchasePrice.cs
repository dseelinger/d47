using System.Globalization;
using D47.Core.Knowledge;

namespace D47.Core.Ships;

/// <summary>What a <see cref="ShipPurchases"/> list costs at list price, before selling the modules it replaces.</summary>
/// <param name="Total">The cost of the priced items.</param>
/// <param name="Priced">How many items the total covers.</param>
/// <param name="Unpriced">The display names of the items with no price, which are not in the total.</param>
public sealed record ShipPurchasePrice(long Total, int Priced, IReadOnlyList<string> Unpriced)
{
    public bool AllPriced => Unpriced.Count == 0;

    /// <summary>Prices each item: a hull at its cost plus its stock modules, a module at its cost.</summary>
    public static ShipPurchasePrice Of(ShipPurchases purchases)
    {
        ArgumentNullException.ThrowIfNull(purchases);

        long total = 0;
        var priced = 0;
        var unpriced = new List<string>();

        foreach (var purchase in purchases.Items)
        {
            var cost = purchase.Slot is null ? HullCost(purchase.Symbol) : ModuleCost(purchase.Symbol);

            if (cost is { } value)
            {
                total += value;
                priced++;
            }
            else
            {
                unpriced.Add(purchase.Item.Display);
            }
        }

        return new ShipPurchasePrice(total, priced, unpriced);
    }

    /// <summary>The figure as the panel and the adviser write it: grouped digits, invariant culture.</summary>
    public static string Credits(long amount) => amount.ToString("N0", CultureInfo.InvariantCulture);

    private static long? ModuleCost(string? symbol) => EliteSpecifications.Module(symbol)?.Cost;

    private static long? HullCost(string? symbol)
    {
        if (EliteSpecifications.Ship(symbol) is not { Cost: { } hull } ship)
        {
            return null;
        }

        var total = hull;

        foreach (var stock in ship.Stock)
        {
            if (ModuleCost(stock) is not { } cost)
            {
                return null;
            }

            total += cost;
        }

        return total;
    }
}
