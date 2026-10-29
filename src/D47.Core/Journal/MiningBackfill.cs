using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>The Commander's finished mining runs, recovered from journals d47 was not running for (#610).</summary>
public static class MiningBackfill
{
    private static readonly JournalLineFilter Reads = JournalLineFilter.Events(
        "LaunchDrone", "ProspectedAsteroid", "MiningRefined", "Docked", "Died", "LaunchSRV", "LaunchVessel",
        "DockSRV", "SRVDestroyed", "Embark", "Disembark", "Location");

    /// <summary>
    /// Every Commander's finished runs that refined at least one tonne, oldest first, keyed by Frontier id,
    /// over a list of journals oldest-first. A run still open at the last journal is not counted.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<MiningRun>> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var open = new Dictionary<string, MiningRuns>(StringComparer.Ordinal);
        var finished = new Dictionary<string, List<MiningRun>>(StringComparer.Ordinal);

        CommanderEventWalk.Each(
            fileSystem,
            files,
            logger,
            "mining events",
            Reads,
            cancellation,
            (parsed, commander) =>
            {
                if (parsed.Kind == "NewCommander")
                {
                    if (parsed.String("FID") is { Length: > 0 } created)
                    {
                        _ = open.Remove(created);
                        _ = finished.Remove(created);
                    }

                    return;
                }

                if (parsed.Kind is "Commander" or "LoadGame")
                {
                    if (parsed.Kind == "LoadGame"
                        && parsed.String("FID") is { Length: > 0 } fid
                        && open.TryGetValue(fid, out var resumed))
                    {
                        open[fid] = resumed.Apply(parsed);
                    }

                    return;
                }

                if (commander.Length == 0)
                {
                    return;
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
            });

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
}
