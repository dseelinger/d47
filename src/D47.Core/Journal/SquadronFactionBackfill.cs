using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Every Commander's squadron faction sightings, read from every journal on disk.</summary>
public static class SquadronFactionBackfill
{
    private static readonly JournalLineFilter Reads = JournalLineFilter.Events(
        "SquadronStartup", "JoinedSquadron", "SquadronCreated", "LeftSquadron", "KickedFromSquadron", "DisbandedSquadron",
        "Location", "FSDJump", "CarrierJump");

    /// <summary>Each Commander's sightings, keyed by Frontier id, over journals oldest-first.</summary>
    public static IReadOnlyDictionary<string, SquadronFactionSightings> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var sightings = new Dictionary<string, SquadronFactionSightings>(StringComparer.Ordinal);
        var squadrons = new Dictionary<string, SquadronState>(StringComparer.Ordinal);

        CommanderEventWalk.Each(
            fileSystem,
            files,
            logger,
            "squadron factions",
            Reads,
            cancellation,
            (parsed, commander) =>
            {
                if (parsed.Kind == "NewCommander")
                {
                    if (parsed.String("FID") is { Length: > 0 } created)
                    {
                        _ = sightings.Remove(created);
                        _ = squadrons.Remove(created);
                    }

                    return;
                }

                if (parsed.Kind == "Commander" || commander.Length == 0)
                {
                    return;
                }

                var squadron = squadrons.GetValueOrDefault(commander, SquadronState.None).Apply(parsed);
                squadrons[commander] = squadron;

                var now = sightings.GetValueOrDefault(commander, SquadronFactionSightings.Empty).Apply(parsed, squadron);

                if (now.IsKnown)
                {
                    sightings[commander] = now;
                }
            });

        logger.LogInformation("Squadron factions recovered from history for {Count} Commanders", sightings.Count);

        return sightings;
    }
}
