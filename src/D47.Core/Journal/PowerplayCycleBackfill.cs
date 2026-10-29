using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>The merits earned for the pledged Power over the last week, recovered from earlier journals.</summary>
public static class PowerplayCycleBackfill
{
    /// <summary>Each Commander's merits for their Power, keyed by Frontier id, over a list of journals oldest-first.</summary>
    public static IReadOnlyDictionary<string, PowerplayCycleMerits> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var pledges = new Dictionary<string, PowerplayPledge>(StringComparer.Ordinal);
        var merits = new Dictionary<string, PowerplayCycleMerits>(StringComparer.Ordinal);
        var commander = string.Empty;

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();

            foreach (var line in JournalLines.Read(fileSystem, file, logger, "Powerplay events", warnIfMissing: true))
            {
                // Powerplay, PowerplayJoin, PowerplayDefect, PowerplayLeave, PowerplayMerits and the rest.
                if (!line.Contains("\"event\":\"Powerplay", StringComparison.Ordinal)
                    && !line.Contains("\"event\":\"NewCommander\"", StringComparison.Ordinal)
                    && !line.Contains("\"event\":\"Commander\"", StringComparison.Ordinal)
                    && !line.Contains("\"event\":\"LoadGame\"", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!JournalEvent.TryParse(line, logger, out var parsed) || parsed is null)
                {
                    continue;
                }

                if (parsed.Kind == "NewCommander")
                {
                    if (parsed.String("FID") is { Length: > 0 } created)
                    {
                        _ = pledges.Remove(created);
                        _ = merits.Remove(created);
                    }

                    continue;
                }

                if (parsed.Kind is "Commander" or "LoadGame")
                {
                    if (parsed.String("FID") is { Length: > 0 } fid)
                    {
                        commander = fid;
                    }

                    continue;
                }

                if (commander.Length == 0)
                {
                    continue;
                }

                var pledge = (pledges.TryGetValue(commander, out var held) ? held : PowerplayPledge.None).Apply(parsed);
                pledges[commander] = pledge;

                merits[commander] = (merits.TryGetValue(commander, out var sum) ? sum : PowerplayCycleMerits.None)
                    .Apply(parsed, pledge);
            }
        }

        return merits
            .Where(pair => pair.Value.IsKnown)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }
}
