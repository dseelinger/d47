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

    public const string MissionDestinationsChart = "mission_destinations";

    public const string DeathsChart = "deaths";

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

    private static readonly string[] MissionPhrases =
    [
        "show my missions on the map",
    ];

    private static readonly string[] DeathPhrases =
    [
        "show where I died",
        "my deaths on the map",
    ];

    public static CapabilityDescriptor Create(Func<CommanderGameState?> state, Func<string, IReadOnlyList<DeathPlace>>? deaths = null) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Star charts",
        Summary = "Adds a chart, such as the Guardian beacons, the material farming sites, your mission destinations or where you died, to the Galaxy map and says what it holds.",
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
                    + "farming sites, the destinations of the Commander's active missions or the systems the Commander has died in. Answers with how many "
                    + "systems the chart marks and, when the Commander's position is known, the nearest one and its distance.",
                Parameters =
                [
                    ToolParameter.OneOf("chart", "The chart to show.", [GuardianBeacons, FarmingSitesChart, MissionDestinationsChart, DeathsChart], required: true),
                ],
                Commands =
                [
                    .. BeaconPhrases.Select(phrase => new ToolCommandPhrase(phrase, new Dictionary<string, string> { ["chart"] = GuardianBeacons })),
                    .. FarmingPhrases.Select(phrase => new ToolCommandPhrase(phrase, new Dictionary<string, string> { ["chart"] = FarmingSitesChart })),
                    .. MissionPhrases.Select(phrase => new ToolCommandPhrase(phrase, new Dictionary<string, string> { ["chart"] = MissionDestinationsChart })),
                    .. DeathPhrases.Select(phrase => new ToolCommandPhrase(phrase, new Dictionary<string, string> { ["chart"] = DeathsChart })),
                ],
                Handler = (arguments, _) => Task.FromResult(Answer(state(), deaths, arguments)),
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

    public static string DescribeDeaths(IReadOnlyList<DeathPlace> places)
    {
        if (places.Count == 0)
        {
            return "I have no record of where you died.";
        }

        var deaths = places.Sum(place => place.Count);

        return $"You have died {deaths} {(deaths == 1 ? "time" : "times")} in {places.Count} {(places.Count == 1 ? "system" : "systems")}.";
    }

    private static ToolResult Answer(CommanderGameState? state, Func<string, IReadOnlyList<DeathPlace>>? deaths, ToolArguments arguments)
    {
        if (!arguments.TryGetString("chart", out var chart))
        {
            return ToolResult.Error($"chart must be {GuardianBeacons}, {FarmingSitesChart}, {MissionDestinationsChart} or {DeathsChart}.");
        }

        if (string.Equals(chart, GuardianBeacons, StringComparison.OrdinalIgnoreCase))
        {
            return ToolResult.Ok(DescribeBeacons(state?.Location.StarPos)) with
            {
                Page = PageRef.StarChart(StarChartKind.GuardianBeacons),
            };
        }

        if (string.Equals(chart, FarmingSitesChart, StringComparison.OrdinalIgnoreCase))
        {
            return ToolResult.Ok(DescribeFarmingSites(state?.Location.StarPos)) with
            {
                Page = PageRef.StarChart(StarChartKind.FarmingSites),
            };
        }

        if (string.Equals(chart, MissionDestinationsChart, StringComparison.OrdinalIgnoreCase))
        {
            return ToolResult.Ok(DescribeMissionDestinations(state?.Missions ?? MissionBoard.Empty)) with
            {
                Page = PageRef.StarChart(StarChartKind.MissionDestinations),
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

        return ToolResult.Error($"chart must be {GuardianBeacons}, {FarmingSitesChart}, {MissionDestinationsChart} or {DeathsChart}.");
    }
}
