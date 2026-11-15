using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Every Commander's hired pilots, recovered from every journal on disk.</summary>
public static class CrewBackfill
{
    private static readonly JournalLineFilter Reads = JournalLineFilter.Events(
        "CrewHire", "CrewFire", "CrewAssign", "NpcCrewRank", "ShipyardSwap", "Loadout", "SetUserShipName");

    /// <summary>Each Commander's roster, keyed by Frontier id, over journals oldest-first.</summary>
    public static IReadOnlyDictionary<string, ShipCrew> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var rosters = new Dictionary<string, ShipCrew>(StringComparer.Ordinal);
        var ships = new Dictionary<string, ShipLoadout>(StringComparer.Ordinal);

        CommanderEventWalk.Each(
            fileSystem,
            files,
            logger,
            "crew",
            Reads,
            cancellation,
            (parsed, commander) =>
            {
                if (parsed.Kind == "NewCommander")
                {
                    if (parsed.String("FID") is { Length: > 0 } created)
                    {
                        _ = rosters.Remove(created);
                        _ = ships.Remove(created);
                    }

                    return;
                }

                if (commander.Length == 0)
                {
                    return;
                }

                var ship = ships.GetValueOrDefault(commander, ShipLoadout.Unknown);

                if (parsed.Kind == "Loadout")
                {
                    // The identity alone: the modules are not read.
                    ship = new ShipLoadout
                    {
                        Type = parsed.String("Ship"),
                        TypeName = parsed.Named("Ship"),
                        Name = string.IsNullOrWhiteSpace(parsed.String("ShipName")) ? null : parsed.String("ShipName"),
                        ShipId = parsed.Int("ShipID"),
                    };
                }
                else if (parsed.Kind is "LoadGame" or "SetUserShipName")
                {
                    ship = ship.Apply(parsed);
                }

                ships[commander] = ship;

                if (parsed.Kind is "Loadout" or "LoadGame" or "SetUserShipName")
                {
                    return;
                }

                var now = rosters.GetValueOrDefault(commander, ShipCrew.Empty)
                    .Apply(parsed, ship.Name ?? ship.TypeSaid, ship.ShipId);

                if (now.Any)
                {
                    rosters[commander] = now;
                }
                else
                {
                    _ = rosters.Remove(commander);
                }
            });

        logger.LogInformation("Crew recovered from history for {Count} Commanders", rosters.Count);

        return rosters;
    }
}
