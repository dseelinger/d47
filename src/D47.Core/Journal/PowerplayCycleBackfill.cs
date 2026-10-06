using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>The merits earned for the pledged Power over the last week, recovered from earlier journals.</summary>
public static class PowerplayCycleBackfill
{
    /// <summary>Each Commander's merits for their Power, keyed by Frontier id, over a list of journals oldest-first.</summary>
    public static IReadOnlyDictionary<string, PowerplayCycleMerits> FromHistory(
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

            foreach (var line in Lines(file, logger))
            {
                // Powerplay, PowerplayJoin, PowerplayDefect, PowerplayLeave, PowerplayMerits and the rest.
                if (!line.Contains("\"event\":\"Powerplay", StringComparison.Ordinal)
                    && !line.Contains("\"event\":\"Commander\"", StringComparison.Ordinal)
                    && !line.Contains("\"event\":\"LoadGame\"", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!JournalEvent.TryParse(line, logger, out var parsed) || parsed is null)
                {
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

    private static IEnumerable<string> Lines(string file, ILogger logger)
    {
        FileStream stream;

        try
        {
            stream = new FileStream(
                file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read {File} for Powerplay events", file);
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
