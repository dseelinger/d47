using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Every Commander's kills, recovered from every journal on disk.</summary>
public static class CombatBackfill
{
    private static readonly string[] Kinds =
    [
        "Commander", "LoadGame", "NewCommander", "Location", "FSDJump", "CarrierJump", "Loadout", "ShipyardSwap",
        "LaunchSRV", "LaunchVessel", "DockSRV", "Embark", "Disembark", "Bounty", "FactionKillBond",
    ];

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
        var commander = string.Empty;

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();

            foreach (var line in JournalLines.Read(fileSystem, file, logger, "combat kills", warnIfMissing: true))
            {
                if (!Kinds.Any(kind => line.Contains($"\"event\":\"{kind}\"", StringComparison.Ordinal))
                    || !JournalEvent.TryParse(line, logger, out var parsed) || parsed is null)
                {
                    continue;
                }

                // Names the Commander it resets, which is not the one whose session came before it.
                if (parsed.Kind == "NewCommander")
                {
                    if (parsed.String("FID") is { Length: > 0 } created)
                    {
                        records.Remove(created);
                    }

                    continue;
                }

                if (parsed.Kind is "Commander" or "LoadGame" && parsed.String("FID") is { Length: > 0 } fid)
                {
                    commander = fid;
                }

                if (parsed.Kind != "Commander" && commander.Length > 0)
                {
                    records[commander] = (records.TryGetValue(commander, out var held) ? held : CombatRecord.None).Apply(parsed);
                }
            }
        }

        return records;
    }
}
