using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// Where the Commander's fleet carrier is, recovered from journals d47 was not running for (#406).
/// </summary>
public static class CarrierBackfill
{
    /// <summary>Every Commander's own carrier as their journals last reported it, keyed by Frontier id.</summary>
    public static IReadOnlyDictionary<string, CarrierState> FromHistory(string directory, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!Directory.Exists(directory))
        {
            logger.LogWarning("No journal folder at {Directory}", directory);
            return new Dictionary<string, CarrierState>(StringComparer.Ordinal);
        }

        return FromHistory(
            [.. Directory.EnumerateFiles(directory, JournalFolder.FilePattern)
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)],
            logger);
    }

    /// <summary>The same, over an explicit list oldest-first.</summary>
    public static IReadOnlyDictionary<string, CarrierState> FromHistory(
        IReadOnlyList<string> files,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var folded = new Dictionary<string, CarrierState>(StringComparer.Ordinal);
        var commander = string.Empty;

        foreach (var file in files)
        {
            foreach (var line in Lines(file, logger))
            {
                // The text test, before any JSON is touched: three event names out of the hundreds a journal
                // holds, so the cost is a read of the folder rather than a replay of it.
                if (!line.Contains("\"event\":\"Carrier", StringComparison.Ordinal)
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

                var held = folded.TryGetValue(commander, out var existing) ? existing : CarrierState.None;

                folded[commander] = held.Apply(parsed);
            }
        }

        var carriers = new Dictionary<string, CarrierState>(StringComparer.Ordinal);

        foreach (var (fid, carrier) in folded)
        {
            // Nothing worth restoring is nothing filed: filing a state that learned neither an identity nor a
            // location would hand the store an answer that is not one.
            if (!carrier.IsKnown)
            {
                continue;
            }

            carriers[fid] = new CarrierState
            {
                CallSign = carrier.CallSign,
                Name = carrier.Name,
                CarrierId = carrier.CarrierId,
                StarSystem = carrier.StarSystem,
                SeenAt = carrier.SeenAt,
            };

            logger.LogInformation(
                "Carrier recovered from history for {Commander}: {CallSign} at {System}, last reported {SeenAt:u}",
                fid,
                carrier.CallSign ?? "callsign unknown",
                carrier.StarSystem ?? "somewhere unrecorded",
                carrier.SeenAt);
        }

        return carriers;
    }

    /// <summary>One journal's lines.</summary>
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
            // A journal that cannot be read is one file's worth of history missing, not a reason to start
            // with no carrier at all.
            logger.LogWarning(ex, "Could not read {File} for carrier events", file);
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
