using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Every Commander's credit transactions, recovered from every journal on disk.</summary>
public static class CreditLedgerBackfill
{
    private const string EventKey = "\"event\":\"";

    /// <summary>Each Commander's ledger, keyed by Frontier id, over journals oldest-first.</summary>
    public static IReadOnlyDictionary<string, CreditLedger> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var ledgers = new Dictionary<string, CreditLedger>(StringComparer.Ordinal);
        var commander = string.Empty;

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();

            var legacy = false;

            foreach (var line in JournalLines.Read(fileSystem, file, logger, "credit transactions", warnIfMissing: true))
            {
                if (!Read(line) || !JournalEvent.TryParse(line, logger, out var parsed) || parsed is null)
                {
                    continue;
                }

                if (parsed.Kind == "Fileheader")
                {
                    legacy = CreditLedger.IsLegacy(parsed);
                    continue;
                }

                if (legacy)
                {
                    continue;
                }

                // Names the Commander it resets, which is not the one whose session came before it.
                if (parsed.Kind == "NewCommander")
                {
                    if (parsed.String("FID") is { Length: > 0 } created)
                    {
                        ledgers.Remove(created);
                    }

                    continue;
                }

                if (parsed.Kind is "Commander" or "LoadGame" && parsed.String("FID") is { Length: > 0 } fid)
                {
                    commander = fid;
                }

                if (parsed.Kind != "Commander" && commander.Length > 0)
                {
                    ledgers[commander] = (ledgers.TryGetValue(commander, out var held) ? held : CreditLedger.None).Apply(parsed);
                }
            }
        }

        return ledgers;
    }

    private static bool Read(string line)
    {
        var start = line.IndexOf(EventKey, StringComparison.Ordinal);

        if (start < 0)
        {
            return false;
        }

        start += EventKey.Length;
        var end = line.IndexOf('"', start);

        return end > start && CreditLedger.Reads.GetAlternateLookup<ReadOnlySpan<char>>().Contains(line.AsSpan(start, end - start));
    }
}
