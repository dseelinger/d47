using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Every Commander's tech broker unlocks, recovered from every journal on disk.</summary>
public static class TechBrokerBackfill
{
    private static readonly string[] Kinds = ["Commander", "LoadGame", "NewCommander", "TechnologyBroker", "Loadout", "StoredModules"];

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
        var commander = string.Empty;

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();

            foreach (var line in JournalLines.Read(fileSystem, file, logger, "tech broker unlocks", warnIfMissing: true))
            {
                if (!Kinds.Any(kind => line.Contains($"\"event\":\"{kind}\"", StringComparison.Ordinal))
                    || !JournalEvent.TryParse(line, logger, out var parsed) || parsed is null)
                {
                    continue;
                }

                if (parsed.Kind == "NewCommander")
                {
                    if (parsed.String("FID") is { Length: > 0 } created)
                    {
                        _ = unlocks.Remove(created);
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

                if (commander.Length > 0)
                {
                    var held = unlocks.GetValueOrDefault(commander, TechBrokerUnlocks.Empty);
                    var now = held.Apply(parsed);

                    if (now.IsKnown)
                    {
                        unlocks[commander] = now;
                    }
                }
            }
        }

        logger.LogInformation("Tech broker unlocks recovered from history for {Count} Commanders", unlocks.Count);

        return unlocks;
    }
}
