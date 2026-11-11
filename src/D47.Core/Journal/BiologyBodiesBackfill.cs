using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Every Commander's biology bodies, recovered from every journal on disk.</summary>
public static class BiologyBodiesBackfill
{
    private static readonly JournalLineFilter Reads = JournalLineFilter.Events(
        "Commander", "LoadGame", "NewCommander", "FSDJump", "CarrierJump", "Location", "Scan", "FSSBodySignals",
        "SAASignalsFound");

    /// <summary>Each Commander's bodies, keyed by Frontier id, over journals oldest-first.</summary>
    public static IReadOnlyDictionary<string, BiologyBodies> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var records = new Dictionary<string, BiologyBodies>(StringComparer.Ordinal);
        var commander = string.Empty;

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();

            foreach (var line in JournalLines.Read(fileSystem, file, logger, "biology", warnIfMissing: true))
            {
                if (!Reads.Matches(line)
                    || !JournalEvent.TryParse(line, logger, out var parsed) || parsed is null)
                {
                    continue;
                }

                // Names the Commander it resets, which is not the one whose session came before it.
                if (parsed.Kind == "NewCommander")
                {
                    if (parsed.String("FID") is { Length: > 0 } created)
                    {
                        records.Remove(created);
                    }

                    continue;
                }

                if (parsed.Kind is "Commander" or "LoadGame" && parsed.String("FID") is { Length: > 0 } fid)
                {
                    commander = fid;
                }

                if (parsed.Kind != "Commander" && commander.Length > 0)
                {
                    records[commander] = (records.TryGetValue(commander, out var held) ? held : BiologyBodies.None).Apply(parsed);
                }
            }
        }

        return records;
    }
}
