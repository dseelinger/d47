using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>The construction sites the Commander has docked at, recovered from journals d47 was not running for (#798).</summary>
public static class ColonisationBackfill
{
    private static readonly JournalLineFilter Reads = JournalLineFilter.Events(
        "ColonisationConstructionDepot", "ColonisationSystemClaim", "ColonisationBeaconDeployed",
        "Location", "FSDJump", "CarrierJump", "Docked", "Undocked");

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

        // ColonisationContribution is left out: a running sum folded here and again by the live reader
        // would count the current journal twice.
        CommanderEventWalk.Each(
            fileSystem,
            files,
            logger,
            "construction sites",
            Reads,
            cancellation,
            (parsed, commander) =>
            {
                if (parsed.Kind == "NewCommander")
                {
                    if (parsed.String("FID") is { Length: > 0 } created)
                    {
                        _ = sites.Remove(created);
                        _ = locations.Remove(created);
                    }

                    return;
                }

                if (parsed.Kind is "Commander" or "LoadGame" || commander.Length == 0)
                {
                    return;
                }

                var location = (locations.GetValueOrDefault(commander) ?? JournalLocation.Unknown).Apply(parsed);
                locations[commander] = location;

                var held = sites.GetValueOrDefault(commander) ?? ColonisationSites.Empty;
                sites[commander] = held.Apply(parsed, location.StarSystem, location.StationName);
            });

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
}
