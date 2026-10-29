using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

internal static class LedgerBackfill
{
    /// <summary>Each Commander's ledger, keyed by Frontier id, over journals oldest-first. Legacy files are skipped.</summary>
    public static IReadOnlyDictionary<string, TLedger> FromHistory<TLedger>(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation,
        string purpose,
        TLedger none,
        Func<TLedger, JournalEvent, TLedger> apply,
        JournalLineFilter reads,
        bool markOperations,
        Func<string, bool>? alsoRequires = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var ledgers = new Dictionary<string, TLedger>(StringComparer.Ordinal);
        var commander = string.Empty;

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();

            var legacy = false;
            var events = Parsed(fileSystem, file, logger, purpose, reads, alsoRequires);

            foreach (var parsed in markOperations ? OperationMarker.Mark(events) : events)
            {
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
                    ledgers[commander] = apply(ledgers.TryGetValue(commander, out var held) ? held : none, parsed);
                }
            }
        }

        return ledgers;
    }

    private static IEnumerable<JournalEvent> Parsed(
        IFileSystem fileSystem,
        string file,
        ILogger logger,
        string purpose,
        JournalLineFilter reads,
        Func<string, bool>? alsoRequires)
    {
        foreach (var line in JournalLines.Read(fileSystem, file, logger, purpose, warnIfMissing: true))
        {
            if (reads.Matches(line)
                && (alsoRequires is null || alsoRequires(line))
                && JournalEvent.TryParse(line, logger, out var parsed) && parsed is not null)
            {
                yield return parsed;
            }
        }
    }
}
