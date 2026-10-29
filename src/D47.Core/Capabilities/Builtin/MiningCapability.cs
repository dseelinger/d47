using System.Globalization;
using D47.Core.Journal;
using D47.Core.Mining;

namespace D47.Core.Capabilities.Builtin;

/// <summary>The mining target the prospector callout judges each rock against, and the limpets a haul takes.</summary>
public static class MiningCapability
{
    public const string Id = "mining";

    public const string SetTool = "set_mining_target";

    public const string ClearTool = "clear_mining_target";

    public const string EstimateTool = "estimate_limpets";

    public const string SitesTool = "list_surface_mining_sites";

    private const int MostSitesSpoken = 3;

    public static CapabilityDescriptor Create(
        MiningTargetStore? store,
        Func<string> frontierId,
        Func<HistoryState>? history = null,
        Func<string, IReadOnlyList<MiningRun>?>? pastRuns = null,
        Func<SurfaceMiningSites?>? surfaceSites = null,
        Func<DateTimeOffset>? now = null) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Mining",
        Summary = "Set the material you are mining for, so prospector results say whether a rock is worth it.",
        Examples = ["mining target painite", "mining target platinum above twenty five percent", "clear the mining target", "where did I surface mine for platinum"],
        Display = new CapabilityDisplay { PanelTitle = "Mining", Order = 51, ShowOnPanel = false },
        Tools =
        [
            new ToolDefinition
            {
                Name = SetTool,
                Exercise =
                    "Say \"mining target platinum\". The reply confirms the target. While it is set, prospector "
                    + "results name only platinum.",
                Description =
                    "Set the material the Commander is mining for, and optionally the percentage a "
                    + "prospected rock has to hold. While it is set, prospector results name only that "
                    + "material and say whether the rock is above or below the percentage.",
                Parameters =
                [
                    ToolParameter.OneOf("material", "The material to mine.", MiningTarget.Materials, required: true),
                    ToolParameter.Number(
                        "percent",
                        "The proportion of the rock, 0 to 100, at or above which it is worth mining. Leave out for any amount."),
                ],
                Commands =
                [
                    .. MiningTarget.Materials.Select(material => new ToolCommandPhrase(
                        $"mining target {material.ToLowerInvariant()}",
                        new Dictionary<string, string>(StringComparer.Ordinal) { ["material"] = material })),
                ],
                Handler = (arguments, _) => Task.FromResult(Set(store, frontierId(), arguments)),
            },
            new ToolDefinition
            {
                Name = ClearTool,
                Exercise =
                    "With a mining target set, say \"clear the mining target\". The reply confirms it, and "
                    + "prospector results name every material in the rock again.",
                Description = "Clear the mining target, so prospector results name every material in the rock again.",
                Commands =
                [
                    new ToolCommandPhrase("clear the mining target", new Dictionary<string, string>(StringComparer.Ordinal)),
                    new ToolCommandPhrase("clear mining target", new Dictionary<string, string>(StringComparer.Ordinal)),
                ],
                Handler = (_, _) => Task.FromResult(Clear(store, frontierId())),
            },
            new ToolDefinition
            {
                Name = EstimateTool,
                Exercise =
                    "Ask D47 how many limpets it takes to mine two hundred tonnes of platinum. The reply gives "
                    + "collector and prospector limpet counts from your own past runs, or says there are none to go "
                    + "on.",
                Description =
                    "Estimate how many collector and prospector limpets it takes to mine a number of tonnes, "
                    + "from the Commander's own past mining runs. With a material, uses the runs that mined "
                    + "mostly that material when there are at least three.",
                Parameters =
                [
                    ToolParameter.Number("tonnes", "The tonnes to mine.", required: true),
                    ToolParameter.OneOf("material", "The material to mine. Leave out for any.", MiningTarget.Materials),
                ],
                Handler = (arguments, _) => Task.FromResult(
                    Estimate(frontierId(), history?.Invoke() ?? HistoryState.Done, pastRuns, arguments)),
            },
            new ToolDefinition
            {
                Name = SitesTool,
                Exercise =
                    "Ask D47 where you have surface mined. The reply lists each body with the tonnes refined, the "
                    + "runs and the last run, latest first. Name a material to list the sites that refined it.",
                Description =
                    "List the bodies where the Commander surface mined from an SRV, such as with a Rhino: "
                    + "the tonnes refined, the runs, the last run and where on the body. With a material, "
                    + "the sites that refined it, most tonnes first; without, every site, latest first.",
                Parameters =
                [
                    ToolParameter.Text(
                        "material",
                        "A commodity refined, such as Platinum or Copper. Leave out for every site."),
                ],
                Handler = (arguments, _) => Task.FromResult(
                    Sites(history?.Invoke() ?? HistoryState.Done, surfaceSites?.Invoke(), now?.Invoke() ?? DateTimeOffset.UtcNow, arguments)),
            },
        ],
    };

    private static ToolResult Set(MiningTargetStore? store, string frontierId, ToolArguments arguments)
    {
        if (store is null || frontierId.Length == 0)
        {
            return ToolResult.Error("No Elite Dangerous journal has been detected yet, so there is nobody to set a target for.");
        }

        if (!arguments.TryGetString("material", out var spoken) || MiningTarget.Match(spoken) is not { } material)
        {
            return ToolResult.Error(
                $"I do not know that material. I can target {string.Join(", ", MiningTarget.Materials)}.");
        }

        double? percent = null;

        if (arguments.TryGetDouble("percent", out var given))
        {
            if (given is < 0 or > 100)
            {
                return ToolResult.Error("The percentage has to be between 0 and 100.");
            }

            percent = given;
        }

        store.Set(frontierId, new MiningTarget(material, percent));

        return ToolResult.Ok(percent is { } floor
            ? $"Mining target: {material} above {floor.ToString("0.#", CultureInfo.InvariantCulture)}%."
            : $"Mining target: {material}.");
    }

    private static ToolResult Clear(MiningTargetStore? store, string frontierId)
    {
        if (store is null || frontierId.Length == 0)
        {
            return ToolResult.Error("No Elite Dangerous journal has been detected yet, so there is no target to clear.");
        }

        return ToolResult.Ok(store.Clear(frontierId) ? "Mining target cleared." : "There was no mining target set.");
    }

    private static ToolResult Estimate(
        string frontierId,
        HistoryState history,
        Func<string, IReadOnlyList<MiningRun>?>? pastRuns,
        ToolArguments arguments)
    {
        if (!arguments.TryGetDouble("tonnes", out var tonnes) || tonnes <= 0)
        {
            return ToolResult.Error("Say how many tonnes, more than zero.");
        }

        string? material = null;

        if (arguments.TryGetString("material", out var spoken) && spoken.Length > 0)
        {
            material = MiningTarget.Match(spoken);

            if (material is null)
            {
                return ToolResult.Error(
                    $"I do not know that material. I can estimate for {string.Join(", ", MiningTarget.Materials)}.");
            }
        }

        switch (history)
        {
            case HistoryState.Pending or HistoryState.Running:
                return ToolResult.Ok("I have not finished reading the journal history yet, so I cannot estimate limpets.");
            case HistoryState.Failed or HistoryState.Stopped:
                return ToolResult.Ok("Reading the journal history did not finish, so I cannot estimate limpets.");
        }

        if (frontierId.Length == 0)
        {
            return ToolResult.Error("No Elite Dangerous journal has been detected yet, so there are no mining runs to estimate from.");
        }

        var runs = pastRuns?.Invoke(frontierId) ?? [];

        return ToolResult.Ok(LimpetEstimate.Describe(runs, tonnes, material) ?? "I have no mining runs to estimate from.");
    }

    private static ToolResult Sites(HistoryState history, SurfaceMiningSites? held, DateTimeOffset now, ToolArguments arguments)
    {
        switch (history)
        {
            case HistoryState.Pending or HistoryState.Running:
                return ToolResult.Ok("I have not finished reading the journal history yet, so I cannot list your surface mining sites.");
            case HistoryState.Failed or HistoryState.Stopped:
                return ToolResult.Ok("Reading the journal history did not finish, so I cannot list your surface mining sites.");
        }

        var material = arguments.TryGetString("material", out var spoken) ? spoken.Trim() : string.Empty;
        var sites = (held ?? SurfaceMiningSites.None).Sites.Where(site => site.Runs.Count > 0).ToList();

        if (material.Length == 0)
        {
            if (sites.Count == 0)
            {
                return ToolResult.Ok("You have no surface mining on record.");
            }

            return ToolResult.Ok(Speak(sites.OrderByDescending(Latest).Select(site => (site, (string?)null)).ToList(), now));
        }

        var named = sites
            .Select(site => (Site: site, Name: RefinedName(site, material), Tonnes: TonnesOf(site, material)))
            .Where(entry => entry.Tonnes > 0)
            .OrderByDescending(entry => entry.Tonnes)
            .ThenByDescending(entry => Latest(entry.Site))
            .ToList();

        if (named.Count == 0)
        {
            return ToolResult.Ok($"You have no surface mining of {material} on record.");
        }

        return ToolResult.Ok(Speak([.. named.Select(entry => (entry.Site, (string?)entry.Name))], now));
    }

    private static DateTimeOffset Latest(SurfaceMiningSite site) => site.Runs.Max(run => run.OpenedAt);

    private static bool Matches(RefinedMaterial refined, string material) =>
        refined.Name.Equals(material, StringComparison.OrdinalIgnoreCase)
        || refined.Symbol.Equals(material, StringComparison.OrdinalIgnoreCase);

    private static int TonnesOf(SurfaceMiningSite site, string material) =>
        site.Runs.Sum(run => run.Refined.Values.Where(refined => Matches(refined, material)).Sum(refined => refined.Tonnes));

    private static string RefinedName(SurfaceMiningSite site, string material) =>
        site.Runs.SelectMany(run => run.Refined.Values).FirstOrDefault(refined => Matches(refined, material))?.Name ?? material;

    private static string Speak(IReadOnlyList<(SurfaceMiningSite Site, string? Material)> entries, DateTimeOffset now)
    {
        var spoken = string.Join(" ", entries.Take(MostSitesSpoken).Select(entry => Describe(entry.Site, entry.Material, now)));

        return entries.Count > MostSitesSpoken ? $"{spoken} and {entries.Count - MostSitesSpoken} more." : spoken;
    }

    private static string Describe(SurfaceMiningSite site, string? material, DateTimeOffset now)
    {
        var where = (site.Body, site.StarSystem) switch
        {
            ({ } body, { } system) => $"{body} in {system}",
            ({ } body, null) => body,
            (null, { } system) => $"an unnamed body in {system}",
            _ => "an unnamed body",
        };

        string refined;

        if (material is not null)
        {
            refined = $"{TonnesOf(site, material)} tonnes of {material} refined";
        }
        else
        {
            var largest = site.Runs
                .SelectMany(run => run.Refined.Values)
                .GroupBy(item => item.Name)
                .Select(group => (Name: group.Key, Tonnes: group.Sum(item => item.Tonnes)))
                .OrderByDescending(item => item.Tonnes)
                .First();

            refined = $"{site.Runs.Sum(run => run.TonnesRefined)} tonnes refined, mostly {largest.Name}";
        }

        var last = Latest(site);
        var date = last.Year == now.Year
            ? last.ToString("d MMMM", CultureInfo.InvariantCulture)
            : last.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
        var runs = site.Runs.Count == 1 ? "1 run" : $"{site.Runs.Count} runs";
        var text = $"{where}: {refined} over {runs}, last on {date}";

        if (site.Runs.LastOrDefault(run => run.At is not null)?.At is { } fix)
        {
            text += $", near latitude {fix.Latitude.ToString("0.0", CultureInfo.InvariantCulture)}, "
                + $"longitude {fix.Longitude.ToString("0.0", CultureInfo.InvariantCulture)}";
        }

        return text + ".";
    }
}
