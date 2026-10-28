using D47.Core.Knowledge;

namespace D47.Core.Ships;

/// <summary>One thing a ship plan still needs bought.</summary>
/// <param name="Item">The form <c>search_outfitting</c> parses.</param>
/// <param name="Symbol">The journal symbol where the plan has one: the exact module, or the hull.</param>
/// <param name="Slot">The slot that plans it; null for the hull.</param>
public sealed record ShipPurchase(OutfittingItem Item, string? Symbol, string? Slot);

/// <summary>What a ship plan still needs bought, and whether module storage was checked to say so.</summary>
/// <param name="StorageChecked">False until a <c>StoredModules</c> snapshot has been seen.</param>
public sealed record ShipPurchases(IReadOnlyList<ShipPurchase> Items, bool StorageChecked);
