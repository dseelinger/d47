using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Every Commander's surface mining sites, recovered from every journal on disk.</summary>
public static class SurfaceMiningBackfill
{
    private static readonly JournalLineFilter Reads = JournalLineFilter.Events(
        "Commander", "LoadGame", "NewCommander", "Shutdown", "Location", "ApproachBody", "Touchdown", "LeaveBody",
        "FSDJump", "CarrierJump", "LaunchSRV", "LaunchVessel", "DockSRV", "SRVDestroyed", "Embark", "Disembark",
        "Died", "MiningRefined");

    /// <summary>Each Commander's sites, keyed by Frontier id, over journals oldest-first.</summary>
    public static IReadOnlyDictionary<string, SurfaceMiningSites> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var records = new Dictionary<string, SurfaceMiningSites>(StringComparer.Ordinal);
        var commander = string.Empty;

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();

            foreach (var line in JournalLines.Read(fileSystem, file, logger, "surface mining", warnIfMissing: true))
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
                    records[commander] = (records.TryGetValue(commander, out var held) ? held : SurfaceMiningSites.None).Apply(parsed);
                }
            }
        }

        return records;
    }
}
