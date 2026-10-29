using System.Globalization;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>The stations likeliest to have one mission type on their boards, ranked from a systems search.</summary>
internal static class MissionStations
{
    public const string Tool = "find_mission_stations";

    private const int EngineerCount = 3;

    private const string Caveat = "No source publishes what a mission board holds, so these are the likeliest, not certain.";

    public static ToolDefinition Definition(
        IGalaxyService? galaxy,
        Func<string?> currentSystem,
        Configuration.SettingsService settings,
        Func<CommanderGameState?>? gameState) => new()
    {
        Name = Tool,
        Exercise =
            "Ask D47 \"where can I find disable missions near here\". The model calls it; no spoken "
            + "phrase reaches it directly. The reply opens \"The likeliest stations for disable "
            + "missions within\" a distance of the system and lists up to five stations with their "
            + "distance. It never says a board has the mission.",
        Description =
            "Find the five stations likeliest to have one mission type on their boards, near a system: ranked by how many "
            + "factions present are in the states or under the governments that type's names were recorded under, and by "
            + "the system's economy. No source publishes mission boards, so this is likelihood, never a statement that a "
            + "board has the mission.",
        Parameters =
        [
            ToolParameter.Text(
                "mission_type",
                "One mission type as spoken, such as disable or courier, or its internal name such as Mission_Disable.",
                required: true),
            ToolParameter.Text("near", "Search out from this system. Defaults to theirs."),
            ToolParameter.Number(
                "max_distance",
                $"How far to look, in light years. Default {MissionStationQuery.DefaultDistance:0}, "
                    + $"at most {MissionStationQuery.MaxDistanceCeiling:0}."),
        ],
        Handler = (arguments, cancellationToken) =>
            FindAsync(galaxy, currentSystem, settings, gameState?.Invoke(), arguments, cancellationToken),
    };

    public static async Task<ToolResult> FindAsync(
        IGalaxyService? galaxy,
        Func<string?> currentSystem,
        Configuration.SettingsService settings,
        CommanderGameState? state,
        ToolArguments arguments,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetString("mission_type", out var type) || string.IsNullOrWhiteSpace(type))
        {
            return ToolResult.Error("I need a mission type to search for, such as disable or courier.");
        }

        var names = MissionTypes.Resolve(type);

        if (names.Count == 0)
        {
            return ToolResult.Ok(MissionTypes.DescribeUnknown(type));
        }

        var words = MissionTypes.JoinAnd([.. names.Select(name => name.Words).Distinct(StringComparer.OrdinalIgnoreCase)]);

        if (names.All(name => name.Engineer.Length > 0))
        {
            return ToolResult.Ok(DescribeEngineers(words, state?.Location.StarPos));
        }

        var ranks = RankNote(names, state?.Ranks);

        if (galaxy is null || !settings.Current.Knowledge.GalaxySearch)
        {
            return ToolResult.Error(ranks + GalaxyCapability.Unavailable);
        }

        var near = arguments.TryGetString("near", out var explicitNear) && !string.IsNullOrWhiteSpace(explicitNear)
            ? explicitNear
            : currentSystem();

        double? maxDistance = arguments.TryGetDouble("max_distance", out var asked) ? asked : null;

        if (!MissionStationQuery.TryParse(near, names, maxDistance, out var query, out var failure))
        {
            return ToolResult.Error(failure);
        }

        try
        {
            var scan = await galaxy.FindMissionStationsAsync(query, cancellationToken).ConfigureAwait(false);
            var ranked = MissionStationRanking.Rank(query, scan, MissionStationQuery.Size);

            return ToolResult.Ok(ranks + Describe(words, query, scan, ranked));
        }
        catch (GalaxyUnavailableException ex)
        {
            return ToolResult.Error(ex.Message);
        }
    }

    /// <summary>The ranked stations as prose.</summary>
    internal static string Describe(
        string words,
        MissionStationQuery query,
        MissionStationScan scan,
        IReadOnlyList<MissionStationCandidate> ranked)
    {
        var from = scan.Reference ?? query.ReferenceSystem;
        var within = $"within {Number(query.MaxDistance)} light years of {from}";
        var report = new StringBuilder();

        if (ranked.Count == 0)
        {
            var missing = scan.Systems.Count == 0 && query.FactionFilter.Count > 0
                ? $"a faction {Filter(query)}"
                : $"a station with a mission board{SocialSpace(query)}";

            return $"No system {within} has {missing}, so I have no likeliest station for {words} missions there.";
        }

        report.Append($"The likeliest stations for {words} missions {within}");
        report.Append(query.FactionFilter.Count > 0
            ? $", where a faction is {Filter(query)}"
            : $". Their names record no faction state or government, so these are the nearest stations with a mission board{SocialSpace(query)}");

        if (query.Economies.Count > 0)
        {
            report.Append($"; a {JoinOr(query.Economies)} economy counts for one more");
        }

        report.AppendLine(".");

        foreach (var candidate in ranked)
        {
            var station = candidate.Station;

            report.AppendLine();
            report.Append($"{station.Name} in {station.SystemName}");

            if (station.Distance is { } distance)
            {
                report.Append($", {Number(distance)} light years");
            }

            if (query.FiltersOnStates)
            {
                report.Append($", {Count(candidate.MatchingStates, "faction")} in a matching state");
            }
            else if (query.FactionFilter.Count > 0)
            {
                report.Append($", {Count(candidate.MatchingGovernments, "faction")} under a matching government");
            }

            report.Append('.');
        }

        report.AppendLine();

        if (scan.Total > scan.Systems.Count)
        {
            report.AppendLine();
            report.Append($"Ranked over the nearest {scan.Systems.Count} of the {scan.Total} systems that matched.");
        }

        report.AppendLine();
        report.Append(Caveat);

        return report.ToString();
    }

    /// <summary>Engineer missions come from engineer bases, so these name the nearest bases rather than searching.</summary>
    internal static string DescribeEngineers(string words, StarPosition? here)
    {
        var said = $"The likeliest boards for {words} missions of this kind are at engineer bases. ";

        if (here is null)
        {
            return said + "I do not know where you are yet, so I cannot say which bases are nearest.";
        }

        var nearest = EngineerDirectory.All
            .Where(engineer => engineer.System is not null && engineer.Position is not null)
            .OrderBy(engineer => engineer.DistanceFrom(here))
            .ThenBy(engineer => engineer.Name, StringComparer.Ordinal)
            .Take(EngineerCount)
            .Select(engineer => $"{engineer.Name} at {engineer.Where}, {Number(engineer.DistanceFrom(here)!.Value)} light years")
            .ToList();

        return said + $"The nearest to you: {string.Join("; ", nearest)}.";
    }

    /// <summary>One sentence per navy whose rank some of these names need and the Commander does not hold.</summary>
    internal static string RankNote(IReadOnlyList<MissionName> names, RankState? ranks)
    {
        if (ranks is not { IsKnown: true })
        {
            return string.Empty;
        }

        var note = new StringBuilder();
        var everyOne = names.All(name => name.Rank.Length > 0);

        foreach (var navy in names.Select(name => name.Rank).Where(rank => rank.Length > 0)
                     .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (ranks.For(navy) is { Rank: > 0 })
            {
                continue;
            }

            note.Append($"{(everyOne ? "These" : "Some of these")} need {(navy == "Empire" ? "an" : "a")} {navy} navy rank, and you hold none. ");
        }

        return note.ToString();
    }

    private static string Filter(MissionStationQuery query) =>
        query.FiltersOnStates
            ? $"in {JoinOr(query.States)}"
            : $"under a {JoinOr(query.Governments)} government";

    private static string SocialSpace(MissionStationQuery query) =>
        query.OnFoot ? " and a social space, or a settlement" : string.Empty;

    private static string JoinOr(IReadOnlyList<string> items) => items.Count switch
    {
        0 => string.Empty,
        1 => items[0],
        _ => $"{string.Join(", ", items.Take(items.Count - 1))} or {items[^1]}",
    };

    private static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";

    private static string Number(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
