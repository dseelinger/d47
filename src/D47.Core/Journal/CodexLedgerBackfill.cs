using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Every Commander's codex entries, recovered from every journal on disk.</summary>
public static class CodexLedgerBackfill
{
    private const string EventKey = "\"event\":\"";

    /// <summary>Each Commander's ledger, keyed by Frontier id, over journals oldest-first.</summary>
    public static IReadOnlyDictionary<string, CodexLedger> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var ledgers = new Dictionary<string, CodexLedger>(StringComparer.Ordinal);
        var commander = string.Empty;

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();

            var legacy = false;

            foreach (var parsed in OperationMarker.Mark(Parsed(fileSystem, file, logger)))
            {
                if (!CodexLedger.Reads.Contains(parsed.Kind))
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
                    ledgers[commander] = (ledgers.TryGetValue(commander, out var held) ? held : CodexLedger.None).Apply(parsed);
                }
            }
        }

        return ledgers;
    }

    private static IEnumerable<JournalEvent> Parsed(IFileSystem fileSystem, string file, ILogger logger)
    {
        foreach (var line in Lines(fileSystem, file, logger))
        {
            if (Read(line) && JournalEvent.TryParse(line, logger, out var parsed) && parsed is not null)
            {
                yield return parsed;
            }
        }
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

        var kind = line.AsSpan(start, end - start);

        return end > start
            && (kind.SequenceEqual("Scan")
                ? line.Contains("\"PlanetClass\"", StringComparison.Ordinal)
                : CodexLedger.Reads.GetAlternateLookup<ReadOnlySpan<char>>().Contains(kind)
                    || kind.SequenceEqual("GameModeChange") || kind.SequenceEqual("Shutdown"));
    }

    private static IEnumerable<string> Lines(IFileSystem fileSystem, string file, ILogger logger)
    {
        Stream stream;

        try
        {
            stream = fileSystem.OpenRead(file) ?? throw new FileNotFoundException("The journal file is missing.", file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read {File} for codex entries", file);
            yield break;
        }

        using (stream)
        {
            using var reader = new StreamReader(stream);

            while (reader.ReadLine() is { } line)
            {
                yield return line;
            }
        }
    }
}
