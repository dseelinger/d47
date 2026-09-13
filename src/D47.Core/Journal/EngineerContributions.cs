using System.Collections.Immutable;
using System.Globalization;

namespace D47.Core.Journal;

/// <summary>Elite's running total of one thing contributed to one engineer, and when it was written.</summary>
public sealed record EngineerContributionReading(long TotalQuantity, DateTimeOffset SeenAt);

/// <summary>
/// What the Commander has contributed to each engineer, folded from <c>EngineerContribution</c>. The
/// total is Elite's <c>TotalQuantity</c> as written, not a sum of quantities.
/// </summary>
public sealed record EngineerContributions
{
    public static readonly EngineerContributions Empty = new();

    private ImmutableDictionary<string, EngineerContributionReading> Readings { get; init; } =
        ImmutableDictionary.Create<string, EngineerContributionReading>(StringComparer.OrdinalIgnoreCase);

    public bool IsKnown => !Readings.IsEmpty;

    /// <summary>
    /// The reading for one engineer, contribution <c>Type</c> and commodity or material symbol; the symbol
    /// is null for a type that names neither, such as <c>Bond</c>.
    /// </summary>
    public EngineerContributionReading? Reading(int engineerId, string type, string? symbol) =>
        Readings.TryGetValue(Key(engineerId, type, symbol), out var reading) ? reading : null;

    public long? Total(int engineerId, string type, string? symbol) =>
        Reading(engineerId, type, symbol)?.TotalQuantity;

    public EngineerContributions Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        if (journalEvent.Kind != "EngineerContribution" ||
            journalEvent.Int("EngineerID") is not { } engineerId ||
            journalEvent.String("Type") is not { } type ||
            journalEvent.Long("TotalQuantity") is not { } total)
        {
            return this;
        }

        var symbol = journalEvent.String("Commodity") ?? journalEvent.String("Material");

        return new EngineerContributions
        {
            Readings = Readings.SetItem(
                Key(engineerId, type, symbol),
                new EngineerContributionReading(total, journalEvent.Timestamp)),
        };
    }

    private static string Key(int engineerId, string type, string? symbol) =>
        string.Create(CultureInfo.InvariantCulture, $"{engineerId}|{type}|{symbol}");
}
