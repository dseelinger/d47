using System.Text.RegularExpressions;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Interface;

/// <summary>Turns Spansh's system, station and faction names into Find names.</summary>
public static partial class GalaxyFindNames
{
    public const string SystemPath = "Star system";

    public const string StationPath = "Station";

    public const string FactionPath = "Minor faction";

    /// <summary>True for a fleet carrier's callsign: three letters or digits, a hyphen, three more.</summary>
    public static bool IsCarrierCallsign(string name) => Callsign().IsMatch(name);

    public static IReadOnlyList<PanelFindName> Of(
        IEnumerable<SystemNameMatch> systems,
        IEnumerable<StationNameMatch> stations,
        IEnumerable<string> factions,
        StarPosition? here) =>
    [
        .. systems.Select(system => new PanelFindName(
            PanelFindKind.StarSystem, system.Name, SystemPath, system.Name, system.SystemAddress)),
        .. stations.Select(station => new PanelFindName(
            IsCarrierCallsign(station.Name) ? PanelFindKind.Carrier : PanelFindKind.Station,
            station.Name,
            StationPath,
            station.Name,
            station.SystemAddress,
            here is { } from ? from.DistanceTo(station.Position) : null)),
        .. factions.Select(faction => new PanelFindName(PanelFindKind.Faction, faction, FactionPath, faction)),
    ];

    [GeneratedRegex("^[A-Za-z0-9]{3}-[A-Za-z0-9]{3}$")]
    private static partial Regex Callsign();
}
