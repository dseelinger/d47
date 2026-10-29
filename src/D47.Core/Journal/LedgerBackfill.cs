using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Whether a journal line is one a ledger reads, given the event name at the start of the line.</summary>
internal delegate bool LedgerLine(string line, ReadOnlySpan<char> kind);

internal static class LedgerBackfill
{
    private const string EventKey = "\"event\":\"";

    /// <summary>Each Commander's ledger, keyed by Frontier id, over journals oldest-first. Legacy files are skipped.</summary>
    public static IReadOnlyDictionary<string, TLedger> FromHistory<TLedger>(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation,
        string purpose,
        TLedger none,
        Func<TLedger, JournalEvent, TLedger> apply,
        LedgerLine reads,
        bool markOperations)
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
            var events = Parsed(fileSystem, file, logger, purpose, reads);

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
        IFileSystem fileSystem, string file, ILogger logger, string purpose, LedgerLine reads)
    {
        foreach (var line in JournalLines.Read(fileSystem, file, logger, purpose, warnIfMissing: true))
        {
            var start = line.IndexOf(EventKey, StringComparison.Ordinal);

            if (start < 0)
            {
                continue;
            }

            start += EventKey.Length;
            var end = line.IndexOf('"', start);

            if (end > start
                && reads(line, line.AsSpan(start, end - start))
                && JournalEvent.TryParse(line, logger, out var parsed) && parsed is not null)
            {
                yield return parsed;
            }
        }
    }
}
