using System.Globalization;
using D47.Core.Knowledge;

namespace D47.Core.Watches;

/// <summary>The form field a refused save is written on.</summary>
public enum WatchField
{
    None,
    System,
    Station,
    Commodity,
    Units,
}

/// <summary>The outcome of a save: <see cref="Message"/> is null when the watch was written.</summary>
public sealed record WatchSaveResult(WatchField Field, string? Message)
{
    public bool Saved => Message is null;

    public static WatchSaveResult Done { get; } = new(WatchField.None, null);

    public static WatchSaveResult Refused(WatchField field, string reason) => new(field, "Not saved: " + reason);
}

/// <summary>A stock watch as the form holds it.</summary>
/// <param name="Rare">The rare picked, which fills the station and commodity; null for any station.</param>
public sealed record StockDraft(
    string Name,
    bool Enabled,
    RareEntry? Rare,
    string System,
    string Station,
    string Commodity,
    StockComparison Comparison,
    string Units);

/// <summary>Resolves a stock watch's station and commodity once, on save, and writes it to the store.</summary>
public sealed class WatchSetup(WatchStore store, IGalaxyService galaxy, ITradePlanService trade, Func<bool> galaxySearch)
{
    public const string DefaultCommunityGoalName = "New Community Goals";

    /// <summary>The name a stock watch takes until the Commander types one.</summary>
    public static string DefaultName(string commodity, string station) => $"{commodity} at {station}";

    /// <summary>A whole number of 0 or more, or null.</summary>
    public static int? Units(string? text) =>
        int.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var units) ? units : null;

    /// <summary>Saves a New Community Goal watch; <paramref name="existing"/> is the watch being edited.</summary>
    public WatchSaveResult SaveCommunityGoal(Watch? existing, string name, bool enabled)
    {
        var typed = name.Trim();
        var watch = new Watch(
            existing?.Id ?? Guid.NewGuid(),
            typed.Length > 0 ? typed : DefaultCommunityGoalName,
            WatchKind.NewCommunityGoal,
            enabled,
            FiredForTick: existing?.FiredForTick);

        return Write(existing, watch);
    }

    /// <summary>
    /// Saves a stock watch. A rare sends nothing; any other station is looked up through Spansh and its commodity
    /// checked with Ardent, both only with Galaxy search on.
    /// </summary>
    public async Task<WatchSaveResult> SaveStockAsync(Watch? existing, StockDraft draft, CancellationToken cancellationToken)
    {
        if (Units(draft.Units) is not { } units)
        {
            return WatchSaveResult.Refused(WatchField.Units, "type a whole number of 0 or more.");
        }

        long marketId;
        string system;
        string station;
        string commodity;

        if (draft.Rare is { } rare)
        {
            (marketId, system, station, commodity) = (rare.MarketId, rare.System, rare.Station, rare.Name);
        }
        else
        {
            (system, station, commodity) = (draft.System.Trim(), draft.Station.Trim(), draft.Commodity.Trim());

            if (system.Length == 0)
            {
                return WatchSaveResult.Refused(WatchField.System, "type a system.");
            }

            if (station.Length == 0)
            {
                return WatchSaveResult.Refused(WatchField.Station, "pick a station.");
            }

            if (commodity.Length == 0)
            {
                return WatchSaveResult.Refused(WatchField.Commodity, "type a commodity.");
            }

            if (!galaxySearch())
            {
                return WatchSaveResult.Refused(WatchField.System, "Galaxy search is off, and an any-station watch needs it.");
            }

            var found = await ResolveAsync(system, station, cancellationToken).ConfigureAwait(false);

            if (found.Refusal is { } refusal)
            {
                return refusal;
            }

            (marketId, system, station) = (found.MarketId, found.System, found.Station);

            try
            {
                await trade.QuoteAsync(marketId, commodity, cancellationToken).ConfigureAwait(false);
            }
            catch (UnknownCommodityException)
            {
                return WatchSaveResult.Refused(
                    WatchField.Commodity,
                    $"Ardent lists no commodity called {commodity}. Type it as the market spells it.");
            }
            catch (GalaxyUnavailableException)
            {
                return WatchSaveResult.Refused(WatchField.Commodity, "Ardent could not be reached to check the commodity.");
            }
        }

        var typed = draft.Name.Trim();

        var watch = new Watch(
            existing?.Id ?? Guid.NewGuid(),
            typed.Length > 0 ? typed : DefaultName(commodity, station),
            WatchKind.Stock,
            draft.Enabled,
            marketId,
            station,
            system,
            commodity,
            draft.Comparison,
            units,
            existing?.FiredForTick);

        return Write(existing, watch);
    }

    /// <summary>The names of the fixed stations in <paramref name="system"/>, or none where Galaxy search is off or the lookup fails.</summary>
    public async Task<IReadOnlyList<string>> StationsInAsync(string system, CancellationToken cancellationToken)
    {
        if (!galaxySearch() || string.IsNullOrWhiteSpace(system))
        {
            return [];
        }

        try
        {
            var result = await galaxy.FindStationsAsync(StationQuery.Near(system, 1, 20), cancellationToken).ConfigureAwait(false);

            return [.. result.Stations
                .Where(found => string.Equals(found.SystemName, system.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(found => found.Name)
                .Order(StringComparer.OrdinalIgnoreCase)];
        }
        catch (GalaxyUnavailableException)
        {
            return [];
        }
    }

    private WatchSaveResult Write(Watch? existing, Watch watch)
    {
        var reason = existing is null ? store.Add(watch) : store.Update(watch);

        return reason is null ? WatchSaveResult.Done : WatchSaveResult.Refused(WatchField.None, reason);
    }

    private async Task<(long MarketId, string System, string Station, WatchSaveResult? Refusal)> ResolveAsync(
        string system,
        string station,
        CancellationToken cancellationToken)
    {
        StationSearchResult result;

        try
        {
            result = await galaxy.FindStationsAsync(StationQuery.Within(system, station), cancellationToken).ConfigureAwait(false);
        }
        catch (GalaxyUnavailableException)
        {
            return (0, system, station, WatchSaveResult.Refused(WatchField.System, "Spansh could not be reached to find the station."));
        }

        var match = result.Stations.FirstOrDefault(found =>
            found.MarketId is > 0 && string.Equals(found.Name, station, StringComparison.OrdinalIgnoreCase));

        return match?.MarketId is { } marketId
            ? (marketId, match.SystemName, match.Name, null)
            : (0, system, station, WatchSaveResult.Refused(WatchField.System, $"Spansh finds no stations in {system}."));
    }
}
