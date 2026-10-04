using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>One Bartender exchange: hand over <see cref="Give"/> of <see cref="Offered"/> to receive <see cref="Get"/> of the wanted item.</summary>
/// <param name="Held">How many of <see cref="Offered"/> the Commander holds.</param>
public sealed record MicroResourceExchange(MaterialEntry Offered, int Give, int Get, int Held);

/// <summary>Everything the micro-resource detail answer says about one on-foot material.</summary>
/// <param name="Kind">Items, Components, Consumables or Data.</param>
/// <param name="Backpack">How many are in the backpack.</param>
/// <param name="Locker">How many are in the ship locker.</param>
/// <param name="LockerCap">The ship locker's cap for this kind: per item for Consumables, per category otherwise.</param>
/// <param name="LockerCapPerItem">Whether <see cref="LockerCap"/> applies to this item alone rather than to the category.</param>
/// <param name="LockerCategoryTotal">Everything the ship locker holds of this kind.</param>
/// <param name="InventoryKnown">Whether either inventory file has been read.</param>
/// <param name="Exchanges">What the Commander holds that pays for <see cref="Get"/> of this item, cheapest first. Empty when the Bartender does not exchange it.</param>
public sealed record MicroResourceDetail(
    MaterialEntry Entry,
    string Kind,
    int Backpack,
    int Locker,
    int LockerCap,
    bool LockerCapPerItem,
    int LockerCategoryTotal,
    bool InventoryKnown,
    IReadOnlyList<string> Settlements,
    IReadOnlyList<string> Buildings,
    IReadOnlyList<string> Containers,
    IReadOnlyList<MicroResourceExchange> Exchanges)
{
    /// <summary>Backpack and ship locker together.</summary>
    public int Held => Backpack + Locker;
}

public static class MicroResourceGuide
{
    /// <summary>
    /// The detail for an on-foot material by symbol or name, or null where the catalogue does not know it
    /// or it lives in another ledger. <paramref name="get"/> is how many the Commander wants from the
    /// Bartender.
    /// </summary>
    public static MicroResourceDetail? For(string symbol, CommanderGameState? state, int get = 1) =>
        For(symbol, state?.Suit ?? SuitInventory.Empty, get);

    /// <summary>The same, from the suit inventory alone.</summary>
    public static MicroResourceDetail? For(string symbol, SuitInventory suit, int get = 1)
    {
        if (MaterialCatalogue.Find(symbol) is not { Ledger: MaterialLedger.ShipLocker } entry)
        {
            return null;
        }

        var kind = KindOf(entry.Category);
        var perItem = kind == "Consumables";

        return new MicroResourceDetail(
            entry,
            kind,
            CountIn(suit.Backpack, entry.Symbol),
            CountIn(suit.ShipLocker, entry.Symbol),
            perItem ? OnFootRules.ConsumableCapacityPerItem : OnFootRules.LockerCapacityPerCategory,
            perItem,
            suit.ShipLockerTotal(kind),
            suit.IsKnown,
            entry.Settlements,
            entry.Buildings,
            entry.Containers,
            Exchanges(entry, suit, get));
    }

    /// <summary>Elite's four locker tabs, from the category the material table carries.</summary>
    public static string KindOf(string? category) => category switch
    {
        "Component" => "Components",
        "Consumable" => "Consumables",
        "Data" => "Data",
        _ => "Items",
    };

    private static int CountIn(IReadOnlyList<SuitItem> items, string symbol)
    {
        var wanted = JournalJson.Symbol(symbol);

        return wanted is null
            ? 0
            : items.Where(item => JournalJson.Symbol(item.Name) == wanted).Sum(item => item.Count);
    }

    private static IReadOnlyList<MicroResourceExchange> Exchanges(MaterialEntry entry, SuitInventory suit, int get)
    {
        if (entry.BarterCost is not { } cost)
        {
            return [];
        }

        return [.. MaterialCatalogue.All
            .Where(other => other.BarterValue is not null && other.Symbol != entry.Symbol)
            .Select(other => (other, Held: suit.CountOf(other.Symbol)))
            .Where(pair => pair.Held > 0)
            .Select(pair => (pair.other, pair.Held, Give: OnFootRules.BarterCostOf(get, pair.other.BarterValue, cost)))
            .Where(pair => pair.Give is { } give && give <= pair.Held)
            .OrderBy(pair => pair.Give)
            .Select(pair => new MicroResourceExchange(pair.other, pair.Give!.Value, get, pair.Held))];
    }
}
