using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Each Commander's last known location, recovered from journals d47 was not running for.</summary>
public static class LocationBackfill
{
    /// <summary>The location each Commander's newest journals left them in, over the last <see cref="FleetBackfill.MaxLookback"/> files.</summary>
    public static IReadOnlyDictionary<string, JournalLocation> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var locations = new Dictionary<string, JournalLocation>(StringComparer.Ordinal);
        var current = string.Empty;

        for (var i = Math.Max(0, files.Count - FleetBackfill.MaxLookback); i < files.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();

            CommanderEventWalk.Poll(
                new JournalReader(files[i], fileSystem, logger),
                ref current,
                (journalEvent, commander) =>
                {
                    if (journalEvent.Kind == "NewCommander")
                    {
                        if (journalEvent.String("FID") is { Length: > 0 } created)
                        {
                            _ = locations.Remove(created);
                        }

                        return;
                    }

                    if (commander.Length == 0)
                    {
                        return;
                    }

                    var next = (locations.TryGetValue(commander, out var held) ? held : JournalLocation.Unknown).Apply(journalEvent);

                    if (next.StarSystem is not null)
                    {
                        locations[commander] = next;
                    }
                });
        }

        return locations;
    }
}
