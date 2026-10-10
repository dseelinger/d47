using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>The construction sites the Commander has docked at, recovered from journals d47 was not running for (#798).</summary>
public static class ColonisationBackfill
{
    /// <summary>Every Commander's sites as their journals last reported them, keyed by Frontier id. Sites only, never contributions.</summary>
    public static IReadOnlyDictionary<string, ColonisationSites> FromHistory(
        IFileSystem fileSystem,
        string directory,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (fileSystem.FolderWritten(directory) is null)
        {
            logger.LogWarning("No journal folder at {Directory}", directory);
            return new Dictionary<string, ColonisationSites>(StringComparer.Ordinal);
        }

        return FromHistory(
            fileSystem,
            [.. fileSystem.Enumerate(directory, JournalFolder.FilePattern)
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)],
            logger,
            cancellation);
    }

    /// <summary>The same, over an explicit list oldest-first.</summary>
    public static IReadOnlyDictionary<string, ColonisationSites> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var sites = new Dictionary<string, ColonisationSites>(StringComparer.Ordinal);
        var locations = new Dictionary<string, JournalLocation>(StringComparer.Ordinal);
        var commander = string.Empty;

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();

            foreach (var line in Lines(fileSystem, file, logger))
            {
                // ColonisationContribution is left out: a running sum folded here and again by the live reader
                // would count the current journal twice.
                if (!line.Contains("\"event\":\"ColonisationConstructionDepot\"", StringComparison.Ordinal)
                    && !line.Contains("\"event\":\"ColonisationSystemClaim\"", StringComparison.Ordinal)
                    && !line.Contains("\"event\":\"ColonisationBeaconDeployed\"", StringComparison.Ordinal)
                    && !line.Contains("\"event\":\"Commander\"", StringComparison.Ordinal)
                    && !line.Contains("\"event\":\"LoadGame\"", StringComparison.Ordinal)
                    && !line.Contains("\"event\":\"Location\"", StringComparison.Ordinal)
                    && !line.Contains("\"event\":\"FSDJump\"", StringComparison.Ordinal)
                    && !line.Contains("\"event\":\"CarrierJump\"", StringComparison.Ordinal)
                    && !line.Contains("\"event\":\"Docked\"", StringComparison.Ordinal)
                    && !line.Contains("\"event\":\"Undocked\"", StringComparison.Ordinal))
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

                var location = (locations.GetValueOrDefault(commander) ?? JournalLocation.Unknown).Apply(parsed);
                locations[commander] = location;

                var held = sites.GetValueOrDefault(commander) ?? ColonisationSites.Empty;
                sites[commander] = held.Apply(parsed, location.StarSystem, location.StationName);
            }
        }

        var found = new Dictionary<string, ColonisationSites>(StringComparer.Ordinal);

        foreach (var (fid, held) in sites)
        {
            if (!held.IsKnown)
            {
                continue;
            }

            found[fid] = held;

            logger.LogInformation(
                "Construction sites recovered from history for {Commander}: {Count}",
                fid,
                held.All.Count);
        }

        return found;
    }

    /// <summary>One journal's lines.</summary>
    private static IEnumerable<string> Lines(IFileSystem fileSystem, string file, ILogger logger)
    {
        Stream stream;

        try
        {
            stream = fileSystem.OpenRead(file) ?? throw new FileNotFoundException("The journal file is missing.", file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read {File} for construction sites", file);
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
