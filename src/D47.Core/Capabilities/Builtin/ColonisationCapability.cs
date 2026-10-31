using System.Globalization;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// What a construction site still needs, what the Commander has to give it, and where the next one
/// could go (Phase 18, "Colonisation and construction tracking" and "Find somewhere worth colonising").
/// </summary>
public static class ColonisationCapability
{
    public const string Id = "colonisation";

    public const string PlannerKey = "knowledge.colonisationPlanner";

    public const string ProgressKey = "knowledge.colonisationProgress";

    public const string KeyRow = "knowledge.ravenColonialKey";

    public const string KeySecretName = "ravencolonial.apiKey";

    /// <summary><param name="commander"> The active Commander, or null before any journal has been read.</summary>
    /// <param name="commander">
    /// The active Commander, or null before any journal has been read.
    /// </param>
    /// <param name="galaxy">
    /// The galaxy index, or null where none is composed — under the designer, and in a test that is not
    /// about it.
    /// </param>
    /// <param name="trade">Where the shopping list comes from (Phase 50).</param>
    /// <param name="sourcing">Where the last shopping list is kept, for a surface to draw.</param>
    public static CapabilityDescriptor Create(
        Func<CommanderGameState?> commander,
        IGalaxyService? galaxy = null,
        Configuration.SettingsService? settings = null,
        ITradePlanService? trade = null,
        SourcingBoard? sourcing = null,
        Func<DateTimeOffset>? now = null,
        Func<ColonisationPlans?>? plans = null,
        Func<ColonisationProgress?>? progress = null) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Colonisation",
        Summary =
            "What your construction sites still need, what you are already carrying towards them, "
            + "what is left to haul — and which nearby systems have the bodies your next colony wants.",
        Examples =
        [
            "what does my construction site still need",
            "what is left to deliver",
            "how far along is the construction",
            "what am I carrying for the build",
            "find me somewhere worth colonising",
        ],
        // Phrases rather than bare words. "construction" alone would hijack any sentence that happens to
        // contain it, and both spellings of "colonisation" belong to the plan as much as to the tracking —
        // the checklist owns "what am I building", and this owns the site.
        Keywords =
        [
            new("construction site", "get_construction_sites"),
            new("construction sites", "get_construction_sites"),
            new("colonisation site", "get_construction_sites"),
            new("colonization site", "get_construction_sites"),
            new("left to deliver", "get_construction_needs"),
            new("worth colonising", "find_colonisation_candidates"),
            new("worth colonizing", "find_colonisation_candidates"),
            new("where to colonise", "find_colonisation_candidates"),
            new("where to colonize", "find_colonisation_candidates"),
        ],
        Tools =
        [
            new ToolDefinition
            {
                Name = "get_construction_sites",
                Exercise =
                    "Ask D47 about your construction sites. The reply lists each site with where it is, how far "
                    + "along it is, how many commodities are outstanding, and when the journal last saw it.",
                Description =
                    "Every construction site this Commander's journal has reported: where it is, how "
                    + "far along it is, how many commodities are still outstanding, and when they last "
                    + "saw it. Those figures are as of that visit, not live. Where a plan is read from "
                    + "the colonisation planner, also the planned and building sites no depot has been seen for.",
                Parameters =
                [
                    ToolParameter.Flag(
                        "include_finished",
                        "Also list sites that are complete or have failed. Default false — a "
                            + "finished site cannot be hauled to."),
                ],
                Handler = (arguments, cancellationToken) =>
                    SitesAsync(commander(), settings, plans?.Invoke(), arguments, Now(now), cancellationToken),
            },
            new ToolDefinition
            {
                Name = "get_construction_needs",
                Exercise =
                    "Ask D47 what a construction site still needs. The reply lists each outstanding commodity with "
                    + "how much is left, how much is in your hold, and the trips your ship's capacity implies.",
                Description =
                    "The hauling list for one construction site: every commodity still outstanding, how "
                    + "much is left of each, how much of it is already in the cargo hold, and how many "
                    + "trips the ship's capacity implies. Can also say where to buy the whole list. Names "
                    + "a site by its station or system, or by the name or build type of a planned site; "
                    + "with no name, the only site under construction, or a list to choose from if there "
                    + "are several. Where a plan is read from the colonisation planner, its figures are given beside "
                    + "the depot's.",
                Parameters =
                [
                    ToolParameter.Text(
                        "site",
                        "The station or system name of the site, or the name or build type of a "
                            + "planned site. Leave out when only one is under construction."),
                    ToolParameter.Flag(
                        "where_to_buy",
                        "Also work out which nearby stations between them stock the whole list."),
                ],
                Handler = (arguments, cancellationToken) =>
                    NeedsAsync(
                        commander(), settings, trade, sourcing, plans?.Invoke(), now, arguments, cancellationToken),
            },
            new ToolDefinition
            {
                Name = "find_colonisation_candidates",
                Exercise =
                    "Ask D47 for colonisation candidates near Sol. The reply lists unpopulated systems in claim "
                    + "range, best first, with the bodies they hold and the reasons.",
                Description =
                    "Unpopulated systems within claim range that hold the bodies a colony wants, best "
                    + "colony first with the reasons: how many bodies, of what kinds, how many can be "
                    + "landed on, which have rings, and how far apart they are. No index outside the game can see a claim, so these are "
                    + "systems to check in the System Colonisation Contact.",
                Parameters =
                [
                    ToolParameter.Text(
                        "near",
                        "Search out from this system. Defaults to where the Commander is, which is "
                            + "where they would claim from."),
                    ToolParameter.Text(
                        "body_type",
                        "A kind of planet the system must hold — for example \"Earth-like world\" "
                            + "or \"Class I gas giant\"."),
                    ToolParameter.Flag("terraformable", "Only systems with at least one terraforming candidate."),
                    ToolParameter.Flag("rings", "Only systems with a ringed body in them."),
                    ToolParameter.Integer("landable", "At least this many bodies that can be landed on."),
                    ToolParameter.Number(
                        "max_distance",
                        "How far to look, in light years. Defaults to 15, the furthest a claim "
                            + "reaches, and capped there."),
                    ToolParameter.Integer("limit", "How many to return, 1 to 5. Default 3."),
                ],
                Handler = (arguments, cancellationToken) =>
                    CandidatesAsync(galaxy, settings, commander, arguments, cancellationToken),
            },
        ],
        Settings =
        [
            new SettingRow
            {
                Key = PlannerKey,
                Exercise =
                    "Open Settings › Providers › Outside sources, group Outside sources, and turn Read colonisation "
                    + "plans from Raven Colonial on (it is off by default). Needs a system with an unfinished "
                    + "construction site or a claim in your journals. Ask \"what does my construction site still "
                    + "need\": the answer includes the planner's figures, such as tonnes on linked carriers and the "
                    + "number of Commanders and carriers on the project. With the row off the answer comes from the "
                    + "journal and says the plan could not be read. Needs a network connection to Raven Colonial. "
                    + "Turning this off also greys out Send my colonisation progress.",
                Label = "Read colonisation plans from Raven Colonial",
                Help =
                    "Lets d47 read the plan Raven Colonial holds for a system you are building in. The names of "
                    + "those systems leave this machine; your Commander name does not. Off by default; see "
                    + "[Privacy](privacy) for exactly what is sent.",
                Kind = SettingKind.Toggle,
                DefaultDisplay = "off",
                DocsAnchor = "read-colonisation-plans-from-raven-colonial",
                Binding = SettingBinding.Toggle(
                    s => s.Knowledge.ColonisationPlanner,
                    (s, on) => s with { Knowledge = s.Knowledge with { ColonisationPlanner = on } }),
            },
            new SettingRow
            {
                Key = ProgressKey,
                Exercise =
                    "Open Settings › Providers › Outside sources, group Sharing, and turn Send my colonisation "
                    + "progress to Raven Colonial on. It is greyed out while Read colonisation plans from Raven "
                    + "Colonial is off. It is a protected setting, so the model cannot change it; set it by hand. Needs"
                    + " a Raven Colonial key (see that row) and a construction site that already has a project on Raven"
                    + " Colonial. The line under the switch reads one of \"Sending as CMDR <name>\", \"No Raven "
                    + "Colonial key is set\", \"Raven Colonial refused the key\", \"This key belongs to CMDR <name>, so"
                    + " nothing is sent for CMDR <shown>\", \"Could not ask Raven Colonial whose key this is at <time> "
                    + "UTC\" or \"Last write failed at <time> UTC: <reason>\". In the game, with D47 running, deliver "
                    + "commodities to the depot: the tonnes show on the project on ravencolonial.com. Deliveries made "
                    + "while D47 was not running are never sent. Leave it off if another app already sends your "
                    + "progress.",
                Label = "Send my colonisation progress to Raven Colonial",
                Help =
                    "Sends what you deliver to a construction site, and what it still needs, to that site's "
                    + "project on Raven Colonial as it happens. Needs reading plans on, and a Raven Colonial key. "
                    + "Raven Colonial asks that only one app sends your colonisation progress. If another app you "
                    + "run already does, leave this off or turn it off there.",
                Kind = SettingKind.Toggle,
                DefaultDisplay = "off",
                Protected = true,
                DisabledWhen = s => !s.Knowledge.ColonisationPlanner,
                Note = s => s.Knowledge is { ColonisationPlanner: true, ColonisationProgress: true }
                    ? progress?.Invoke()?.Note
                    : null,
                Watch = refresh =>
                {
                    if (progress?.Invoke() is not { } watched)
                    {
                        return () => { };
                    }

                    watched.Changed += refresh;
                    return () => watched.Changed -= refresh;
                },
                EgressId = Configuration.EgressDisclosure.ColonisationProgress,
                DocsAnchor = "send-colonisation-progress-to-raven-colonial",
                Binding = SettingBinding.Toggle(
                    s => s.Knowledge.ColonisationProgress,
                    (s, on) => s with { Knowledge = s.Knowledge with { ColonisationProgress = on } }),
            },
            new SettingRow
            {
                Key = KeyRow,
                Exercise =
                    "Open Settings › Providers › Outside sources, group Sharing, and paste your key into Raven "
                    + "Colonial key. Get the key from https://ravencolonial.com/user after signing in to Raven "
                    + "Colonial, so this needs a Raven Colonial account. The row never shows the key back. Each "
                    + "Commander has their own key: set it while that Commander is shown. With Send my colonisation "
                    + "progress on, the line under that switch changes to \"Sending as CMDR <name>\" for a valid key, "
                    + "or \"Raven Colonial refused the key\" for a wrong one. With no key it reads \"No Raven Colonial "
                    + "key is set\".",
                Label = "Raven Colonial key",
                Help =
                    "Get your key from https://ravencolonial.com/user after signing in to Raven Colonial. It is "
                    + "stored encrypted for this Windows account and never shown back. Each Commander on this "
                    + "machine has their own key.",
                Kind = SettingKind.Secret,
                Scope = SettingScope.Commander,
                SecretName = KeySecretName,
                DocsAnchor = "raven-colonial-key",
            },
        ],
        Display = new CapabilityDisplay { PanelTitle = "Colonisation", Order = 54 },
    };

    // ------------------------------------------------------------------ sites

    private static DateTimeOffset Now(Func<DateTimeOffset>? now) => now?.Invoke() ?? DateTimeOffset.UtcNow;

    private static async Task<ToolResult> SitesAsync(
        CommanderGameState? state,
        Configuration.SettingsService? settings,
        ColonisationPlans? plans,
        ToolArguments arguments,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (state is null)
        {
            return ToolResult.Ok("No Elite Dangerous journal has been detected yet.");
        }

        var read = await PlanReading
            .ReadAsync(
                plans,
                settings is not null && !settings.Current.Knowledge.ColonisationPlanner,
                PlanReading.SystemsOf(state.Colonisation),
                now,
                cancellationToken)
            .ConfigureAwait(false);

        return ToolResult.Ok(Sites(state, arguments, now, read));
    }

    private static string Sites(CommanderGameState state, ToolArguments arguments, DateTimeOffset now, PlanReading read)
    {
        var finished = arguments.TryGetBoolean("include_finished", out var all) && all;

        var sites = finished ? state.Colonisation.All : state.Colonisation.Current(now);
        var stale = finished ? [] : state.Colonisation.NotSeenSince(now);

        var seen = state.Colonisation.All.Select(site => site.MarketId).ToHashSet();

        var unseen = read.Systems
            .Select(plan => (plan.SystemName, Sites: plan.Sites
                .Where(planned => PlanReading.Live(planned) && (planned.MarketId is not { } id || !seen.Contains(id)))
                .ToList()))
            .Where(system => system.Sites.Count > 0)
            .OrderBy(system => system.SystemName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (sites.Count == 0 && stale.Count == 0 && unseen.Count == 0)
        {
            return JoinNote(Nothing(state, finished), read);
        }

        var report = new StringBuilder();

        report.AppendLine(sites.Count == 0
            ? "No construction site is current."
            : $"{sites.Count} construction site{(sites.Count == 1 ? "" : "s")}, as of your last visit to each:");

        foreach (var site in sites)
        {
            report.AppendLine();
            report.AppendLine(site.Where);

            var line = new List<string> { $"{Percent(site.Progress)} built" };

            if (site.Complete)
            {
                line.Add("complete");
            }

            if (site.Failed)
            {
                line.Add("failed");
            }

            var outstanding = site.Outstanding;

            line.Add(outstanding.Count == 0
                ? "nothing outstanding"
                : $"{outstanding.Count} of {site.Resources.Count} commodities outstanding, "
                    + $"{Tonnes(outstanding.Sum(resource => resource.Remaining))} left");

            report.AppendLine($"  {SpokenText.Capitalised(string.Join(", ", line))}.");
            report.AppendLine($"  Seen {Stamp(site.SeenAt)}.");

            if (read.For(site) is { } planned)
            {
                report.AppendLine($"  Planner: {PlanReading.Describe(planned)}.");
            }
        }

        if (stale.Count > 0)
        {
            report.AppendLine();
            report.AppendLine(
                $"{stale.Count} more not seen in over {ColonisationSites.CurrentFor.Days} days, the newest "
                + $"not since {Stamp(stale[0].SeenAt)}.");
        }

        if (unseen.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("Planned or building in the colonisation planner, with no depot of yours seen:");

            foreach (var (system, planned) in unseen)
            {
                report.AppendLine(system);

                foreach (var site in planned)
                {
                    report.AppendLine($"  {PlanReading.Clip(site.Name)} — {PlanReading.Describe(site)}.");
                }
            }
        }

        report.AppendLine();
        report.AppendLine(Freshness);

        return JoinNote(report.ToString().TrimEnd(), read);
    }

    private static string JoinNote(string answer, PlanReading read) =>
        read.Note() is { } note ? answer + Environment.NewLine + note : answer;

    // ------------------------------------------------------------------ needs

    /// <summary>The hauling list for one site, and — since Phase 50 — where to buy it.</summary>
    private static async Task<ToolResult> NeedsAsync(
        CommanderGameState? state,
        Configuration.SettingsService? settings,
        ITradePlanService? trade,
        SourcingBoard? board,
        ColonisationPlans? plans,
        Func<DateTimeOffset>? now,
        ToolArguments arguments,
        CancellationToken cancellationToken)
    {
        if (state is null)
        {
            return ToolResult.Ok("No Elite Dangerous journal has been detected yet.");
        }

        var wanted = arguments.Text("site")?.Trim();

        var at = Now(now);

        var chosen = Choose(state, wanted, at);

        // A site the journal names needs only its own system's plan; otherwise every system is searched.
        var read = await PlanReading
            .ReadAsync(
                plans,
                settings is not null && !settings.Current.Knowledge.ColonisationPlanner,
                chosen is not null ? [chosen.StarSystem] : PlanReading.SystemsOf(state.Colonisation),
                at,
                cancellationToken)
            .ConfigureAwait(false);

        PlannedSite? planned = null;
        string? plannedSystem = null;

        if (chosen is null && wanted is not null)
        {
            var found = read.Systems
                .SelectMany(plan => plan.Sites
                    .Where(candidate => PlanReading.Live(candidate) && PlanReading.Matches(candidate, wanted))
                    .Select(candidate => (plan.SystemName, Site: candidate)))
                .ToList();

            if (found.Count > 1)
            {
                return ToolResult.Ok(JoinNote(
                    $"{found.Count} planned sites match \"{PlanReading.Clip(wanted)}\" — "
                    + string.Join("; ", found.Select(match =>
                        $"{PlanReading.Clip(match.Site.Name)} ({PlanReading.Describe(match.Site)}), {match.SystemName}"))
                    + ". Which one?",
                    read));
            }

            if (found.Count == 1)
            {
                (plannedSystem, planned) = found[0];
                chosen = planned.MarketId is { } id ? state.Colonisation.ById(id) : null;
            }
        }

        if (chosen is null && planned is not null)
        {
            return ToolResult.Ok(JoinNote(PlannerOnly(plannedSystem!, planned), read));
        }

        if (chosen is not { } site)
        {
            return ToolResult.Ok(JoinNote(Ambiguous(state, wanted, at), read));
        }

        var merged = PlannedNeeds
            .Merge(site, read.For(site))
            .Where(need => need.Symbol.Length > 0)
            .ToDictionary(need => need.Symbol, StringComparer.Ordinal);

        var report = new StringBuilder();

        report.AppendLine($"{site.Where} — {Percent(site.Progress)} built, seen {Stamp(site.SeenAt)}.");

        if (site.Failed)
        {
            report.AppendLine("This build has failed.");
        }

        var outstanding = site.Outstanding;

        if (outstanding.Count == 0)
        {
            report.AppendLine(site.Complete
                ? "It is complete. Nothing outstanding."
                : "Nothing outstanding on the manifest — every commodity it asked for has been delivered.");

            report.AppendLine();
            report.AppendLine(Freshness);

            return ToolResult.Ok(JoinNote(report.ToString().TrimEnd(), read));
        }

        var needs = ConstructionNeeds.For(site, state.Hold, state.Carrier)
            .Select(need => need.Resource.Symbol is { } symbol && merged.TryGetValue(symbol, out var both)
                && both.Remaining < need.Remaining
                    ? need with
                    {
                        Remaining = both.Remaining,
                        ToBuy = Math.Max(0, both.Remaining - need.InHold - (need.OnCarrier ?? 0)),
                    }
                    : need)
            .ToList();
        var left = needs.Sum(need => need.Remaining);
        var aboard = needs.Sum(need => Math.Min(need.Remaining, need.InHold));
        var toBuy = needs.Sum(need => need.ToBuy);

        report.AppendLine();
        report.AppendLine(
            $"{outstanding.Count} commodit{(outstanding.Count == 1 ? "y" : "ies")} outstanding, "
            + $"{Tonnes(left)} in all:");

        var others = state.Colonisation.Current(at).Where(other => other.MarketId != site.MarketId).ToList();

        foreach (var need in needs)
        {
            var resource = need.Resource;
            var detail = new List<string> { $"{Tonnes(need.Remaining)} left" };

            if (resource.Symbol is { } figured && merged.TryGetValue(figured, out var figures))
            {
                if (figures.Planner is { } byPlanner && byPlanner != figures.Depot)
                {
                    detail[0] = byPlanner < figures.Depot
                        ? $"{Tonnes(byPlanner)} left by the planner; your last docking said {Number(figures.Depot)}"
                        : $"{Tonnes(figures.Depot)} left by your last docking; the planner says {Number(byPlanner)}";
                }

                if (figures.OnLinkedCarriers > 0)
                {
                    detail.Add($"{Tonnes(figures.OnLinkedCarriers)} on linked carriers");
                }
            }

            // Capped at what is still wanted.
            if (need.InHold > 0)
            {
                detail.Add(need.InHold >= need.Remaining
                    ? "all of it in the hold"
                    : $"{Tonnes(need.InHold)} in the hold");
            }

            if (need.OnCarrier is > 0 and var onCarrier)
            {
                detail.Add($"{Tonnes(onCarrier)} on the carrier");
            }

            detail.Add($"{Number(resource.Provided)} of {Number(resource.Required)} delivered");

            var line = $"  {resource.Name} — {string.Join(", ", detail)}.";

            if (need.OrderOpen)
            {
                line += " An order is open on the carrier, so its figure may be off by what other "
                    + "Commanders have traded.";
            }

            var sharing = others
                .Where(other => other.Outstanding.Any(row => row.Symbol is not null && row.Symbol == resource.Symbol))
                .Select(other => other.Where)
                .ToList();

            if (sharing.Count > 0 && (need.InHold > 0 || need.OnCarrier is > 0))
            {
                line += $" That stock is shared with {string.Join(" and ", sharing)}.";
            }

            report.AppendLine(line);
        }

        if (read.For(site) is { } project)
        {
            report.AppendLine();
            report.AppendLine(Project(project));
        }

        report.AppendLine();

        foreach (var line in Logistics(state, site, left, aboard, toBuy))
        {
            report.AppendLine(line);
        }

        if (arguments.TryGetBoolean("where_to_buy", out var shopping) && shopping)
        {
            report.AppendLine();
            report.AppendLine(await SourceAsync(
                    state, settings, trade, board, now, site, needs, cancellationToken)
                .ConfigureAwait(false));
        }

        report.AppendLine();
        report.AppendLine(Freshness);

        return ToolResult.Ok(JoinNote(report.ToString().TrimEnd(), read));
    }

    /// <summary>Counts only: the planner's Commanders and carriers are never named.</summary>
    private static string Project(PlannedSite project) =>
        $"The planner's project has {project.Commanders} Commander{(project.Commanders == 1 ? "" : "s")} and "
        + $"{project.LinkedCarriers} linked carrier{(project.LinkedCarriers == 1 ? "" : "s")}. Its figures are "
        + "the planner's, and the lower of the planner's and your last docking's is counted.";

    /// <summary>A planned site no depot of the Commander's has been seen for: the planner's figures alone.</summary>
    private static string PlannerOnly(string system, PlannedSite planned)
    {
        var report = new StringBuilder();

        report.AppendLine(
            $"{PlanReading.Clip(planned.Name)} in {system} — {PlanReading.Describe(planned)}. I have seen no depot "
            + "for it, so everything here is the planner's alone.");

        if (planned.Status != PlanStatus.Build)
        {
            report.AppendLine("It is only planned, so the planner lists nothing outstanding yet.");
        }
        else if (planned.Needed.Count == 0)
        {
            report.AppendLine("The planner lists nothing outstanding.");
        }
        else
        {
            report.AppendLine(
                $"{planned.Needed.Count} commodit{(planned.Needed.Count == 1 ? "y" : "ies")} outstanding, "
                + $"{Tonnes(planned.Needed.Values.Sum())} in all:");

            foreach (var (symbol, left) in planned.Needed.OrderByDescending(pair => pair.Value))
            {
                var onCarriers = planned.OnLinkedCarriers.GetValueOrDefault(symbol);

                report.AppendLine(
                    $"  {PlanReading.Clip(symbol)} — {Tonnes(left)} left"
                    + (onCarriers > 0 ? $", {Tonnes(onCarriers)} on linked carriers." : "."));
            }
        }

        report.AppendLine();
        report.AppendLine(Project(planned));

        return report.ToString().TrimEnd();
    }

    /// <summary>Which stations between them carry the whole list (Phase 50).</summary>
    private static async Task<string> SourceAsync(
        CommanderGameState state,
        Configuration.SettingsService? settings,
        ITradePlanService? trade,
        SourcingBoard? board,
        Func<DateTimeOffset>? now,
        ConstructionSite site,
        IReadOnlyList<ConstructionNeeds> needs,
        CancellationToken cancellationToken)
    {
        if (trade is null || settings is null || !settings.Current.Knowledge.GalaxySearch)
        {
            return "Looking markets up is switched off, so I cannot say where to buy any of it.";
        }

        if (state.Location.StarSystem is not { Length: > 0 } near)
        {
            return "I don't know where the Commander is right now, so I have nowhere to search out from.";
        }

        var toSource = needs.Where(need => need.ToBuy > 0).Select(need => need.ToSource()).ToList();

        if (toSource.Count == 0)
        {
            return "Your hold and carrier cover everything outstanding, so there is nothing to buy.";
        }

        SourcingAnswer answer;

        try
        {
            answer = await trade
                .SourceConstructionAsync(
                    new SourcingSearch(near, state.Location.StationName, toSource), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (GalaxyUnavailableException error)
        {
            return error.Message;
        }

        // Posted on the way out, so a surface draws the answer the Commander was just told rather than
        // running a second search that could disagree with it (the arrangement CommodityBoard already makes
        // for one commodity, and RoutePlanBook for routes).
        if (board is not null)
        {
            board.Post(new SourcingPosting(
                site.Where, answer, near, now?.Invoke() ?? DateTimeOffset.UtcNow));

            board.Announce();
        }

        return Describe(answer, near).TrimEnd();
    }

    /// <summary>The shopping list in words.</summary>
    private static string Describe(SourcingAnswer answer, string near)
    {
        var said = new StringBuilder();

        if (answer.Plan.Stops.Count == 0)
        {
            said.AppendLine($"Nothing within range of {near} is selling any of it.");
        }
        else
        {
            said.AppendLine(
                $"{answer.Plan.Stops.Count} stop{(answer.Plan.Stops.Count == 1 ? string.Empty : "s")} "
                + $"cover{(answer.Plan.Stops.Count == 1 ? "s" : string.Empty)} it, "
                + $"{Credits(answer.Plan.Total)} in all:");

            foreach (var stop in answer.Plan.Stops)
            {
                var lots = stop.Lots
                    .OrderByDescending(lot => lot.Tonnes)
                    .Select(lot => $"{Tonnes(lot.Tonnes)} {lot.Commodity} at {Credits(lot.UnitPrice)}");

                said.AppendLine(
                    $"  {stop.Market.Station} ({stop.Market.System}), {stop.Distance:0.#} ly — "
                    + $"covers {stop.Covers}: {string.Join(", ", lots)}. {Credits(stop.Total)}.");
            }
        }

        if (answer.Plan.Unpriced.Count > 0)
        {
            said.AppendLine($"Nothing in range prices: {string.Join(", ", answer.Plan.Unpriced)}.");
        }

        if (answer.Plan.Shortfalls.Count > 0)
        {
            var short_ = answer.Plan.Shortfalls
                .OrderByDescending(pair => pair.Value)
                .Select(pair => $"{pair.Key} by {Tonnes(pair.Value)}");

            said.AppendLine($"Stocked but not enough: {string.Join(", ", short_)}.");
        }

        if (answer.DroppedAsStale > 0)
        {
            said.AppendLine(
                $"{answer.DroppedAsStale} market{(answer.DroppedAsStale == 1 ? " was" : "s were")} "
                + "left out for quoting prices too old to trust.");
        }

        said.Append("Prices are other Commanders' reports and supply ages fastest during a rush.");

        return said.ToString();
    }

    // Tool result the model reads back, not speech (#336): exact digits on purpose, not SpokenCredits.Band.
    private static string Credits(long value) =>
        $"{value.ToString("N0", CultureInfo.InvariantCulture)} cr";

    /// <summary>
    /// The arithmetic a Commander would otherwise be doing on paper: what is aboard, what the ship can
    /// take, how many runs that is, and what the carrier is holding.
    /// </summary>
    private static IEnumerable<string> Logistics(
        CommanderGameState state,
        ConstructionSite site,
        int left,
        int aboard,
        int toBuy)
    {
        var hold = state.Hold;

        if (!hold.IsKnown)
        {
            yield return
                "I have not read your cargo hold yet, so nothing here is netted off against what you "
                + "are already carrying.";
        }
        else if (aboard > 0)
        {
            // Named as the ship's or the SRV's.
            var vessel = hold.IsShip ? "your hold" : "the SRV's hold";

            yield return $"{Tonnes(aboard)} of that is already in {vessel}, leaving {Tonnes(toBuy)} to find.";
        }
        else
        {
            yield return $"Nothing in your hold counts towards this one. {Tonnes(toBuy)} to find.";
        }

        if (state.Ship.CargoCapacity is { } capacity and > 0)
        {
            var runs = (toBuy + capacity - 1) / capacity;

            yield return runs <= 0
                ? $"Your {Number(capacity)}-tonne hold covers what is left in one run."
                : $"At {Number(capacity)} tonnes a run, that is {runs} more full load{(runs == 1 ? "" : "s")}.";
        }

        if (ColonisationRules.HaulSentence(site, state.Ship.CargoCapacity) is { } haul)
        {
            yield return $"{haul}.";
        }

        if (state.Carrier is { Owned: true, Hold.Reconciled: true } counted)
        {
            var netted = left - aboard - toBuy;
            var checkedAt = Stamp(counted.Hold.CheckedAt);

            yield return netted > 0
                ? $"Your carrier's count matched Elite's cargo total when checked {checkedAt}; "
                    + $"{Tonnes(netted)} of it counts towards this one."
                : $"Your carrier's count matched Elite's cargo total when checked {checkedAt}; "
                    + "none of it counts towards this one.";
        }
        else if (state.Carrier is { Owned: true, CargoTonnes: { } tonnes })
        {
            var since = state.Carrier.Hold.CheckedAt is { } lastChecked
                ? $" The count by commodity has not matched since {Stamp(lastChecked)}, so none of it is netted off."
                : string.Empty;

            yield return tonnes > 0
                ? $"Your carrier was holding {Tonnes(tonnes)} of cargo as of {Stamp(state.Carrier.StatsSeenAt)}. "
                    + "Elite does not write what those tonnes are, so I cannot tell you how much of it "
                    + "belongs on this manifest." + since
                : $"Your carrier was empty as of {Stamp(state.Carrier.StatsSeenAt)}.";
        }

        // What this Commander personally put in, which is not what the depot's figures say — those are
        // everybody's.
        var mine = state.Colonisation.MineDeliveredTo(site.MarketId);

        if (mine.Count > 0)
        {
            yield return
                $"You have delivered {Tonnes(mine.Values.Sum())} here since I started reading this "
                + $"session, across {mine.Count} commodit{(mine.Count == 1 ? "y" : "ies")}. The "
                + "delivered figures above are everybody's.";
        }
    }

    // ------------------------------------------------------------- choosing

    /// <summary>The site being asked about.</summary>
    private static ConstructionSite? Choose(CommanderGameState state, string? wanted, DateTimeOffset now)
    {
        if (wanted is not null)
        {
            return state.Colonisation.Named(wanted);
        }

        var current = state.Colonisation.Current(now);

        return current.Count == 1 ? current[0] : null;
    }

    private static string Ambiguous(CommanderGameState state, string? wanted, DateTimeOffset now)
    {
        var current = state.Colonisation.Current(now);

        if (wanted is not null)
        {
            var known = state.Colonisation.All;

            return known.Count == 0
                ? Nothing(state, finished: false)
                : $"I have no construction site called \"{wanted}\". I know of: "
                    + $"{string.Join("; ", known.Select(site => site.Where))}.";
        }

        if (current.Count == 0)
        {
            return state.Colonisation.NotSeenSince(now) is [var newest, ..]
                ? $"No construction site is current. The most recent is {newest.Where}, not seen since "
                    + $"{Stamp(newest.SeenAt)}. Ask for it by name to see what it last needed."
                : Nothing(state, finished: false);
        }

        return $"{current.Count} sites are under construction — {string.Join("; ", current.Select(site => site.Where))}. "
            + "Which one?";
    }

    /// <summary>What to say when there is nothing to report.</summary>
    private static string Nothing(CommanderGameState state, bool finished)
    {
        if (!state.Colonisation.IsKnown)
        {
            return
                "I have no construction sites yet — a site reports its manifest while you are docked at "
                + "it, so I see one once you have visited it.";
        }

        return finished
            ? "No construction sites at all in what I have seen."
            : "Nothing is under construction in what I have seen. Every site I know of is complete or failed.";
    }

    // ------------------------------------------------------------ candidates

    private static async Task<ToolResult> CandidatesAsync(
        IGalaxyService? galaxy,
        Configuration.SettingsService? settings,
        Func<CommanderGameState?> commander,
        ToolArguments arguments,
        CancellationToken cancellationToken)
    {
        if (galaxy is null || settings is null || !settings.Current.Knowledge.GalaxySearch)
        {
            return ToolResult.Error(
                "Galaxy search is switched off, so I can't look for anywhere to colonise. The Commander "
                + "can turn it on in settings.");
        }

        var near = arguments.Text("near") ?? commander()?.Location.StarSystem;

        if (!ColonisationQuery.TryParse(
                near,
                arguments.TryGetString("body_type", out var bodyType) ? bodyType : null,
                arguments.TryGetBoolean("terraformable", out var terraformable) && terraformable,
                arguments.TryGetBoolean("rings", out var rings) && rings,
                arguments.TryGetInt32("landable", out var landable) ? landable : null,
                arguments.Values.TryGetValue("max_distance", out var raw)
                && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : null,
                arguments.TryGetInt32("limit", out var limit) ? limit : 3,
                out var query,
                out var failure))
        {
            return ToolResult.Error(failure);
        }

        ColonisationScan scan;

        try
        {
            scan = await galaxy.ScanForColonisationAsync(query, cancellationToken).ConfigureAwait(false);
        }
        catch (GalaxyUnavailableException ex)
        {
            return ToolResult.Error(ex.Message);
        }

        // The claim rule, applied here because the index will not apply it.
        var unpopulated = scan.Systems.Where(system => system.Population == 0).ToList();

        var free = unpopulated.Where(system => !system.BeingColonised && !system.Colonised).ToList();

        // Nobody has scanned these.
        var unsurveyed = free.Count(system => system.KnownPlanets == 0);

        var matched = free
            .Where(system => system.KnownPlanets > 0)
            .Where(system => query.Subtype is null || system.Holds(query.Subtype))
            .Where(system => !query.Terraformable || system.Terraformable > 0)
            .ToList();

        // The systems to fetch bodies for, best scan score first.
        var pool = ColonisationRanking.Shortlist(matched).ToList();

        var detail = new Dictionary<string, SystemBodies>(StringComparer.Ordinal);
        var bodies = new Dictionary<string, IReadOnlyList<BodySummary>>(StringComparer.Ordinal);
        var detailFailed = false;

        if (pool.Count > 0)
        {
            try
            {
                var found = await galaxy.FindBodiesAsync(
                    BodyQuery.ForSystems(
                        query.ReferenceSystem,
                        [.. pool.Select(system => system.Name)],
                        query.MaxDistance),
                    cancellationToken).ConfigureAwait(false);

                detail = Fold(found);

                foreach (var group in found.Bodies.GroupBy(body => body.SystemName, StringComparer.Ordinal))
                {
                    bodies[group.Key] = [.. group];
                }
            }
            catch (GalaxyUnavailableException)
            {
                // The scan already answered.
                detailFailed = true;
            }
        }

        if (query.NeedsBodyDetail && !detailFailed)
        {
            pool = [.. pool.Where(system =>
                detail.TryGetValue(system.Name, out var shape)
                && shape.Landable >= query.MinimumLandable
                && (!query.Rings || shape.Rings.Count > 0))];
        }

        var candidates = ColonisationRanking.Rank(pool, bodies).Take(query.Size).ToList();

        // Whether the search was made from somewhere a claim can actually be registered.
        var fromEmpty = scan.Systems.Any(system =>
            system.Population == 0
            && string.Equals(system.Name, scan.Reference ?? query.ReferenceSystem, StringComparison.OrdinalIgnoreCase));

        return ToolResult.Ok(Describe(
            query,
            scan,
            unpopulated.Count,
            unpopulated.Count - free.Count,
            unsurveyed,
            candidates,
            detail,
            detailFailed,
            fromEmpty));
    }

    /// <summary>What the body search adds that the scan cannot see.</summary>
    private sealed record SystemBodies(int Landable, IReadOnlyList<string> Rings);

    private static Dictionary<string, SystemBodies> Fold(BodySearchResult bodies)
    {
        var folded = new Dictionary<string, SystemBodies>(StringComparer.Ordinal);

        foreach (var group in bodies.Bodies.GroupBy(body => body.SystemName, StringComparer.Ordinal))
        {
            var ringed = new List<string>();

            foreach (var body in group.Where(body => body.Rings.Count > 0))
            {
                var types = body.Rings
                    .Select(ring => ring.Type)
                    .Where(type => type is not null)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                // Elite names a body by prefixing its system, so the full name repeats a word the line above
                // just said — "HIP 22711 7 h" against "HIP 22711".
                var name = body.Name.StartsWith(group.Key + " ", StringComparison.OrdinalIgnoreCase)
                    ? body.Name[(group.Key.Length + 1)..]
                    : body.Name;

                ringed.Add(types.Count == 0
                    ? name
                    : $"{name} ({string.Join(", ", types)}"
                        + (body.ReserveLevel is { } reserve ? $", {reserve.ToLowerInvariant()} reserves)" : ")"));
            }

            folded[group.Key] = new SystemBodies(group.Count(body => body.IsLandable), ringed);
        }

        return folded;
    }

    private static string Describe(
        ColonisationQuery query,
        ColonisationScan scan,
        int unpopulated,
        int taken,
        int unsurveyed,
        IReadOnlyList<RankedColonisationSystem> candidates,
        IReadOnlyDictionary<string, SystemBodies> detail,
        bool detailFailed,
        bool fromEmpty)
    {
        var report = new StringBuilder();
        var from = scan.Reference ?? query.ReferenceSystem;

        if (candidates.Count == 0)
        {
            // Three ways to have nothing to show, and they call for three different next actions. "Nothing
            // matched" where the objective was never the problem would send a Commander off to loosen a
            // filter they did not set.
            report.AppendLine(
                unpopulated == 0
                    ? $"Every one of the {scan.Total} systems within {Distance(query.MaxDistance)} of {from} is "
                        + "already populated, so none of them can be claimed."
                : query.HasObjective
                    ? $"{unpopulated} unpopulated system{(unpopulated == 1 ? "" : "s")} within "
                        + $"{Distance(query.MaxDistance)} of {from}, and none of them match what you asked "
                        + "for. A wider objective would find something."
                    : $"{unpopulated} unpopulated system{(unpopulated == 1 ? "" : "s")} within "
                        + $"{Distance(query.MaxDistance)} of {from}, and nothing I can tell you anything "
                        + "useful about.");

            if (taken > 0)
            {
                report.AppendLine(Building(taken));
            }

            if (unsurveyed > 0)
            {
                report.AppendLine(Unsurveyed(unsurveyed));
            }

            report.AppendLine();
            report.AppendLine(CannotSay);

            return report.ToString().TrimEnd();
        }

        report.AppendLine(
            $"{unpopulated} of the {scan.Total} systems within {Distance(query.MaxDistance)} of {from} are "
            + $"unpopulated. {(candidates.Count == 1 ? "One is" : $"{candidates.Count} are")} worth a look, "
            + "best colony first:");

        foreach (var (system, _, reasons) in candidates)
        {
            report.AppendLine();
            report.Append(system.Name);

            if (system.Distance is { } light)
            {
                report.Append($" — {light.ToString("N2", CultureInfo.InvariantCulture)} ly");
            }

            report.AppendLine($", {system.BodyCount} bod{(system.BodyCount == 1 ? "y" : "ies")}.");

            if (reasons.Count > 0)
            {
                report.AppendLine($"  Ranked for {string.Join(", ", reasons)}.");
            }

            if (detail.TryGetValue(system.Name, out var shape))
            {
                report.AppendLine(
                    $"  {shape.Landable} landable"
                    + (shape.Rings.Count == 0
                        ? ", nothing ringed."
                        : $". Rings on {shape.Rings.Count}: {string.Join("; ", shape.Rings.Take(4))}."));
            }

            if (system.Planets.Count > 0)
            {
                // Five kinds at most.
                report.AppendLine(
                    "  " + string.Join(", ", system.Planets.Take(5).Select(planet =>
                        $"{planet.Count} {Plural(planet.Subtype, planet.Count)}")) + ".");
            }

            if (system.Terraformable > 0)
            {
                report.AppendLine(
                    $"  {system.Terraformable} terraforming candidate{(system.Terraformable == 1 ? "" : "s")}.");
            }

            if (system.NearestBody is { } nearest && system.FurthestBody is { } furthest)
            {
                // Planets rather than bodies, because the star sits at zero and a spread that always starts
                // there says nothing about how spread out the system is.
                report.AppendLine($"  Planets {Light(nearest)} to {Light(furthest)} from arrival.");
            }
        }

        report.AppendLine();

        if (taken > 0)
        {
            report.AppendLine(Building(taken));
        }

        if (unsurveyed > 0)
        {
            report.AppendLine(Unsurveyed(unsurveyed));
        }

        if (fromEmpty)
        {
            report.AppendLine(
                $"{from} is itself unpopulated, so there is no System Colonisation Contact there. Claim "
                + "range is measured from the starport you claim at, so this is the wrong place to "
                + "measure from unless you are heading back to one.");
        }

        if (detailFailed)
        {
            report.AppendLine(
                "I couldn't reach the index for the second half of this, so there are no landable or ring "
                + "figures above — the systems and their bodies are still right.");
        }

        report.AppendLine(BuildSlots);
        report.AppendLine(CannotSay);

        return report.ToString().TrimEnd();
    }

    /// <summary>A body kind said of several bodies.</summary>
    private static string Plural(string subtype, int count)
    {
        if (count == 1)
        {
            return subtype;
        }

        var split = subtype.IndexOf(" with ", StringComparison.Ordinal);
        var head = split < 0 ? subtype : subtype[..split];
        var tail = split < 0 ? string.Empty : subtype[split..];
        var space = head.LastIndexOf(' ');
        var last = space < 0 ? head : head[(space + 1)..];

        var plural = last.EndsWith('y') && last.Length > 1 && !"aeiou".Contains(last[^2])
            ? last[..^1] + "ies"
            : last + "s";

        return (space < 0 ? plural : head[..(space + 1)] + plural) + tail;
    }

    private static string Building(int taken) =>
        $"{taken} more unpopulated system{(taken == 1 ? " is" : "s are")} left out because somebody has "
        + "already begun building there.";

    /// <summary>Said rather than swallowed.</summary>
    private static string Unsurveyed(int count) =>
        $"{count} more {(count == 1 ? "is a system" : "are systems")} nobody has surveyed — the index has "
        + $"{(count == 1 ? "its star" : "their stars")} and no planets at all, so I cannot say what is in "
        + $"{(count == 1 ? "it" : "them")}. That is not the same as empty.";

    /// <summary>
    /// Said on every candidate answer, in full, because the thing it rules out is exactly what a
    /// Commander will otherwise assume they have been told.
    /// </summary>
    private const string BuildSlots =
        "Before claiming, check the build slots and where the primary port goes in the system map; no "
        + "index carries either.";

    private const string CannotSay =
        "None of this says a system is free. A claim lasts 24 hours, lives on Frontier's servers, and is "
        + "visible only to the Commander who made it — no index outside the game holds one, so I cannot "
        + "check. These are systems worth opening in the System Colonisation Contact, which is the only "
        + "thing that knows.";

    private static string Distance(double lightYears) =>
        $"{lightYears.ToString("0.#", CultureInfo.InvariantCulture)} light years";

    private static string Light(double seconds) =>
        $"{seconds.ToString("N0", CultureInfo.InvariantCulture)} ls";

    // -------------------------------------------------------------- wording

    /// <summary>Said once per answer rather than per figure.</summary>
    private const string Freshness =
        "A site reports only while you are docked at it, so these are its figures from your last visit "
        + "rather than live ones. Others may have delivered since.";

    private static string Stamp(DateTimeOffset? when) =>
        when is { } at
            ? at.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " game time"
            : "at a time I did not record";

    private static string Percent(double fraction) =>
        (fraction * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static string Tonnes(int value) => $"{Number(value)} tonne{(value == 1 ? "" : "s")}";

    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

}
