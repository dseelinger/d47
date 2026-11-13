namespace D47.Core.Knowledge;

/// <summary>One module a station's outfitting lists; the mount is null where the service gives none.</summary>
public sealed record StockedModule(string Name, int? Class, string? Rating, string? Mount = null);

/// <summary>What a station's outfitting and shipyard list, as the service reports it.</summary>
public sealed record StationStock(IReadOnlyList<StockedModule> Modules, IReadOnlyList<string> Ships)
{
    /// <summary>Whether the station lists the module, at the item's size, rating and checked mount where it has them, or the ship.</summary>
    public bool Sells(OutfittingItem item) =>
        item.IsShip
            ? Ships.Any(ship => OutfittingCatalogue.ShipNames(item.Name).Contains(ship, StringComparer.OrdinalIgnoreCase))
            : Lists(item.Name, item.Class, item.Rating, item.MountChecked ? item.Mount : null);

    /// <summary>Whether the station lists the module, at its size and rating where given, and its mount where that is checked.</summary>
    public bool Lists(ModuleRequirement module) =>
        Lists(
            module.Name,
            int.TryParse(module.Class, System.Globalization.CultureInfo.InvariantCulture, out var size) ? size : null,
            module.Rating,
            module.MountChecked ? module.Mount : null);

    private bool Lists(string name, int? size, string? rating, string? mount) =>
        Modules.Any(module =>
            OutfittingCatalogue.ModuleNames(name).Contains(module.Name, StringComparer.OrdinalIgnoreCase)
            && (size is null || module.Class == size)
            && (rating is null || string.Equals(module.Rating, rating, StringComparison.OrdinalIgnoreCase))
            && (mount is null || string.Equals(module.Mount, mount, StringComparison.Ordinal)));
}

/// <summary>A station and the asked-for items it sells.</summary>
public sealed record OutfitterStation(StationSummary Station, IReadOnlyList<OutfittingItem> Sells)
{
    /// <summary>When the service last saw the station's outfitting or shipyard.</summary>
    public DateTimeOffset? ReportedAt => Station.StockLastSeen;
}
