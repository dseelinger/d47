using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Every Commander's credit transactions, recovered from every journal on disk.</summary>
public static class CreditLedgerBackfill
{
    /// <summary>Each Commander's ledger, keyed by Frontier id, over journals oldest-first.</summary>
    public static IReadOnlyDictionary<string, CreditLedger> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default) =>
        LedgerBackfill.FromHistory(
            fileSystem,
            files,
            logger,
            cancellation,
            "credit transactions",
            CreditLedger.None,
            (ledger, parsed) => ledger.Apply(parsed),
            (_, kind) => CreditLedger.Reads.GetAlternateLookup<ReadOnlySpan<char>>().Contains(kind),
            markOperations: false);
}
