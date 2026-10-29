using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Every Commander's kills, recovered from every journal on disk.</summary>
public static class CombatBackfill
{
    private static readonly string[] Kinds = CommanderEventWalk.Events(
        "Location", "FSDJump", "CarrierJump", "Loadout", "ShipyardSwap",
        "LaunchSRV", "LaunchVessel", "DockSRV", "Embark", "Disembark", "Bounty", "FactionKillBond");

    /// <summary>Each Commander's kills, keyed by Frontier id, over journals oldest-first.</summary>
    public static IReadOnlyDictionary<string, CombatRecord> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var records = new Dictionary<string, CombatRecord>(StringComparer.Ordinal);

        CommanderEventWalk.Each(
            fileSystem,
            files,
            logger,
            "combat kills",
            Kinds,
            cancellation,
            (parsed, commander) =>
            {
                // Names the Commander it resets, which is not the one whose session came before it.
                if (parsed.Kind == "NewCommander")
                {
                    if (parsed.String("FID") is { Length: > 0 } created)
                    {
                        records.Remove(created);
                    }

                    return;
                }

                if (parsed.Kind != "Commander" && commander.Length > 0)
                {
                    records[commander] = (records.TryGetValue(commander, out var held) ? held : CombatRecord.None).Apply(parsed);
                }
            });

        return records;
    }
}
