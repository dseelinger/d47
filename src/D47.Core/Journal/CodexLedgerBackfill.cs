using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Every Commander's codex entries, recovered from every journal on disk.</summary>
public static class CodexLedgerBackfill
{
    /// <summary>Each Commander's ledger, keyed by Frontier id, over journals oldest-first.</summary>
    public static IReadOnlyDictionary<string, CodexLedger> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default) =>
        LedgerBackfill.FromHistory(
            fileSystem,
            files,
            logger,
            cancellation,
            "codex entries",
            CodexLedger.None,
            (ledger, parsed) => ledger.Apply(parsed),
            Reads,
            markOperations: true);

    private static bool Reads(string line, ReadOnlySpan<char> kind) =>
        kind.SequenceEqual("Scan")
            ? line.Contains("\"PlanetClass\"", StringComparison.Ordinal)
            : CodexLedger.Reads.GetAlternateLookup<ReadOnlySpan<char>>().Contains(kind)
                || kind.SequenceEqual("GameModeChange") || kind.SequenceEqual("Shutdown");
}
