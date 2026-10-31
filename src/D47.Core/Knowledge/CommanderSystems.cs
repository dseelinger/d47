using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>The system names a Commander's state holds.</summary>
public static class CommanderSystems
{
    public static IEnumerable<string?> Of(CommanderGameState? commander) =>
        commander is null
            ? []
            : [
                commander.Location.StarSystem,
                commander.Carrier.StarSystem,
                commander.Carrier.DestinationSystem,
                commander.SquadronCarrier.StarSystem,
                commander.SquadronCarrier.DestinationSystem,
                .. commander.Fleet.Ships.Select(ship => ship.StarSystem),
                .. commander.Modules.Modules.Select(module => module.StarSystem),
                .. commander.Colonisation.All.Select(site => site.StarSystem),
            ];
}
