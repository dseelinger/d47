using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// The kind of material trader each station has, as written by the Commander's own <c>MaterialTrade</c>
/// events, for the machine rather than per Commander. Rebuilt from the journals on every start and extended
/// by live events; holds no file.
/// </summary>
public sealed class TraderSightings(IFileSystem fileSystem, ILogger logger)
{
    private readonly Lock _gate = new();

    private readonly Dictionary<long, string> _types = [];

    private bool _legacy;

    /// <summary><c>Raw</c>, <c>Manufactured</c> or <c>Encoded</c> as <see cref="StationQuery.TraderTypes"/> spells them, or null when no trade there is recorded.</summary>
    public string? TypeOf(long marketId)
    {
        lock (_gate)
        {
            return _types.GetValueOrDefault(marketId);
        }
    }

    /// <summary>Folds live events; a synchronous append with no I/O.</summary>
    public void Apply(IReadOnlyList<JournalEvent> events)
    {
        JournalFold.Apply(_gate, events, Fold);
    }

    /// <summary>Folds journal files oldest first, on the calling thread.</summary>
    public void FoldHistory(IReadOnlyList<string> files, CancellationToken cancellation = default)
    {
        JournalFold.Files(fileSystem, logger, files, "trader types", Fold, cancellation);
    }

    /// <summary>Folds journal lines in order.</summary>
    public void Fold(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        Apply(JournalFold.Parse(lines, Reads.Matches, logger));
    }

    private static readonly JournalLineFilter Reads = JournalLineFilter.Events("MaterialTrade", "Fileheader");

    private void Fold(JournalEvent journalEvent)
    {
        switch (journalEvent.Kind)
        {
            case "Fileheader":
                _legacy = CreditLedger.IsLegacy(journalEvent);
                break;

            case "MaterialTrade" when !_legacy
                && journalEvent.Long("MarketID") is { } marketId
                && journalEvent.String("TraderType") is { } written
                && StationQuery.TraderTypes.FirstOrDefault(
                    type => string.Equals(type, written, StringComparison.OrdinalIgnoreCase)) is { } spelled:
                _types[marketId] = spelled;
                break;
        }
    }
}
