using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Persona;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Opens a chart on the Galaxy map and says what it holds.</summary>
public static class AstrometricsCapability
{
    public const string Id = "astrometrics";

    public const string Tool = "show_star_chart";

    public const string GuardianBeacons = "guardian_beacons";

    public const string FarmingSitesChart = "farming_sites";

    public const string ExobiologyChart = "exobiology";

    public const string MissionDestinationsChart = "mission_destinations";

    public const string FirstDiscoveredChart = "first_discovered";

    public const string DeathsChart = "deaths";

    public const string GuardianSitesChart = "guardian_sites";

    private static readonly string[] BeaconPhrases =
    [
        "show me all known Guardian beacons",
        "show the Guardian beacons",
        "where are the Guardian beacons",
        "Guardian beacons on the map",
    ];

    private static readonly string[] FarmingPhrases =
    [
        "show the farming sites",
        "material farming sites on the map",
    ];

    private static readonly string[] ExobiologyPhrases =
    [
        "show exobiology by region",
    ];

    private static readonly string[] MissionPhrases =
    [
        "show my missions on the map",
    ];

    private static readonly string[] FirstDiscoveryPhrases =
    [
        "show my first discoveries",
        "first discoveries on the map",
    ];

    private static readonly string[] DeathPhrases =
    [
        "show where I died",
        "my deaths on the map",
    ];

    private static readonly string[] GuardianSitePhrases =
    [
        "show the Guardian sites",
        "Guardian sites on the map",
        "where are the Guardian ruins",
    ];

    public static CapabilityDescriptor Create(
        Func<CommanderGameState?> state,
        Func<string, IReadOnlyList<DeathPlace>>? deaths = null,
        Func<string, IReadOnlyList<FirstDiscoveryPlace>>? firstDiscoveries = null) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Star charts",
        Summary = "Adds a chart, such as the Guardian beacons, the Guardian sites, the material farming sites, exobiology by region, your mission destinations, your first discoveries or where you died, to the Galaxy map and says what it holds.",
        Examples = ["show me all known Guardian beacons", "show the farming sites", "show my missions on the map", "show where I died"],
        Display = new CapabilityDisplay { PanelTitle = "Star charts", Order = 71, ShowOnPanel = false },
        Tools =
        [
            new ToolDefinition
            {
                Name = Tool,
                Exercise =
                    "Say \"show me all known Guardian beacons\". D47 adds the Guardian beacons layer to the "
                    + "Galaxy map and says how many beacon systems it knows and which is nearest you.",
                Description =
                    "Adds a chart layer to the Galaxy map and opens the map: the Guardian beacons, the material "
                    + "farming sites, each region filled by how many exobiology species are known there, the destinations of the "
                    + "Commander's active missions, the systems where the Commander was the first "
                    + "to discover a body, the systems the Commander has died in or the systems with a Guardian ruin or structure. Answers with how many "
                    + "systems the chart marks and, when the Commander's position is known, the nearest one and its distance; "
                    + "for exobiology, the fewest and most species a region has and the count for the Commander's region.",
                Parameters =
                [
                    ToolParameter.OneOf("chart", "The chart to show.", [GuardianBeacons, FarmingSitesChart, ExobiologyChart, MissionDestinationsChart, FirstDiscoveredChart, DeathsChart, GuardianSitesChart], required: true),
                ],
                Commands =
                [
                    .. BeaconPhrases.Select(phrase => new ToolCommandPhrase(phrase, new Dictionary<string, string> { ["chart"] = GuardianBeacons })),
                    .. FarmingPhrases.Select(phrase => new ToolCommandPhrase(phrase, new Dictionary<string, string> { ["chart"] = FarmingSitesChart })),
                    .. ExobiologyPhrases.Select(phrase => new ToolCommandPhrase(phrase, new Dictionary<string, string> { ["chart"] = ExobiologyChart })),
                    .. MissionPhrases.Select(phrase => new ToolCommandPhrase(phrase, new Dictionary<string, string> { ["chart"] = MissionDestinationsChart })),
                    .. FirstDiscoveryPhrases.Select(phrase => new ToolCommandPhrase(phrase, new Dictionary<string, string> { ["chart"] = FirstDiscoveredChart })),
                    .. DeathPhrases.Select(phrase => new ToolCommandPhrase(phrase, new Dictionary<string, string> { ["chart"] = DeathsChart })),
                    .. GuardianSitePhrases.Select(phrase => new ToolCommandPhrase(phrase, new Dictionary<string, string> { ["chart"] = GuardianSitesChart })),
                ],
                Handler = (arguments, _) => Task.FromResult(Answer(state(), deaths, firstDiscoveries, arguments)),
            },
        ],
    };

    public static string DescribeBeacons(StarPosition? here)
    {
        var systems = StarCharts.Systems(StarChartKind.GuardianBeacons);
        var count = $"I know {systems.Count} Guardian beacon systems.";

        if (here is not { } position)
        {
            return count;
        }

        var (address, name) = GuardianCores.NearestBeacon(position);
        var distance = GuardianCores.BeaconPositions[address].DistanceTo(position);

        return $"{count} The nearest is {name}, {distance.ToString("0.#", CultureInfo.InvariantCulture)} light years away.";
    }

    public static string DescribeGuardianSites(StarPosition? here)
    {
        var systems = StarCharts.Systems(StarChartKind.GuardianSites);
        var count = $"I know {systems.Count} systems with a Guardian ruin or structure.";

        if (here is not { } position || systems.Count == 0)
        {
            return count;
        }

        var nearest = systems.MinBy(system => system.At.DistanceTo(position))!;

        return $"{count} The nearest is {nearest.Name}, {nearest.At.DistanceTo(position).ToString("0.#", CultureInfo.InvariantCulture)} light years away.";
    }

    public static string DescribeFarmingSites(StarPosition? here)
    {
        var systems = StarCharts.Systems(StarChartKind.FarmingSites);
        var count = $"I know {FarmingSites.All.Count} material farming sites in {systems.Count} systems.";

        if (here is not { } position || systems.Count == 0)
        {
            return count;
        }

        var nearest = systems.MinBy(system => system.At.DistanceTo(position))!;

        return $"{count} The nearest is {nearest.Name}, {nearest.At.DistanceTo(position).ToString("0.#", CultureInfo.InvariantCulture)} light years away.";
    }

    public static string DescribeExobiology(StarPosition? here)
    {
        var counts = ExobiologyCatalogue.SpeciesPerRegion();

        if (counts.Count == 0)
        {
            return "I have no exobiology catalogue to fill the regions from.";
        }

        var fewest = counts.MinBy(pair => pair.Value);
        var most = counts.MaxBy(pair => pair.Value);
        var said = $"The catalogue lists species in {counts.Count} regions, from {fewest.Value} in {GalacticRegions.NameOf(fewest.Key)} "
            + $"to {most.Value} in {GalacticRegions.NameOf(most.Key)}.";

        if (here is not { } position || GalacticRegions.Find(position) is not { } region || GalacticRegions.IdOf(region) is not { } id)
        {
            return said;
        }

        return $"{said} Here in {region} it lists {counts.GetValueOrDefault(id)}.";
    }

    public static string DescribeMissionDestinations(MissionBoard missions)
    {
        var bound = missions.Missions.Where(mission => mission.DestinationSystem is not null).ToList();

        if (bound.Count == 0)
        {
            return "You have no active missions with a destination.";
        }

        var systems = bound.Select(mission => mission.DestinationSystem!).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        return $"You have {bound.Count} active {(bound.Count == 1 ? "mission" : "missions")} bound for {systems} {(systems == 1 ? "system" : "systems")}.";
    }

    public static string DescribeFirstDiscoveries(IReadOnlyList<FirstDiscoveryPlace> places)
    {
        if (places.Count == 0)
        {
            return "I have no record of a body you were the first to discover.";
        }

        var bodies = places.Sum(place => place.Bodies);

        return $"You were the first to discover {bodies} {(bodies == 1 ? "body" : "bodies")} in {places.Count} {(places.Count == 1 ? "system" : "systems")}.";
    }

    public static string DescribeDeaths(IReadOnlyList<DeathPlace> places)
    {
        if (places.Count == 0)
        {
            return "I have no record of where you died.";
        }

        var deaths = places.Sum(place => place.Count);

        return $"You have died {deaths} {(deaths == 1 ? "time" : "times")} in {places.Count} {(places.Count == 1 ? "system" : "systems")}.";
    }

    private static ToolResult Answer(CommanderGameState? state, Func<string, IReadOnlyList<DeathPlace>>? deaths, Func<string, IReadOnlyList<FirstDiscoveryPlace>>? firstDiscoveries, ToolArguments arguments)
    {
        if (!arguments.TryGetString("chart", out var chart))
        {
            return ToolResult.Error($"chart must be {GuardianBeacons}, {FarmingSitesChart}, {ExobiologyChart}, {MissionDestinationsChart}, {FirstDiscoveredChart}, {DeathsChart} or {GuardianSitesChart}.");
        }

        if (string.Equals(chart, GuardianBeacons, StringComparison.OrdinalIgnoreCase))
        {
            return ToolResult.Ok(DescribeBeacons(state?.Location.StarPos)) with
            {
                Page = PageRef.StarChart(StarChartKind.GuardianBeacons),
            };
        }

        if (string.Equals(chart, GuardianSitesChart, StringComparison.OrdinalIgnoreCase))
        {
            return ToolResult.Ok(DescribeGuardianSites(state?.Location.StarPos)) with
            {
                Page = PageRef.StarChart(StarChartKind.GuardianSites),
            };
        }

        if (string.Equals(chart, FarmingSitesChart, StringComparison.OrdinalIgnoreCase))
        {
            return ToolResult.Ok(DescribeFarmingSites(state?.Location.StarPos)) with
            {
                Page = PageRef.StarChart(StarChartKind.FarmingSites),
            };
        }

        if (string.Equals(chart, ExobiologyChart, StringComparison.OrdinalIgnoreCase))
        {
            return ToolResult.Ok(DescribeExobiology(state?.Location.StarPos)) with
            {
                Page = PageRef.StarChart(StarChartKind.Exobiology),
            };
        }

        if (string.Equals(chart, MissionDestinationsChart, StringComparison.OrdinalIgnoreCase))
        {
            return ToolResult.Ok(DescribeMissionDestinations(state?.Missions ?? MissionBoard.Empty)) with
            {
                Page = PageRef.StarChart(StarChartKind.MissionDestinations),
            };
        }

        if (string.Equals(chart, FirstDiscoveredChart, StringComparison.OrdinalIgnoreCase))
        {
            var fid = state?.Identity.FrontierId ?? string.Empty;

            return ToolResult.Ok(DescribeFirstDiscoveries(fid.Length == 0 || firstDiscoveries is null ? [] : firstDiscoveries(fid))) with
            {
                Page = PageRef.StarChart(StarChartKind.FirstDiscovered),
            };
        }

        if (string.Equals(chart, DeathsChart, StringComparison.OrdinalIgnoreCase))
        {
            var fid = state?.Identity.FrontierId ?? string.Empty;

            return ToolResult.Ok(DescribeDeaths(fid.Length == 0 || deaths is null ? [] : deaths(fid))) with
            {
                Page = PageRef.StarChart(StarChartKind.Deaths),
            };
        }

        return ToolResult.Error($"chart must be {GuardianBeacons}, {FarmingSitesChart}, {ExobiologyChart}, {MissionDestinationsChart}, {FirstDiscoveredChart}, {DeathsChart} or {GuardianSitesChart}.");
    }
}
