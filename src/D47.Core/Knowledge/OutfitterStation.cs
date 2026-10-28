namespace D47.Core.Knowledge;

/// <summary>One module a station's outfitting lists.</summary>
public sealed record StockedModule(string Name, int? Class, string? Rating);

/// <summary>What a station's outfitting and shipyard list, as the service reports it.</summary>
public sealed record StationStock(IReadOnlyList<StockedModule> Modules, IReadOnlyList<string> Ships)
{
    /// <summary>Whether the station lists the module, at the item's size and rating where it has them, or the ship.</summary>
    public bool Sells(OutfittingItem item) =>
        item.IsShip
            ? Ships.Any(ship => OutfittingCatalogue.ShipNames(item.Name).Contains(ship, StringComparer.OrdinalIgnoreCase))
            : Modules.Any(module =>
                OutfittingCatalogue.ModuleNames(item.Name).Contains(module.Name, StringComparer.OrdinalIgnoreCase)
                && (item.Class is null || module.Class == item.Class)
                && (item.Rating is null || string.Equals(module.Rating, item.Rating, StringComparison.OrdinalIgnoreCase)));
}

/// <summary>A station and the asked-for items it sells.</summary>
public sealed record OutfitterStation(StationSummary Station, IReadOnlyList<OutfittingItem> Sells)
{
    /// <summary>When the service last saw the station's outfitting or shipyard.</summary>
    public DateTimeOffset? ReportedAt => Station.StockLastSeen;
}
