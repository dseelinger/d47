using System.Globalization;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// What a construction site still wants, what the Commander has to give it, and where the next one
/// could go (Phase 18, "Colonisation and construction tracking" and "Find somewhere worth
/// colonising").
/// <para>
/// <b>Two halves that answer different questions from different sources, which is the same shape
/// <see cref="ExobiologyCapability"/> has and for the same reason.</b> Tracking is subtraction over
/// data already on disk — no table, no network, no commodity list, because
/// <c>ColonisationConstructionDepot</c> is a snapshot rather than a delta over 6,330 events and
/// carries <c>Name_Localised</c> on every one of 120,208 rows. Finding is a question about the
/// galaxy, so it goes to the index d47 already queries and is gated by the same switch and the same
/// disclosure as every other question that leaves this machine. Splitting them across two
/// capabilities would put "where should I colonise" and "how is my colony going" in two places for
/// a reason that is about plumbing rather than about anything a Commander would recognise.
/// </para>
/// <para>
/// <b>Everything here is as fresh as the Commander's last visit, and every figure says so.</b> The
/// depot event fires while docked at that very site — 6,307 of 6,330 — so this is a record of where
/// they have been and not a live feed. Reporting it as live would be a wrong answer wearing the
/// shape of a right one, which is the failure mode this capability is most exposed to: the numbers
/// are exact, they are simply about a moment that has passed.
/// </para>
/// <para>
/// <b>Three stores, and only two of them can be itemised.</b> The site says what it wants,
/// <c>Cargo.json</c> says what is in the hold, and the carrier says a tonnage and nothing more. That
/// last one is a measured refusal rather than an omission — see
/// <see cref="CarrierState.CargoTonnes"/>, where deriving a per-commodity carrier stock from
/// <c>CargoTransfer</c> was tried against the corpus and came out wrong twice as often as right.
/// </para>
/// <para>
/// <b>The planning half is not here.</b> A site's objective, its facilities and their order live on
/// the checklist substrate (<see cref="ChecklistCapability"/>, Phase 17), because a
/// colonisation build is the same shape of long-lived intent as an engineering one. This capability
/// reports; it holds no state of its own and proposes nothing.
/// </para>
/// <para>
/// <b>And the finding half promises candidates, never availability.</b> A claim lasts 24 hours, is
/// server-side state that produces exactly one journal line on one Commander's machine, and is
/// therefore invisible to every crowd-fed index there is — Raven Colonial's nineteen endpoints, the
/// most complete in the ecosystem, contain the word "claim" zero times. So the honest sentence is
/// "this system has what your plan wants" and never "this one is free", and the single authority is
/// the System Colonisation Contact in-game. That is a structural property rather than a gap to be
/// closed later, which is why it is said on every answer rather than documented once.
/// </para>
/// </summary>
public static class ColonisationCapability
{
    public const string Id = "colonisation";

    /// <param name="commander">
    /// The active Commander, or null before any journal has been read. Null and "no site has ever
    /// been visited" are different silences and are said differently.
    /// </param>
    /// <param name="galaxy">
    /// The galaxy index, or null where none is composed — under the designer, and in a test that is
    /// not about it. The journal half still answers, which is what a capability being partly off
    /// looks like rather than one being absent (Phase 3).
    /// </param>
    /// <param name="trade">
    /// Where the shopping list comes from (Phase 50). Null under the designer and in tests
    /// that are not about it, and the tracking half still answers — asking where to buy then says
    /// it has nothing composed that reads markets, which is a capability being partly off rather
    /// than absent.
    /// </param>
    /// <param name="carrier">
    /// What the Commander has <em>told</em> d47 is on their fleet carrier, which is taken off the
    /// shopping list. Never derived: see <see cref="CarrierManifest"/> for the measurement that
    /// settled that.
    /// </param>
    /// <param name="sourcing">
    /// Where the last shopping list is posted, so the Checklist tab draws the answer the Commander
    /// was just given rather than searching again.
    /// </param>
    public static CapabilityDescriptor Create(
        Func<CommanderGameState?> commander,
        IGalaxyService? galaxy = null,
        Configuration.SettingsService? settings = null,
        ITradePlanService? trade = null,
        CarrierManifest? carrier = null,
        SourcingBoard? sourcing = null,
        Func<DateTimeOffset>? now = null) => new()
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
        // Phrases rather than bare words. "construction" alone would hijack any sentence that
        // happens to contain it, and both spellings of "colonisation" belong to the plan as much as
        // to the tracking — the checklist owns "what am I building", and this owns the site.
        Keywords =
        [
            "construction site",
            "construction sites",
            "colonisation site",
            "colonization site",
            "left to deliver",
            "worth colonising",
            "worth colonizing",
            "where to colonise",
            "where to colonize",
        ],
        Tools =
        [
            new ToolDefinition
            {
                Name = "get_construction_sites",
                Description =
                    "Every construction site this Commander's journal has reported: where it is, how "
                    + "far along it is, how many commodities are still outstanding, and when they last "
                    + "saw it. Those figures are as of that visit, not live.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "include_finished",
                        Type = ToolParameterType.Boolean,
                        Description =
                            "Also list sites that are complete or have failed. Default false — a "
                            + "finished site cannot be hauled to.",
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(Sites(commander(), arguments))),
            },
            new ToolDefinition
            {
                Name = "get_construction_needs",
                Description =
                    "The hauling list for one construction site: every commodity still outstanding, how "
                    + "much is left of each, how much of it is already in the cargo hold, and how many "
                    + "trips the ship's capacity implies. Can also say where to buy the whole list. Names "
                    + "a site by its station or system; with no name, the only site under construction, or "
                    + "a list to choose from if there are several.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "site",
                        Type = ToolParameterType.String,
                        Description =
                            "The station or system name of the site. Leave out when only one is under "
                            + "construction.",
                    },
                    new ToolParameter
                    {
                        Name = "where_to_buy",
                        Type = ToolParameterType.Boolean,
                        Description = "Also work out which nearby stations between them stock the whole list.",
                    },
                ],
                Handler = (arguments, cancellationToken) =>
                    NeedsAsync(
                        commander(), settings, trade, carrier, sourcing, now, arguments, cancellationToken),
            },
            new ToolDefinition
            {
                Name = "find_colonisation_candidates",
                Description =
                    "Unpopulated systems within claim range that hold the bodies a colony wants: how "
                    + "many bodies, of what kinds, how many can be landed on, which have rings, and how "
                    + "far apart they are. No index outside the game can see a claim, so these are "
                    + "systems to check in the System Colonisation Contact.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "near",
                        Type = ToolParameterType.String,
                        Description =
                            "Search out from this system. Defaults to where the Commander is, which is "
                            + "where they would claim from.",
                    },
                    new ToolParameter
                    {
                        Name = "body_type",
                        Type = ToolParameterType.String,
                        Description =
                            "A kind of planet the system must hold — for example \"Earth-like world\" "
                            + "or \"Class I gas giant\".",
                    },
                    new ToolParameter
                    {
                        Name = "terraformable",
                        Type = ToolParameterType.Boolean,
                        Description = "Only systems with at least one terraforming candidate.",
                    },
                    new ToolParameter
                    {
                        Name = "rings",
                        Type = ToolParameterType.Boolean,
                        Description = "Only systems with a ringed body in them.",
                    },
                    new ToolParameter
                    {
                        Name = "landable",
                        Type = ToolParameterType.Integer,
                        Description = "At least this many bodies that can be landed on.",
                    },
                    new ToolParameter
                    {
                        Name = "max_distance",
                        Type = ToolParameterType.Number,
                        Description =
                            "How far to look, in light years. Defaults to 15, the furthest a claim "
                            + "reaches, and capped there.",
                    },
                    new ToolParameter
                    {
                        Name = "limit",
                        Type = ToolParameterType.Integer,
                        Description = "How many to return, 1 to 5. Default 3.",
                    },
                ],
                Handler = (arguments, cancellationToken) =>
                    CandidatesAsync(galaxy, settings, commander, arguments, cancellationToken),
            },
        ],
        Display = new CapabilityDisplay { PanelTitle = "Colonisation", Order = 54 },
    };

    // ------------------------------------------------------------------ sites

    private static string Sites(CommanderGameState? state, ToolArguments arguments)
    {
        if (state is null)
        {
            return "No Elite Dangerous journal has been detected yet.";
        }

        var finished = arguments.TryGetBoolean("include_finished", out var all) && all;

        var sites = finished ? state.Colonisation.All : state.Colonisation.Active;

        if (sites.Count == 0)
        {
            return Nothing(state, finished);
        }

        var report = new StringBuilder();

        report.AppendLine(
            $"{sites.Count} construction site{(sites.Count == 1 ? "" : "s")}, as of your last visit to each:");

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

            report.AppendLine($"  {Capitalise(string.Join(", ", line))}.");
            report.AppendLine($"  Seen {Stamp(site.SeenAt)}.");
        }

        report.AppendLine();
        report.AppendLine(Freshness);

        return report.ToString().TrimEnd();
    }

    // ------------------------------------------------------------------ needs

    /// <summary>
    /// The hauling list for one site, and — since Phase 50 — where to buy it.
    /// <para>
    /// <b>The outstanding list itself is never recomputed here.</b> <c>ColonisationConstructionDepot</c>
    /// is a snapshot rather than a delta, measured over 6,330 events with <c>RequiredAmount</c> never
    /// moving mid-build, so <see cref="ConstructionSite.Outstanding"/> is a fact off the Commander's
    /// own disk. What the carrier figure changes is the <em>shopping list</em> — what is left to go and
    /// buy — and that distinction is deliberate: recomputing what a site owes is the trap that caught
    /// <c>EngineerProgressState</c> and <c>ModuleStore</c>, silently both times.
    /// </para>
    /// </summary>
    private static async Task<ToolResult> NeedsAsync(
        CommanderGameState? state,
        Configuration.SettingsService? settings,
        ITradePlanService? trade,
        CarrierManifest? carrier,
        SourcingBoard? board,
        Func<DateTimeOffset>? now,
        ToolArguments arguments,
        CancellationToken cancellationToken)
    {
        if (state is null)
        {
            return ToolResult.Ok("No Elite Dangerous journal has been detected yet.");
        }

        var wanted = arguments.TryGetString("site", out var name) && !string.IsNullOrWhiteSpace(name)
            ? name.Trim()
            : null;

        if (Choose(state, wanted) is not { } site)
        {
            return ToolResult.Ok(Ambiguous(state, wanted));
        }

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

            return ToolResult.Ok(report.ToString().TrimEnd());
        }

        var hold = state.Hold;
        var left = outstanding.Sum(resource => resource.Remaining);
        var aboard = outstanding.Sum(resource => Math.Min(resource.Remaining, hold.Of(resource.Symbol)));

        report.AppendLine();
        report.AppendLine(
            $"{outstanding.Count} commodit{(outstanding.Count == 1 ? "y" : "ies")} outstanding, "
            + $"{Tonnes(left)} in all:");

        foreach (var resource in outstanding)
        {
            var detail = new List<string> { $"{Tonnes(resource.Remaining)} left" };

            // Capped at what is still wanted. Twenty tonnes in the hold against six outstanding is
            // six tonnes of progress and fourteen tonnes of something else to do with, and a line
            // reading "20 aboard" against "6 left" invites the Commander to fly there twice.
            if (hold.Of(resource.Symbol) is var carried && carried > 0)
            {
                detail.Add(carried >= resource.Remaining
                    ? "all of it in the hold"
                    : $"{Tonnes(carried)} in the hold");
            }

            detail.Add($"{Number(resource.Provided)} of {Number(resource.Required)} delivered");

            report.AppendLine($"  {resource.Name} — {string.Join(", ", detail)}.");
        }

        report.AppendLine();

        foreach (var line in Logistics(state, site, left, aboard))
        {
            report.AppendLine(line);
        }

        if (arguments.TryGetBoolean("where_to_buy", out var shopping) && shopping)
        {
            report.AppendLine();
            report.AppendLine(await SourceAsync(
                    state, settings, trade, carrier, board, now, site, cancellationToken)
                .ConfigureAwait(false));
        }

        report.AppendLine();
        report.AppendLine(Freshness);

        return ToolResult.Ok(report.ToString().TrimEnd());
    }

    /// <summary>
    /// Which stations between them carry the whole list (Phase 50).
    /// <para>
    /// <b>The unit is <em>this station covers six of your twenty</em></b>, because that is the
    /// sentence a Commander acts on. Not a plotted course: they are flying a loop they will repeat
    /// a dozen times, and the ordering is the easy part and is on the Routing tab. Not a checklist
    /// project either: it is built out of network prices that age in hours, and one that survived a
    /// restart would look current because it was saved.
    /// </para>
    /// <para>
    /// <b>The carrier is taken off the shopping list and never off the site's own figures.</b> What
    /// the Commander says is aboard is a statement of fact d47 cannot check, so it is used, said,
    /// and dated — and the depot's outstanding list above is untouched by it.
    /// </para>
    /// </summary>
    private static async Task<string> SourceAsync(
        CommanderGameState state,
        Configuration.SettingsService? settings,
        ITradePlanService? trade,
        CarrierManifest? carrier,
        SourcingBoard? board,
        Func<DateTimeOffset>? now,
        ConstructionSite site,
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

        var (outstanding, counted) = CarrierManifest.Deduct(
            site.Outstanding, carrier?.For(state.Identity.FrontierId) ?? []);

        var said = new StringBuilder();

        if (counted.Count > 0)
        {
            said.AppendLine(
                $"Taking off what you told me is on the carrier — {Listed(counted)} — "
                + $"as of {Said(counted.Max(stock => stock.SaidAt))}.");
        }

        if (outstanding.Count == 0)
        {
            said.Append("The carrier covers the whole of it. Nothing to buy.");

            return said.ToString().TrimEnd();
        }

        SourcingAnswer answer;

        try
        {
            answer = await trade
                .SourceConstructionAsync(
                    new SourcingSearch(near, state.Location.StationName, outstanding), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (GalaxyUnavailableException error)
        {
            return error.Message;
        }

        // Posted on the way out, so the Checklist tab draws the answer the Commander was just told
        // rather than running a second search that could disagree with it (the arrangement
        // CommodityBoard already makes for one commodity, and RoutePlanBook for routes).
        if (board is not null)
        {
            board.Post(new SourcingPosting(
                site.Where, answer, near, counted, now?.Invoke() ?? DateTimeOffset.UtcNow));

            board.Announce();
        }

        said.Append(Describe(answer, near));

        return said.ToString().TrimEnd();
    }

    /// <summary>
    /// The shopping list in words. <b>Nothing is dropped in silence</b>: every outstanding row
    /// either resolves to a station or is named as one d47 could not price, and found-but-short is
    /// reported separately from never-found, because "widen the search" is right for one and
    /// useless for the other.
    /// </summary>
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

    private static string Listed(IReadOnlyList<CarrierStock> counted) =>
        string.Join(", ", counted.Select(stock => $"{Tonnes(stock.Tonnes)} {stock.Commodity}"));

    private static string Credits(long value) =>
        $"{value.ToString("N0", CultureInfo.InvariantCulture)} cr";

    /// <summary>
    /// The arithmetic a Commander would otherwise be doing on paper: what is aboard, what the ship
    /// can take, how many runs that is, and what the carrier is holding.
    /// </summary>
    private static IEnumerable<string> Logistics(
        CommanderGameState state,
        ConstructionSite site,
        int left,
        int aboard)
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
            // Named as the ship's or the SRV's. Cargo.json is rewritten for whichever the Commander
            // is in, and eight tonnes of SRV scoopings reported as the ship's hold would be a wrong
            // answer nobody could see was wrong.
            var vessel = hold.IsShip ? "your hold" : "the SRV's hold";

            yield return $"{Tonnes(aboard)} of that is already in {vessel}, leaving {Tonnes(left - aboard)} to find.";
        }
        else
        {
            yield return $"Nothing in your hold counts towards this one. {Tonnes(left)} to find.";
        }

        if (state.Ship.CargoCapacity is { } capacity and > 0)
        {
            var runs = (left - aboard + capacity - 1) / capacity;

            yield return runs <= 0
                ? $"Your {Number(capacity)}-tonne hold covers what is left in one run."
                : $"At {Number(capacity)} tonnes a run, that is {runs} more full load{(runs == 1 ? "" : "s")}.";
        }

        // A tonnage and no manifest, which is the whole of what Elite writes. See
        // CarrierState.CargoTonnes for the measurement that closed the itemised version off.
        if (state.Carrier is { Owned: true, CargoTonnes: { } tonnes })
        {
            yield return tonnes > 0
                ? $"Your carrier was holding {Tonnes(tonnes)} of cargo as of {Stamp(state.Carrier.StatsSeenAt)}. "
                    + "Elite does not write what those tonnes are, so I cannot tell you how much of it "
                    + "belongs on this manifest."
                : $"Your carrier was empty as of {Stamp(state.Carrier.StatsSeenAt)}.";
        }

        // What this Commander personally put in, which is not what the depot's figures say — those
        // are everybody's. Only worth a line when there is one.
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

    /// <summary>
    /// The site being asked about. Null where that is nought or several, which
    /// <see cref="Ambiguous"/> then has to distinguish — "I do not know that one" and "which of the
    /// three" are different problems with different next actions.
    /// </summary>
    private static ConstructionSite? Choose(CommanderGameState state, string? wanted)
    {
        if (wanted is not null)
        {
            return state.Colonisation.Named(wanted);
        }

        var active = state.Colonisation.Active;

        return active.Count == 1 ? active[0] : null;
    }

    private static string Ambiguous(CommanderGameState state, string? wanted)
    {
        var active = state.Colonisation.Active;

        if (wanted is not null)
        {
            var known = state.Colonisation.All;

            return known.Count == 0
                ? Nothing(state, finished: false)
                : $"I have no construction site called \"{wanted}\". I know of: "
                    + $"{string.Join("; ", known.Select(site => site.Where))}.";
        }

        if (active.Count == 0)
        {
            return Nothing(state, finished: false);
        }

        return $"{active.Count} sites are under construction — {string.Join("; ", active.Select(site => site.Where))}. "
            + "Which one?";
    }

    /// <summary>
    /// What to say when there is nothing to report. Two silences, and calling them both "no
    /// construction sites" would assert something about the galaxy from evidence about one
    /// Commander's docking history.
    /// </summary>
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

    /// <summary>
    /// How many matching systems are looked at in detail before the shortlist is cut to what was
    /// asked for. The second call costs one request whatever it covers, so this is bounded by how
    /// much of somebody else's index is worth holding at once rather than by request count — five
    /// rich systems already run to two hundred bodies.
    /// </summary>
    private const int DetailPool = 12;

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

        var near = arguments.TryGetString("near", out var given) && !string.IsNullOrWhiteSpace(given)
            ? given
            : commander()?.Location.StarSystem;

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

        // The claim rule, applied here because the index will not apply it. Filtered with
        // predicates rather than set operations: these are records, so Except would compare them by
        // value and quietly collapse two systems that happened to look alike.
        var unpopulated = scan.Systems.Where(system => system.Population == 0).ToList();

        var free = unpopulated.Where(system => !system.BeingColonised && !system.Colonised).ToList();

        // Nobody has scanned these. A star and no planets is what an unsurveyed system looks like
        // in a crowd-fed index, and it is emphatically not a system with no planets — so they are
        // counted out loud rather than either recommended or quietly dropped.
        var unsurveyed = free.Count(system => system.KnownPlanets == 0);

        var matched = free
            .Where(system => system.KnownPlanets > 0)
            .Where(system => query.Subtype is null || system.Holds(query.Subtype))
            .Where(system => !query.Terraformable || system.Terraformable > 0)
            .OrderBy(system => system.Distance ?? double.MaxValue)
            .ToList();

        // How many to fetch the body shape for. Where landability or rings decide the shortlist it
        // has to be wider than the shortlist, because the deciding facts are only in that call;
        // where they do not, the shape is worth reporting for the candidates and fetching for
        // nobody else. One request either way.
        var pool = matched.Take(query.NeedsBodyDetail ? DetailPool : query.Size).ToList();

        var detail = new Dictionary<string, SystemBodies>(StringComparer.Ordinal);
        var detailFailed = false;

        if (pool.Count > 0)
        {
            try
            {
                detail = Fold(await galaxy.FindBodiesAsync(
                    BodyQuery.ForSystems(
                        query.ReferenceSystem,
                        [.. pool.Select(system => system.Name)],
                        query.MaxDistance),
                    cancellationToken).ConfigureAwait(false));
            }
            catch (GalaxyUnavailableException)
            {
                // The scan already answered. Losing the second call costs the landable and ring
                // figures and nothing else, so the answer degrades to what the first call knows and
                // says which half is missing — a refusal here would throw away work that succeeded,
                // including the objective the scan itself could decide.
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

        var candidates = pool.Take(query.Size).ToList();

        // Whether the search was made from somewhere a claim can actually be registered. The
        // contact is a starport service, and an unpopulated system has no starport — so a search
        // from one is measuring fifteen light years out from the wrong place, and it comes back
        // looking exactly like a search from the right one.
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

                // Elite names a body by prefixing its system, so the full name repeats a word the
                // line above just said — "HIP 22711 7 h" against "HIP 22711". The short form is
                // what a Commander reads off the system map anyway.
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
        IReadOnlyList<ColonisationSystem> candidates,
        IReadOnlyDictionary<string, SystemBodies> detail,
        bool detailFailed,
        bool fromEmpty)
    {
        var report = new StringBuilder();
        var from = scan.Reference ?? query.ReferenceSystem;

        if (candidates.Count == 0)
        {
            // Three ways to have nothing to show, and they call for three different next actions.
            // "Nothing matched" where the objective was never the problem would send a Commander
            // off to loosen a filter they did not set.
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
            + "nearest first:");

        foreach (var system in candidates)
        {
            report.AppendLine();
            report.Append(system.Name);

            if (system.Distance is { } light)
            {
                report.Append($" — {light.ToString("N2", CultureInfo.InvariantCulture)} ly");
            }

            report.AppendLine($", {system.BodyCount} bod{(system.BodyCount == 1 ? "y" : "ies")}.");

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
                // Five kinds at most. The tail of a body list is Icy bodies, and a Commander who has
                // heard the first five knows what the system is.
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
                // Planets rather than bodies, because the star sits at zero and a spread that
                // always starts there says nothing about how spread out the system is.
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

        report.AppendLine(CannotSay);

        return report.ToString().TrimEnd();
    }

    /// <summary>
    /// A body kind said of several bodies. Written out because the naive rule produces "18 Rocky
    /// bodys" and "4 Gas giant with water-based lifes" — the head noun is not always the last
    /// word, and the catalogue is a closed sixty-one so the two shapes it uses are all there are.
    /// </summary>
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

    /// <summary>
    /// Said rather than swallowed. These are the ones a Commander on a frontier might most want,
    /// and the only honest thing to report about them is that nobody has looked.
    /// </summary>
    private static string Unsurveyed(int count) =>
        $"{count} more {(count == 1 ? "is a system" : "are systems")} nobody has surveyed — the index has "
        + $"{(count == 1 ? "its star" : "their stars")} and no planets at all, so I cannot say what is in "
        + $"{(count == 1 ? "it" : "them")}. That is not the same as empty.";

    /// <summary>
    /// Said on every candidate answer, in full, because the thing it rules out is exactly what a
    /// Commander will otherwise assume they have been told.
    /// </summary>
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

    /// <summary>
    /// Said once per answer rather than per figure. The numbers are exact and the moment they
    /// describe is not now, and a Commander who reads them as live will fly somewhere for nothing.
    /// </summary>
    private const string Freshness =
        "A site reports only while you are docked at it, so these are its figures from your last visit "
        + "rather than live ones. Others may have delivered since.";

    private static string Stamp(DateTimeOffset? when) =>
        when is { } at
            ? at.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " game time"
            : "at a time I did not record";

    /// <summary>
    /// When the Commander said something. <b>Not <see cref="Stamp"/></b>, which says "game time"
    /// and means it: every other date in this capability comes off a journal event, and this one
    /// came off a keyboard.
    /// </summary>
    private static string Said(DateTimeOffset when) =>
        when.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string Percent(double fraction) =>
        (fraction * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static string Tonnes(int value) => $"{Number(value)} tonne{(value == 1 ? "" : "s")}";

    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Capitalise(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
