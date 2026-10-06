using System.Collections.Immutable;

namespace D47.Core.Journal;

/// <summary>An open trade order on the carrier's market, from <c>CarrierTradeOrder</c>.</summary>
public enum CarrierOrder
{
    None,

    /// <summary>Other Commanders can sell into the hold, so the counted figure may be low.</summary>
    Purchase,

    /// <summary>Other Commanders can buy out of the hold, so the counted figure may be high.</summary>
    Sale,
}

/// <summary>
/// The carrier's hold per commodity, counted from the Commander's own transfers, buys and sells, and
/// checked against <c>SpaceUsage.Cargo</c> at each <c>CarrierStats</c> (#799).
/// </summary>
public sealed record CarrierHold
{
    public static readonly CarrierHold Empty = new();

    /// <summary>Tonnes per folded commodity symbol.</summary>
    public ImmutableDictionary<string, int> Tonnes { get; init; } = ImmutableDictionary.Create<string, int>(StringComparer.Ordinal);

    /// <summary>The open trade order per folded commodity symbol.</summary>
    public ImmutableDictionary<string, CarrierOrder> Orders { get; init; } =
        ImmutableDictionary.Create<string, CarrierOrder>(StringComparer.Ordinal);

    /// <summary>
    /// Whether the count matched <c>SpaceUsage.Cargo</c> at the last check, or null before the first one.
    /// A count held at zero is false until the next check.
    /// </summary>
    public bool? Reconciled { get; init; }

    /// <summary>When <see cref="Reconciled"/> was last set by a <c>CarrierStats</c>.</summary>
    public DateTimeOffset? CheckedAt { get; init; }

    /// <summary>The <c>CarrierStats</c> reporting an empty hold that the count starts from, or null.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    public int Total => Tonnes.Values.Sum();

    /// <summary>Tonnes of a commodity, or null for one no movement has counted.</summary>
    public int? Holding(string symbol) =>
        Fold(symbol) is { } key && Tonnes.TryGetValue(key, out var tonnes) ? tonnes : null;

    public CarrierOrder Order(string symbol) =>
        Fold(symbol) is { } key && Orders.TryGetValue(key, out var order) ? order : CarrierOrder.None;

    public bool OrderOpen(string symbol) => Order(symbol) is not CarrierOrder.None;

    /// <summary>Whether the figure for a commodity might be wrong.</summary>
    public bool Uncertain(string symbol) => OrderOpen(symbol) || Reconciled == false;

    /// <summary>
    /// The count moved by <paramref name="delta"/> tonnes of a commodity, held at zero and marked
    /// unreconciled rather than going negative.
    /// </summary>
    public CarrierHold Moved(string? symbol, int delta)
    {
        if (Fold(symbol) is not { } key || delta == 0)
        {
            return this;
        }

        var next = Tonnes.GetValueOrDefault(key) + delta;

        return next < 0
            ? this with { Tonnes = Tonnes.SetItem(key, 0), Reconciled = false }
            : this with { Tonnes = Tonnes.SetItem(key, next) };
    }

    /// <summary>The count checked against the hold's total; an empty hold starts the count again.</summary>
    public CarrierHold Checked(int cargo, DateTimeOffset at) => cargo == 0
        ? Empty with { Orders = Orders, Reconciled = true, CheckedAt = at, StartedAt = at }
        : this with { Reconciled = Total == cargo, CheckedAt = at };

    /// <summary>
    /// An order opened on a commodity, or cancelled. A cancelled order may have been partly filled, so
    /// the count is unreconciled until the next check.
    /// </summary>
    public CarrierHold Ordered(string? symbol, CarrierOrder order)
    {
        if (Fold(symbol) is not { } key)
        {
            return this;
        }

        return order is CarrierOrder.None
            ? this with { Orders = Orders.Remove(key), Reconciled = false }
            : this with { Orders = Orders.SetItem(key, order) };
    }

    /// <summary>
    /// The count to keep when the walk over every journal is adopted beside the live reader's. The walk
    /// read the current journal too, so its count already holds what the reader counted; the reader's
    /// is kept only when it starts from an empty hold at least as recent as the walk's, which makes it
    /// complete on its own.
    /// </summary>
    public static CarrierHold Adopted(CarrierHold restored, CarrierHold live)
    {
        ArgumentNullException.ThrowIfNull(restored);
        ArgumentNullException.ThrowIfNull(live);

        return live.StartedAt is { } fresh && (restored.StartedAt is not { } walked || fresh >= walked)
            ? live
            : restored;
    }

    private static string? Fold(string? symbol) => JournalJson.Symbol(symbol);
}
