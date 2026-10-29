using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Every Commander's tech broker unlocks, recovered from every journal on disk.</summary>
public static class TechBrokerBackfill
{
    private static readonly JournalLineFilter Reads = JournalLineFilter.Events("TechnologyBroker", "Loadout", "StoredModules");

    /// <summary>Each Commander's unlocks, keyed by Frontier id, over journals oldest-first.</summary>
    public static IReadOnlyDictionary<string, TechBrokerUnlocks> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var unlocks = new Dictionary<string, TechBrokerUnlocks>(StringComparer.Ordinal);

        CommanderEventWalk.Each(
            fileSystem,
            files,
            logger,
            "tech broker unlocks",
            Reads,
            cancellation,
            (parsed, commander) =>
            {
                if (parsed.Kind == "NewCommander")
                {
                    if (parsed.String("FID") is { Length: > 0 } created)
                    {
                        _ = unlocks.Remove(created);
                    }

                    return;
                }

                if (parsed.Kind is "Commander" or "LoadGame" || commander.Length == 0)
                {
                    return;
                }

                var now = unlocks.GetValueOrDefault(commander, TechBrokerUnlocks.Empty).Apply(parsed);

                if (now.IsKnown)
                {
                    unlocks[commander] = now;
                }
            });

        logger.LogInformation("Tech broker unlocks recovered from history for {Count} Commanders", unlocks.Count);

        return unlocks;
    }
}
