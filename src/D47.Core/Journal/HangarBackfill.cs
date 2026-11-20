using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Every Commander's fighter hangar contents per hull, recovered from every journal on disk.</summary>
public static class HangarBackfill
{
    private static readonly JournalLineFilter Reads = JournalLineFilter.Events(
        "RestockVehicle", "Loadout", "ShipyardSwap", "ModuleBuy", "ModuleSell", "ModuleStore", "ModuleSwap", "ModuleRetrieve");

    /// <summary>Each Commander's hangars, keyed by Frontier id, over journals oldest-first.</summary>
    public static IReadOnlyDictionary<string, ShipHangars> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var hangars = new Dictionary<string, ShipHangars>(StringComparer.Ordinal);
        var hulls = new Dictionary<string, int>(StringComparer.Ordinal);

        CommanderEventWalk.Each(
            fileSystem,
            files,
            logger,
            "hangars",
            Reads,
            cancellation,
            (parsed, commander) =>
            {
                if (parsed.Kind == "NewCommander")
                {
                    if (parsed.String("FID") is { Length: > 0 } created)
                    {
                        _ = hangars.Remove(created);
                        _ = hulls.Remove(created);
                    }

                    return;
                }

                if (commander.Length == 0)
                {
                    return;
                }

                if (parsed.Kind is "Loadout" or "ShipyardSwap" or "LoadGame" && parsed.Int("ShipID") is { } shipId)
                {
                    hulls[commander] = shipId;
                    return;
                }

                var was = hangars.GetValueOrDefault(commander, ShipHangars.Empty);
                var now = was.Apply(parsed, hulls.TryGetValue(commander, out var current) ? current : null);

                if (!ReferenceEquals(was, now))
                {
                    hangars[commander] = now;
                }
            });

        logger.LogInformation("Hangar contents recovered from history for {Count} Commanders", hangars.Count);

        return hangars;
    }
}
