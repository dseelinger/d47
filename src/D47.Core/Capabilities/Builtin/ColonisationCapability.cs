using System.Globalization;
using System.Text;
using D47.Core.Journal;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// What a construction site still wants, and what the Commander has to give it (list.md Phase 18,
/// "Colonisation and construction tracking").
/// <para>
/// <b>The whole capability is subtraction over data already on disk.</b> No table, no network, no
/// commodity list — <c>ColonisationConstructionDepot</c> is a snapshot rather than a delta, measured
/// over 6,330 events and 120,208 rows, and carries <c>Name_Localised</c> on every one of them. This
/// is the cheapest item in its phase and the one with the shortest supply chain in the repository:
/// the Commander's own journal, and nothing else.
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
/// the checklist substrate (<see cref="ChecklistCapability"/>, list.md Phase 17), because a
/// colonisation build is the same shape of long-lived intent as an engineering one. This capability
/// reports; it holds no state of its own and proposes nothing.
/// </para>
/// </summary>
public static class ColonisationCapability
{
    public const string Id = "colonisation";

    /// <param name="commander">
    /// The active Commander, or null before any journal has been read. Null and "no site has ever
    /// been visited" are different silences and are said differently.
    /// </param>
    public static CapabilityDescriptor Create(Func<CommanderGameState?> commander) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Construction tracking",
        Summary =
            "What your construction sites still need, what you are already carrying towards them, "
            + "and what is left to haul.",
        Examples =
        [
            "what does my construction site still need",
            "what is left to deliver",
            "how far along is the construction",
            "what am I carrying for the build",
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
        ],
        Tools =
        [
            new ToolDefinition
            {
                Name = "get_construction_sites",
                Description =
                    "Every construction site this Commander's journal has reported: where it is, how "
                    + "far along it is, how many commodities are still outstanding, and when they last "
                    + "saw it. Several sites can be under construction at once. The figures are as of "
                    + "their last visit to each site, not live.",
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
                    + "trips the ship's capacity implies. Names a site by its station or system; with no "
                    + "name, the only site under construction, or a list to choose from if there are "
                    + "several.",
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
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(Needs(commander(), arguments))),
            },
        ],
        Display = new CapabilityDisplay { PanelTitle = "Construction", Order = 53 },
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

    private static string Needs(CommanderGameState? state, ToolArguments arguments)
    {
        if (state is null)
        {
            return "No Elite Dangerous journal has been detected yet.";
        }

        var wanted = arguments.TryGetString("site", out var name) && !string.IsNullOrWhiteSpace(name)
            ? name.Trim()
            : null;

        if (Choose(state, wanted) is not { } site)
        {
            return Ambiguous(state, wanted);
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

            return report.ToString().TrimEnd();
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

        report.AppendLine();
        report.AppendLine(Freshness);

        return report.ToString().TrimEnd();
    }

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

    private static string Percent(double fraction) =>
        (fraction * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static string Tonnes(int value) => $"{Number(value)} tonne{(value == 1 ? "" : "s")}";

    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Capitalise(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
