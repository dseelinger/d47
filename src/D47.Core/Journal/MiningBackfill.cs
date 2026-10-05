using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>The Commander's finished mining runs, recovered from journals d47 was not running for (#610).</summary>
public static class MiningBackfill
{
    /// <summary>
    /// Every Commander's finished runs that refined at least one tonne, oldest first, keyed by Frontier id,
    /// over a list of journals oldest-first. A run still open at the last journal is not counted.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<MiningRun>> FromHistory(
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var open = new Dictionary<string, MiningRuns>(StringComparer.Ordinal);
        var finished = new Dictionary<string, List<MiningRun>>(StringComparer.Ordinal);
        var commander = string.Empty;

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();

            foreach (var line in Lines(file, logger))
            {
                if (!line.Contains("\"event\":\"LaunchDrone\"", StringComparison.Ordinal)
                    && !line.Contains("\"event\":\"ProspectedAsteroid\"", StringComparison.Ordinal)
                    && !line.Contains("\"event\":\"MiningRefined\"", StringComparison.Ordinal)
                    && !line.Contains("\"event\":\"Docked\"", StringComparison.Ordinal)
                    && !line.Contains("\"event\":\"Died\"", StringComparison.Ordinal)
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

                var held = open.TryGetValue(commander, out var existing) ? existing : MiningRuns.None;
                var next = held.Apply(parsed);

                if (held.Open is not null && next.Open is null && next.Last is { TonnesRefined: > 0 } closed)
                {
                    if (!finished.TryGetValue(commander, out var runs))
                    {
                        finished[commander] = runs = [];
                    }

                    runs.Add(closed);
                }

                open[commander] = next;
            }
        }

        foreach (var (fid, runs) in finished)
        {
            logger.LogInformation(
                "Mining runs recovered from history for {Commander}: {Runs} runs, {Tonnes} t refined",
                fid,
                runs.Count,
                runs.Sum(run => run.TonnesRefined));
        }

        return finished.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<MiningRun>)pair.Value,
            StringComparer.Ordinal);
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
            logger.LogWarning(ex, "Could not read {File} for mining events", file);
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
