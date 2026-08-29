using System.Globalization;
using System.Text;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Looking things up in the galaxy (Phase 14, "Galaxy Search").
/// <para>
/// The first capability whose answers come from off this machine, which is what makes its two
/// unusual properties necessary. Filters are validated against a closed vocabulary <em>before</em>
/// a request is built, because the service silently ignores keys it does not recognise and would
/// otherwise answer a different question than the one asked (see <see cref="GalaxyFilters"/>).
/// And every result is a projection: the raw records are hundreds of kilobytes of untrusted
/// third-party text, and what reaches the model is a handful of named fields
/// (see <see cref="SystemSummary"/>).
/// </para>
/// </summary>
public static class GalaxyCapability
{
    /// <summary>The descriptor's id, named once so a help link cannot spell it differently.</summary>
    public const string Id = "galaxy";

    /// <summary>
    /// How far the commodity search looks when nobody said (#157). <b>A default and not a
    /// ceiling</b> — the distinction the Commander had to discover by being told 500 was 250.
    /// </summary>
    private const double DefaultDistance = 50;

    /// <summary>
    /// How stale a quoted price may be when nobody said, in hours — one month, the bound the trade
    /// planner already uses. Inherited rather than invented, for the reason
    /// <see cref="CommoditySearch.MaxPriceAge"/> gives.
    /// </summary>
    private const int DefaultPriceAgeHours = 720;

    /// <summary>
    /// The oldest price the search will consider, a year, matching <see cref="TradeQuery"/>'s own
    /// bound. <b>This one is a ceiling, so it is said out loud when it bites</b> (#157): a price a
    /// year old is barely evidence, and a Commander who asks for two years is owed the number
    /// rather than a silent narrowing of their question.
    /// </summary>
    private const int MaxPriceAgeHours = 8_760;

    /// <summary>How many results the station search returns when nobody said.</summary>
    private const int DefaultLimit = 5;

    /// <summary>
    /// The most <c>limit</c> will return, and the fewest. Bounds rather than a preference: the
    /// answer is spoken, and a model handed forty stations reads out forty stations.
    /// <para>
    /// <b>Said out loud when either bites</b> (<a href="https://github.com/dseelinger/d47/issues/178">#178</a>),
    /// on the precedent <see cref="MaxPriceAgeHours"/> set. Asking for fifty used to return five
    /// with nothing said - reset rather than clamped, so the number that came back had no
    /// relationship to the number asked for, and nothing in the answer admitted it.
    /// </para>
    /// </summary>
    private const int MaxLimit = 20;

    /// <summary>The other end of the same bound. Nought results is a question with no answer.</summary>
    private const int MinLimit = 1;

    /// <param name="galaxy">
    /// The service, or null where none is composed — under the designer and in a test that is not
    /// about it. Null and switched off give the same answer for the same reason: a capability
    /// that cannot act says so and the turn carries on (Phase 3).
    /// </param>
    /// <param name="trade">
    /// The trade planner, which is where whole markets already live (Phase 49). Null where
    /// none is composed, and <c>find_nearest_station</c> then answers the module and ship halves of
    /// its question and says it cannot price a commodity.
    /// </param>
    public static CapabilityDescriptor Create(
        IGalaxyService? galaxy,
        Func<string?> currentSystem,
        Configuration.SettingsService settings,
        ITradePlanService? trade = null,
        Func<string?>? currentStation = null,
        CommodityBoard? board = null,
        Func<DateTimeOffset>? now = null) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Galaxy search",
        Summary = "Look up star systems, and work out how far apart two of them are.",
        Examples =
        [
            "how far is Colonia",
            "find a high tech system within 30 light years",
            "what's the nearest Federation system",
            "where's the nearest Earth-like world",
            "find me a painite hotspot",
        ],

        // No keywords. Every question this answers carries the thing being asked about, and a
        // router that cannot extract an argument cannot route "how far is Colonia" anywhere
        // useful — it would match the phrase and then have nothing to look up. This capability
        // is model-reached or not reached at all, which is the honest version of it.
        Tools =
        [
            new ToolDefinition
            {
                Name = "search_systems",
                Description =
                    "Find star systems matching some criteria, nearest first. Filters: "
                    + $"{GalaxyFilters.Names()}, and no others. Ranges take one number for an upper "
                    + "bound (\"20\") or two separated by a dash (\"10-50\").",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "near",
                        Type = ToolParameterType.String,
                        Description = "Measure from this system. Defaults to theirs.",
                    },
                    new ToolParameter
                    {
                        Name = "distance",
                        Type = ToolParameterType.String,

                        // The range syntax is spelled out in this tool's own description, one
                        // line up. Saying it twice is bytes the action tools pay for.
                        Description = "How far to look, in light years.",
                    },
                    new ToolParameter
                    {
                        Name = "allegiance",
                        Type = ToolParameterType.String,
                        Description = "Superpower allegiance.",
                        AllowedValues = Choices("allegiance"),
                    },
                    new ToolParameter
                    {
                        Name = "government",
                        Type = ToolParameterType.String,
                        Description = "Form of government.",
                        AllowedValues = Choices("government"),
                    },
                    new ToolParameter
                    {
                        Name = "primary_economy",
                        Type = ToolParameterType.String,
                        Description = "The system's main economy.",
                        AllowedValues = Choices("primary_economy"),
                    },
                    new ToolParameter
                    {
                        Name = "security",
                        Type = ToolParameterType.String,
                        Description = "Security level.",
                        AllowedValues = Choices("security"),
                    },
                    new ToolParameter
                    {
                        Name = "state",
                        Type = ToolParameterType.String,
                        Description =
                            "What the controlling faction is going through. Crowd-reported, so this "
                            + "finds systems reported in that state.",
                        AllowedValues = Choices("state"),
                    },
                    new ToolParameter
                    {
                        Name = "limit",
                        Type = ToolParameterType.Integer,
                        Description = "How many to return, 1 to 20. Default 5.",
                    },
                ],
                Handler = (arguments, cancellationToken) =>
                    SearchAsync(galaxy, currentSystem, settings, arguments, cancellationToken),
            },
            new ToolDefinition
            {
                Name = "distance_between",
                Description =
                    "The straight-line distance in light years between two star systems. "
                    + "Leave 'from' out to measure from where the Commander is now.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "to",
                        Type = ToolParameterType.String,
                        Description = "The system to measure to.",
                        Required = true,
                    },
                    new ToolParameter
                    {
                        Name = "from",
                        Type = ToolParameterType.String,
                        Description = "The system to measure from. Defaults to theirs.",
                    },
                ],
                Handler = (arguments, cancellationToken) =>
                    DistanceAsync(galaxy, currentSystem, settings, arguments, cancellationToken),
            },
            new ToolDefinition
            {
                Name = "find_nearest_station",
                Description =
                    "Find the nearest station selling a named module or ship, or trading a commodity "
                    + "— cargo carried in tonnes, never an engineering material.",
                Parameters =
                [
                    // Three short descriptions on purpose. The names carry the meaning here, and
                    // the surface is close enough to its ceiling that prose which repeats a
                    // parameter's own name is prose the Commander's action tools pay for.
                    new ToolParameter
                    {
                        Name = "commodity",
                        Type = ToolParameterType.String,
                        Description = "A commodity traded there, by name.",
                    },
                    new ToolParameter
                    {
                        Name = "selling",
                        Type = ToolParameterType.Boolean,
                        Description = "Sell it rather than buy it.",
                    },
                    new ToolParameter
                    {
                        Name = "tonnes",
                        Type = ToolParameterType.Integer,
                        Description = "How many tonnes, if they said.",
                    },
                    new ToolParameter
                    {
                        Name = "module",
                        Type = ToolParameterType.String,
                        Description = "A module to be sold there, by name — \"Frame Shift Drive\".",
                    },
                    new ToolParameter
                    {
                        Name = "ship",
                        Type = ToolParameterType.String,
                        Description = "A ship to be sold there, by name — \"Krait MkII\".",
                    },
                    new ToolParameter
                    {
                        Name = "module_class",
                        Type = ToolParameterType.String,
                        Description = "Module size, 0 to 8.",
                        AllowedValues = OutfittingCatalogue.Classes,
                    },
                    new ToolParameter
                    {
                        Name = "module_rating",
                        Type = ToolParameterType.String,
                        Description = "Module rating, A to I.",
                        AllowedValues = OutfittingCatalogue.Ratings,
                    },
                    new ToolParameter
                    {
                        Name = "near",
                        Type = ToolParameterType.String,
                        Description = "Search out from this system. Defaults to theirs.",
                    },
                    new ToolParameter
                    {
                        Name = "max_distance",
                        Type = ToolParameterType.Number,
                        Description = "How far to look, in light years. Default 50.",
                    },
                    new ToolParameter
                    {
                        Name = "large_pad",
                        Type = ToolParameterType.Boolean,
                        Description = "Only stations with a large landing pad.",
                    },
                    // The commodity half's own two knobs (#157). Both were decisions taken in the
                    // handler and hidden from the model, so "expand your staleness filter" had no
                    // road from the sentence to the search.
                    new ToolParameter
                    {
                        Name = "max_price_age_hours",
                        Type = ToolParameterType.Integer,
                        Description = "How stale a quoted price may be, in hours. Default 720, one month.",
                    },
                    new ToolParameter
                    {
                        Name = "include_carriers",
                        Type = ToolParameterType.Boolean,
                        Description = "Also fleet carriers, whose prices are player-set and can move.",
                    },
                    new ToolParameter
                    {
                        Name = "limit",
                        Type = ToolParameterType.Integer,
                        Description = "How many to return, 1 to 20. Default 5.",
                    },
                ],
                Handler = (arguments, cancellationToken) =>
                    FindStationAsync(
                        galaxy, trade, currentSystem, currentStation, settings, board, now, arguments, cancellationToken),
            },
            new ToolDefinition
            {
                Name = "find_body",
                Description =
                    "Find the nearest planets, moons or stars matching some criteria — a body type, a "
                    + "surface signal, or a ring to mine.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "body_type",
                        Type = ToolParameterType.String,

                        // Two examples rather than four. They exist to teach the shape of a name,
                        // and two teach it; the other two were paid for on every turn by the
                        // Commander's action tools (Phase 49).
                        Description = "The kind of body, by name — \"Earth-like world\", \"Class I gas giant\".",
                    },
                    new ToolParameter
                    {
                        Name = "signal",
                        Type = ToolParameterType.String,
                        Description =
                            "A signal on the body's surface: \"Biological\", \"Geological\", \"Human\", "
                            + "\"Guardian\" or \"Thargoid\".",
                    },
                    new ToolParameter
                    {
                        Name = "signal_count",
                        Type = ToolParameterType.Integer,
                        Description =
                            "Exactly how many of that signal — not a minimum. Omit unless they asked "
                            + "for a number.",
                    },
                    new ToolParameter
                    {
                        Name = "hotspot",
                        Type = ToolParameterType.String,
                        Description =
                            "A mining hotspot material in the body's rings — \"Painite\", \"Void Opal\".",
                    },
                    new ToolParameter
                    {
                        Name = "hotspot_count",
                        Type = ToolParameterType.Integer,
                        Description =
                            "Exactly how many overlapping hotspots — not a minimum. A triple is 3.",
                    },
                    new ToolParameter
                    {
                        Name = "ring_type",
                        Type = ToolParameterType.String,
                        Description = "Ring composition.",
                        AllowedValues = BodyCatalogue.RingTypes,
                    },
                    new ToolParameter
                    {
                        Name = "reserve_level",
                        Type = ToolParameterType.String,
                        Description = "How rich the rings are.",
                        AllowedValues = BodyCatalogue.ReserveLevels,
                    },
                    new ToolParameter
                    {
                        Name = "landable",
                        Type = ToolParameterType.Boolean,
                        Description = "Only bodies that can be landed on.",
                    },
                    new ToolParameter
                    {
                        Name = "terraformable",
                        Type = ToolParameterType.Boolean,
                        Description = "Only terraforming candidates, which are worth far more to map.",
                    },
                    new ToolParameter
                    {
                        Name = "near",
                        Type = ToolParameterType.String,
                        Description = "Search out from this system. Defaults to theirs.",
                    },
                    new ToolParameter
                    {
                        Name = "max_distance",
                        Type = ToolParameterType.Number,
                        Description = "How far to look, in light years. Default 50.",
                    },
                    new ToolParameter
                    {
                        Name = "limit",
                        Type = ToolParameterType.Integer,
                        Description = "How many to return, 1 to 20. Default 5.",
                    },
                ],
                Handler = (arguments, cancellationToken) =>
                    FindBodyAsync(galaxy, currentSystem, settings, arguments, cancellationToken),
            },
        ],
        Settings =
        [
            new SettingRow
            {
                Key = EnabledKey,
                Label = "Look things up in the galaxy",
                Help =
                    "Lets d47 answer questions about star systems, stations and bodies, and plot routes, "
                    + "by asking spansh.co.uk. System names you ask about, and where you are when the "
                    + "question is relative to you, leave this machine. Off by default; see [Privacy](privacy) for "
                    + "exactly what is sent.",
                Kind = SettingKind.Toggle,
                DefaultDisplay = "off",
                DocsAnchor = "look-things-up-in-the-galaxy",
                Binding = new SettingBinding
                {
                    Read = s => s.Knowledge.GalaxySearch ? "true" : "false",
                    Write = (s, v) => s with
                    {
                        Knowledge = s.Knowledge with { GalaxySearch = v == "true" },
                    },
                },
            },
            new SettingRow
            {
                Key = NotablePlacesKey,
                Label = "Notable places for adventures",
                Help =
                    "Lets a generated adventure pick its stops from the Galactic Exploration Catalog at "
                    + "edastro.com. One request fetches the whole catalogue and the choosing happens here, so "
                    + "where you are never leaves this machine. Off by default; see [Privacy](privacy).",
                Kind = SettingKind.Toggle,
                DefaultDisplay = "off",
                DocsAnchor = "notable-places-for-adventures",
                Binding = new SettingBinding
                {
                    Read = s => s.Knowledge.NotablePlaces ? "true" : "false",
                    Write = (s, v) => s with
                    {
                        Knowledge = s.Knowledge with { NotablePlaces = v == "true" },
                    },
                },
            },
        ],
        Display = new CapabilityDisplay { PanelTitle = "Galaxy search", Order = 47 },
    };

    public const string EnabledKey = "knowledge.galaxy";

    /// <summary>The catalogue of notable places a generated adventure may draw on (Phase 47).</summary>
    public const string NotablePlacesKey = "knowledge.notablePlaces";

    private static IReadOnlyList<string> Choices(string filter) =>
        GalaxyFilters.Find(filter)?.Choices ?? [];

    private static async Task<ToolResult> SearchAsync(
        IGalaxyService? galaxy,
        Func<string?> currentSystem,
        Configuration.SettingsService settings,
        ToolArguments arguments,
        CancellationToken cancellationToken)
    {
        if (galaxy is null || !settings.Current.Knowledge.GalaxySearch)
        {
            return ToolResult.Error(Unavailable);
        }

        var requested = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var filter in GalaxyFilters.All)
        {
            if (arguments.TryGetString(filter.Name, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                requested[filter.Name] = value;
            }
        }

        var near = arguments.TryGetString("near", out var explicitNear) && !string.IsNullOrWhiteSpace(explicitNear)
            ? explicitNear
            : currentSystem();

        if (!arguments.TryGetInt32("limit", out var limit))
        {
            limit = 5;
        }

        if (!GalaxyQuery.TryParse(near, requested, limit, out var query, out var failure))
        {
            return ToolResult.Error(failure);
        }

        if (query.Criteria.Count == 0)
        {
            return ToolResult.Error(
                "That search has no filters, so it would match the whole galaxy. "
                + $"Narrow it with one of: {GalaxyFilters.Describe()}.");
        }

        try
        {
            var result = await galaxy.SearchAsync(query, cancellationToken).ConfigureAwait(false);

            return ToolResult.Ok(Describe(result));
        }
        catch (GalaxyUnavailableException ex)
        {
            return ToolResult.Error(ex.Message);
        }
    }

    private static async Task<ToolResult> DistanceAsync(
        IGalaxyService? galaxy,
        Func<string?> currentSystem,
        Configuration.SettingsService settings,
        ToolArguments arguments,
        CancellationToken cancellationToken)
    {
        if (galaxy is null || !settings.Current.Knowledge.GalaxySearch)
        {
            return ToolResult.Error(Unavailable);
        }

        if (!arguments.TryGetString("to", out var to) || string.IsNullOrWhiteSpace(to))
        {
            return ToolResult.Error("No destination system was named.");
        }

        var from = arguments.TryGetString("from", out var explicitFrom) && !string.IsNullOrWhiteSpace(explicitFrom)
            ? explicitFrom
            : currentSystem();

        if (string.IsNullOrWhiteSpace(from))
        {
            return ToolResult.Error(
                "I don't know where the Commander is right now, so I need the system to measure from.");
        }

        try
        {
            var distance = await galaxy.DistanceAsync(from, to, cancellationToken).ConfigureAwait(false);

            if (distance is null)
            {
                return ToolResult.Error($"I couldn't find one of those systems — '{from}' or '{to}'.");
            }

            return ToolResult.Ok(
                $"{to} is {distance.Value.ToString("N2", CultureInfo.InvariantCulture)} light years from {from}.");
        }
        catch (GalaxyUnavailableException ex)
        {
            return ToolResult.Error(ex.Message);
        }
    }

    private static async Task<ToolResult> FindStationAsync(
        IGalaxyService? galaxy,
        ITradePlanService? trade,
        Func<string?> currentSystem,
        Func<string?>? currentStation,
        Configuration.SettingsService settings,
        CommodityBoard? board,
        Func<DateTimeOffset>? now,
        ToolArguments arguments,
        CancellationToken cancellationToken)
    {
        // **A material is not cargo, and d47 holds the table that says so**
        // (<a href="https://github.com/dseelinger/d47/issues/54">#54</a>). Asked where the closest
        // Core Dynamics Composites were, the model reached for this tool — reasonably, since
        // "closest" and a named thing to buy is what it advertises — and the honest market answer
        // was "not trading within 50 light years", which is true of every engineering material
        // that has ever existed and helps nobody.
        //
        // **Above the availability check on purpose.** This is a local table lookup: it reaches
        // no network, spends nothing, and is the right answer whether or not galaxy search is
        // switched on — so a Commander running local-only gets it too, rather than being told
        // the search is off and left with the wrong idea about what the thing is.
        //
        // Answered rather than redirected, because the catalogue carries where it actually
        // comes from and reading it costs nothing.
        if (arguments.TryGetString("commodity", out var named)
            && MaterialCatalogue.Find(named) is
                { Ledger: not (MaterialLedger.Cargo or MaterialLedger.RareCargo) } material)
        {
            return ToolResult.Ok(MaterialSeam.NotThisOne(material, MaterialSeam.MarketTool));
        }

        if (galaxy is null || !settings.Current.Knowledge.GalaxySearch)
        {
            return ToolResult.Error(Unavailable);
        }

        // The commodity half is a different source and a different ranking, so it forks here
        // rather than threading a second kind of answer through the module search (Phase 49).
        // Everything above it — near, max_distance, large_pad, limit — means the same thing
        // on both sides, which is most of why this is a parameter and not a second tool.
        if (arguments.TryGetString("commodity", out var commodity) && !string.IsNullOrWhiteSpace(commodity))
        {
            return await FindCommodityAsync(
                trade, currentSystem, currentStation, board, now, arguments, commodity, cancellationToken)
                .ConfigureAwait(false);
        }

        arguments.TryGetString("module", out var module);
        arguments.TryGetString("ship", out var ship);
        arguments.TryGetString("module_class", out var moduleClass);
        arguments.TryGetString("module_rating", out var moduleRating);

        var near = arguments.TryGetString("near", out var explicitNear) && !string.IsNullOrWhiteSpace(explicitNear)
            ? explicitNear
            : currentSystem();

        if (string.IsNullOrWhiteSpace(near))
        {
            return ToolResult.Error(
                "I don't know where the Commander is right now, so I need a system to search out from.");
        }

        arguments.TryGetBoolean("large_pad", out var largePad);

        double? maxDistance = arguments.Values.TryGetValue("max_distance", out var raw)
                              && double.TryParse(
                                  raw,
                                  System.Globalization.NumberStyles.Float,
                                  CultureInfo.InvariantCulture,
                                  out var parsed)
            ? parsed
            : null;

        var limit = Limit(arguments, out var askedLimit);

        if (!StationQuery.TryParse(
                near, module, moduleClass, moduleRating, ship, largePad, maxDistance, limit,
                out var query,
                out var failure))
        {
            return ToolResult.Error(failure);
        }

        try
        {
            var result = await galaxy.FindStationsAsync(query, cancellationToken).ConfigureAwait(false);

            return ToolResult.Ok(Describe(result, query) + LimitRefused(askedLimit));
        }
        catch (GalaxyUnavailableException ex)
        {
            return ToolResult.Error(ex.Message);
        }
    }

    /// <summary>
    /// Where to buy a commodity, or where to dump one (Phase 49).
    /// </summary>
    private static async Task<ToolResult> FindCommodityAsync(
        ITradePlanService? trade,
        Func<string?> currentSystem,
        Func<string?>? currentStation,
        CommodityBoard? board,
        Func<DateTimeOffset>? now,
        ToolArguments arguments,
        string commodity,
        CancellationToken cancellationToken)
    {
        if (trade is null)
        {
            return ToolResult.Error(
                "I can find modules and ships, but I have nothing composed that reads commodity markets, "
                + "so I cannot price one.");
        }

        var near = arguments.TryGetString("near", out var explicitNear) && !string.IsNullOrWhiteSpace(explicitNear)
            ? explicitNear
            : currentSystem();

        if (string.IsNullOrWhiteSpace(near))
        {
            return ToolResult.Error(
                "I don't know where the Commander is right now, so I need a system to search out from.");
        }

        arguments.TryGetBoolean("large_pad", out var largePad);
        arguments.TryGetBoolean("selling", out var selling);

        int? tonnes = arguments.TryGetInt32("tonnes", out var asked) && asked > 0 ? asked : null;

        var limit = Limit(arguments, out var askedLimit);

        // <b>No upper clamp</b> (#157). This used to be Math.Clamp(parsed, 1, 250), so "expand
        // your search out to 500 light years" became 250 with nothing said, and the only honest
        // thing left to the model was to report a cap the Commander had not asked for. What
        // bounds the radius now is what the source honours and what the sweep can reach — and the
        // sweep already says how far it got (#156), which is the truthful version of the sentence
        // the cap was making up.
        var maxDistance = arguments.Values.TryGetValue("max_distance", out var raw)
                          && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                          && parsed > 0
            ? parsed
            : DefaultDistance;

        arguments.TryGetBoolean("include_carriers", out var includeCarriers);

        var askedAge = arguments.TryGetInt32("max_price_age_hours", out var hours) && hours > 0
            ? hours
            : (int?)null;

        var maxPriceAge = Math.Clamp(askedAge ?? DefaultPriceAgeHours, 1, MaxPriceAgeHours);

        var query = new CommodityQuery(
            commodity.Trim(),
            selling ? TradeSide.Selling : TradeSide.Buying,
            tonnes,
            maxDistance,
            largePad,
            includeCarriers,
            limit);

        try
        {
            var answer = await trade
                .FindCommodityAsync(
                    new CommoditySearch(near, currentStation?.Invoke(), query, maxPriceAge), cancellationToken)
                .ConfigureAwait(false);

            // Posted on the way out, so the Routing tab draws the answer the Commander was just
            // told rather than running a second search that could disagree with it (Phase 49; the
            // arrangement RoutePlanBook already makes for routes).
            if (board is not null)
            {
                board.Post(new CommodityPosting(
                    query, answer, near, now?.Invoke() ?? DateTimeOffset.UtcNow));

                board.Announce();
            }

            return ToolResult.Ok(DescribeCommodity(query, answer, near, maxPriceAge, askedAge, askedLimit));
        }
        catch (GalaxyUnavailableException ex)
        {
            return ToolResult.Error(ex.Message);
        }
    }

    /// <summary>
    /// The answer, and the date on every price in it.
    /// <para>
    /// <b>This is the way this feature is wrong while looking right.</b> A quoted supply figure
    /// ages badly — a colonisation rush strips a station in hours — and an answer that sounds
    /// current is worse than one that admits it is a month old. So every line carries its age, a
    /// market the Commander stood in themselves is called theirs, and stations dropped for being
    /// too old are counted rather than swallowed: "nothing within fifty light years" and "eleven
    /// stations, all quoting last month" are different answers and only one of them means widen
    /// the search.
    /// </para>
    /// <para>
    /// <b>And the question is echoed when the Commander changed it</b> (#157). The knobs are all
    /// arguments now, so an answer that reads the same whether it swept fifty light years or five
    /// hundred leaves them no way to hear that their instruction landed — which is how <i>"expand
    /// your search"</i> came to look like an instruction being ignored. The existing answer already
    /// dates every price; this is the same honesty applied to the question rather than the result.
    /// </para>
    /// </summary>
    /// <param name="query">What was asked, knobs and all.</param>
    /// <param name="answer">What came back.</param>
    /// <param name="near">The reference system.</param>
    /// <param name="maxPriceAge">The staleness bound actually used, in hours.</param>
    /// <param name="askedPriceAge">
    /// The staleness bound the model asked for, if it asked. Different from
    /// <paramref name="maxPriceAge"/> only where the ask was refused, which is the one case that
    /// has to name the knob and the ceiling rather than answering as though nothing happened.
    /// </param>
    /// <param name="askedLimit">
    /// How many results the model asked for, if it asked. Out of range it owes the same sentence
    /// for the same reason (#178); in range and non-default it is echoed like the others.
    /// </param>
    private static string DescribeCommodity(
        CommodityQuery query,
        CommodityAnswer answer,
        string near,
        int maxPriceAge,
        int? askedPriceAge,
        int? askedLimit)
    {
        var verb = query.Side == TradeSide.Buying ? "buying" : "selling";

        if (answer.Offers.Count == 0)
        {
            // <b>The radius answered is the radius searched</b> (#156). Where the search ran out
            // of budget before it ran out of galaxy, saying "nothing within 250 light years" is a
            // claim about ground it never covered — which is exactly how "No stock of Land Mines
            // within 150 ly of Eurybia" came to be said about a system with 5,229 units of them
            // eleven light years away.
            var nothing = answer.Horizon is { } reached
                ? $"Nothing in the {answer.Considered} markets I could check is {verb} {query.Commodity}"
                : $"Nothing within {query.MaxDistance:0} light years of {near} is {verb} {query.Commodity}";

            if (query.Tonnes is { } wanted)
            {
                nothing += $" in {wanted} tonne lots";
            }

            nothing += ".";

            if (answer.Horizon is { } horizon)
            {
                nothing +=
                    $" Those reach {horizon:0.#} light years of the {query.MaxDistance:0} you asked about, "
                    + "so there is more out there I have not looked at.";
            }

            if (answer.DroppedAsStale > 0)
            {
                nothing +=
                    $" {answer.DroppedAsStale} of the {answer.Considered} markets nearby were left out for "
                    + "quoting prices too old to trust rather than for having none.";
            }

            return nothing + AsAsked(query, maxPriceAge, askedPriceAge, askedLimit);
        }

        var lines = new List<string>();

        foreach (var offer in answer.Offers)
        {
            var line = $"{offer.Market.Station} ({offer.Market.System})";

            if (answer.OriginKnown)
            {
                line += $", {offer.Distance:0.#} ly";
            }

            line += query.Side == TradeSide.Buying
                ? $", {offer.UnitPrice:N0} cr a tonne, {offer.Market.Quote(query.Commodity)?.Supply ?? 0:N0} in stock"
                : $", pays {offer.UnitPrice:N0} cr a tonne, wants {offer.Market.Quote(query.Commodity)?.Demand ?? 0:N0}";

            if (query.Tonnes is not null)
            {
                line += $", {offer.Total:N0} cr for the load";
            }

            line += $" — {Age(offer)}";

            lines.Add(line);
        }

        // The same rule on the way out (#156). A best-of is a claim about everything it looked
        // at, so the heading names the distance actually reached rather than the one asked for.
        var reach = answer.Horizon is { } far ? $"{far:0.#}" : $"{query.MaxDistance:0}";

        var heading = query.Tonnes is { } load
            ? $"{verb} {load} tonnes of {query.Commodity} within {reach} ly of {near}"
            : $"{verb} {query.Commodity} within {reach} ly of {near}";

        var report = $"Best for {heading}: " + string.Join("; ", lines) + ".";

        if (!answer.OriginKnown)
        {
            report +=
                " I could not place " + near + " from the markets I have, so these are ranked on price alone "
                + "and the distances are unknown.";
        }

        if (answer.DroppedAsStale > 0)
        {
            report += $" {answer.DroppedAsStale} more were left out for quoting prices too old to trust.";
        }

        report += AsAsked(query, maxPriceAge, askedPriceAge, askedLimit);

        report += " Prices are reported by other Commanders and can be out of date; supply moves fastest.";

        return report;
    }

    /// <summary>
    /// The knobs the Commander turned, said back to them, and the one that could not be turned as
    /// far as they asked (#157).
    /// <para>
    /// <b>Silence is the default, and that is the point.</b> An unqualified search says nothing
    /// here and reads exactly as it did before this existed. It is only a question that was
    /// changed which gets told the change took — <i>"out to 500 ly, prices up to 60 days old"</i>
    /// — because a widened search whose answer sounds identical to a narrow one is
    /// indistinguishable from an instruction that was dropped.
    /// </para>
    /// <para>
    /// <b>And a refused widening names the knob and the ceiling.</b> "That's as far as I search"
    /// is the sentence this issue exists to delete: a bound the Commander cannot see is a bound
    /// they cannot argue with, so the number gets said out loud with the parameter it belongs to.
    /// </para>
    /// </summary>
    private static string AsAsked(
        CommodityQuery query, int maxPriceAge, int? askedPriceAge, int? askedLimit)
    {
        var refused = askedPriceAge is { } asked && asked > MaxPriceAgeHours;

        var turned = new List<string>();

        if (Math.Abs(query.MaxDistance - DefaultDistance) > 0.001)
        {
            turned.Add($"out to {query.MaxDistance:0.#} ly");
        }

        // Same rule as the radius above: a search that returned twelve and one that returned five
        // read identically otherwise (#178). Left out when the ask was refused, because the
        // refusal below already says the number with its reason attached.
        if (query.Limit != DefaultLimit && LimitRefused(askedLimit).Length == 0)
        {
            turned.Add($"up to {query.Limit} results");
        }

        // Left out when the ask was refused, because the refusal below says the same number with
        // the reason attached. Saying it twice reads as two facts rather than one.
        if (maxPriceAge != DefaultPriceAgeHours && !refused)
        {
            turned.Add($"prices up to {Span(maxPriceAge)} old");
        }

        if (query.IncludeCarriers)
        {
            turned.Add("fleet carriers included");
        }

        var said = turned.Count > 0 ? $" Searched {string.Join(", ", turned)}." : string.Empty;

        if (refused)
        {
            said +=
                $" You asked for prices up to {Span(askedPriceAge!.Value)} old; max_price_age_hours "
                + $"stops at {MaxPriceAgeHours:N0} hours — {Span(MaxPriceAgeHours)} — so that is what "
                + "I searched.";
        }

        return said + LimitRefused(askedLimit);
    }

    /// <summary>A stretch of hours in the words a Commander would use for it.</summary>
    private static string Span(int hours) => hours switch
    {
        >= 8_760 and var y when y % 8_760 == 0 => y / 8_760 == 1 ? "a year" : $"{y / 8_760} years",
        >= 48 and var d when d % 24 == 0 => $"{d / 24} days",
        1 => "an hour",
        _ => $"{hours:N0} hours",
    };

    /// <summary>
    /// How many results to return, and what the model asked for if it asked - the pair the
    /// answer needs to tell an honoured ask from a refused one
    /// (<a href="https://github.com/dseelinger/d47/issues/178">#178</a>).
    /// <para>
    /// Read here rather than in <see cref="StationQuery.TryParse"/> because both halves of
    /// <c>find_nearest_station</c> take the same argument and owe the same sentence, and the
    /// parser is also called by callers that pass a size of their own choosing (Phase 47) and
    /// have nobody to say it to. The clamp there stays: it is the floor under this, not a
    /// duplicate of it.
    /// </para>
    /// </summary>
    private static int Limit(ToolArguments arguments, out int? asked)
    {
        asked = arguments.TryGetInt32("limit", out var wanted) ? wanted : null;

        return Math.Clamp(asked ?? DefaultLimit, MinLimit, MaxLimit);
    }

    /// <summary>
    /// The sentence a refused <c>limit</c> owes, or nothing at all where the ask was honoured.
    /// <para>
    /// The knob and its bound, in the shape <see cref="AsAsked"/> uses for
    /// <c>max_price_age_hours</c>: an out-of-range value is not a value d47 gets to swap for one
    /// it prefers without saying so.
    /// </para>
    /// </summary>
    private static string LimitRefused(int? asked) => asked switch
    {
        > MaxLimit => $" You asked for {asked}; limit stops at {MaxLimit}, so that is what I looked for.",
        < MinLimit => $" You asked for {asked}; limit starts at {MinLimit}, so that is what I looked for.",
        _ => string.Empty,
    };

    /// <summary>
    /// How old one quote is, in the words a Commander would use. A market they stood in themselves
    /// is named as theirs, because it is the one figure with no caveat on it.
    /// </summary>
    private static string Age(CommodityOffer offer)
    {
        if (offer.Market.UpdatedAt is not { } when)
        {
            return offer.IsTheirs ? "your own reading, undated" : "undated";
        }

        var whose = offer.IsTheirs ? "you saw it" : "reported";
        var old = DateTimeOffset.UtcNow - when;

        var howLong = old switch
        {
            { TotalHours: < 1 } => "within the hour",
            { TotalHours: < 24 } => $"{old.TotalHours:0} hours ago",
            { TotalDays: < 14 } => $"{old.TotalDays:0} days ago",
            _ => $"{old.TotalDays / 7:0} weeks ago",
        };

        return $"{whose} {howLong}";
    }

    private static async Task<ToolResult> FindBodyAsync(
        IGalaxyService? galaxy,
        Func<string?> currentSystem,
        Configuration.SettingsService settings,
        ToolArguments arguments,
        CancellationToken cancellationToken)
    {
        if (galaxy is null || !settings.Current.Knowledge.GalaxySearch)
        {
            return ToolResult.Error(Unavailable);
        }

        var near = arguments.TryGetString("near", out var explicitNear) && !string.IsNullOrWhiteSpace(explicitNear)
            ? explicitNear
            : currentSystem();

        if (string.IsNullOrWhiteSpace(near))
        {
            return ToolResult.Error(
                "I don't know where the Commander is right now, so I need a system to search out from.");
        }

        arguments.TryGetString("body_type", out var bodyType);
        arguments.TryGetString("signal", out var signal);
        arguments.TryGetString("hotspot", out var hotspot);
        arguments.TryGetString("ring_type", out var ringType);
        arguments.TryGetString("reserve_level", out var reserveLevel);

        if (!BodyQuery.TryParse(
                near,
                bodyType,
                signal,
                Count(arguments, "signal_count"),
                hotspot,
                Count(arguments, "hotspot_count"),
                ringType,
                reserveLevel,
                Flag(arguments, "landable"),
                Flag(arguments, "terraformable"),
                Distance(arguments, "max_distance"),
                arguments.TryGetInt32("limit", out var limit) ? limit : 5,
                out var query,
                out var failure))
        {
            return ToolResult.Error(failure);
        }

        try
        {
            var result = await galaxy.FindBodiesAsync(query, cancellationToken).ConfigureAwait(false);

            return ToolResult.Ok(Describe(result, query));
        }
        catch (GalaxyUnavailableException ex)
        {
            return ToolResult.Error(ex.Message);
        }
    }

    /// <summary>
    /// A boolean argument as three states rather than two. Absent has to stay distinguishable
    /// from false: "landable" left out means either, and false means specifically the ones that
    /// cannot be landed on, which is a search nobody meant to make by omission.
    /// </summary>
    private static bool? Flag(ToolArguments arguments, string name) =>
        arguments.Values.ContainsKey(name) && arguments.TryGetBoolean(name, out var value) ? value : null;

    private static int? Count(ToolArguments arguments, string name) =>
        arguments.TryGetInt32(name, out var value) ? value : null;

    private static double? Distance(ToolArguments arguments, string name) =>
        arguments.Values.TryGetValue(name, out var raw)
        && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// Body results as prose, and it reads differently depending on what was asked.
    /// <para>
    /// A ring question gets the hotspots and how old the report is; a surface question gets the
    /// signals. Printing both every time would bury the answer under the other question's
    /// evidence — a mining search returning eight lines of biological signal counts is worse than
    /// no answer, because the Commander has to read it to find that out.
    /// </para>
    /// </summary>
    private static string Describe(BodySearchResult result, BodyQuery query)
    {
        if (result.Bodies.Count == 0)
        {
            return $"Nothing within {query.MaxDistance:N0} light years matches that.";
        }

        var report = new StringBuilder();

        report.Append(result.Total == result.Bodies.Count
            ? $"{result.Total} bod{(result.Total == 1 ? "y" : "ies")} matched"
            : $"{result.Total} bodies matched; here are the nearest {result.Bodies.Count}");

        if (result.Reference is not null)
        {
            report.Append($", measured from {result.Reference}");
        }

        report.AppendLine(".");

        foreach (var body in result.Bodies)
        {
            report.AppendLine();
            report.Append($"{body.Name} in {body.SystemName}");

            if (body.Distance is not null)
            {
                report.Append($" — {body.Distance.Value.ToString("N2", CultureInfo.InvariantCulture)} ly");
            }

            if (body.DistanceToArrival is not null)
            {
                report.Append(
                    $", {body.DistanceToArrival.Value.ToString("N0", CultureInfo.InvariantCulture)} ls from arrival");
            }

            var facts = new List<string>();

            if (body.Subtype is not null)
            {
                facts.Add(body.Subtype);
            }

            if (body.IsLandable)
            {
                facts.Add("landable");
            }

            if (body.TerraformingState is not null and not "Not terraformable")
            {
                facts.Add(body.TerraformingState.ToLowerInvariant());
            }

            if (query.IsAboutRings && body.ReserveLevel is not null)
            {
                facts.Add($"{body.ReserveLevel.ToLowerInvariant()} reserves");
            }

            if (facts.Count > 0)
            {
                report.Append($"; {string.Join(", ", facts)}");
            }

            if (query.IsAboutRings)
            {
                DescribeRings(report, body, query.RingSignal);
            }
            else if (body.Signals.Count > 0)
            {
                report.Append("; " + string.Join(
                    ", ",
                    body.Signals.Select(signal => $"{signal.Count} {signal.Kind.ToLowerInvariant()}")));
            }
        }

        return report.ToString().TrimEnd();
    }

    private static void DescribeRings(StringBuilder report, BodySummary body, string? wanted)
    {
        foreach (var ring in body.Rings)
        {
            // Only the rings that carry what was asked for. A metal-rich ring with no Painite in
            // it is not part of the answer to "where is Painite", and listing it invites the
            // Commander to fly to the wrong one of two rings around the same planet.
            var hotspots = wanted is null
                ? ring.Hotspots
                : [.. ring.Hotspots.Where(hotspot =>
                    string.Equals(hotspot.Material, wanted, StringComparison.OrdinalIgnoreCase))];

            if (hotspots.Count == 0)
            {
                continue;
            }

            report.AppendLine();
            report.Append($"  {ring.Name}");

            if (ring.Type is not null)
            {
                report.Append($" ({ring.Type})");
            }

            report.Append(": " + string.Join(
                ", ",
                hotspots.Select(hotspot => $"{hotspot.Count} {hotspot.Material}")));

            // Hotspots are crowd-reported like outfitting stock, and a report nobody has refreshed
            // since the last balance pass is a claim about a ring rather than a fact about one.
            if (ring.SignalsSeen is { } seen)
            {
                report.Append($", reported {seen:yyyy-MM-dd}");
            }
        }
    }

    /// <summary>
    /// Station results as prose, with the age of the data said out loud.
    /// <para>
    /// Outfitting stock is crowd-reported: it is not what is there, it is what somebody last
    /// reported was there. Reading a three-year-old report as current is how a Commander flies
    /// two hundred light years for a module that is not on the shelf, so the report's age is part
    /// of the answer rather than a footnote.
    /// </para>
    /// </summary>
    private static string Describe(StationSearchResult result, StationQuery query)
    {
        var wanted = query.Module is not null
            ? Describe(query)
            : query.Ship ?? "that";

        if (result.Stations.Count == 0)
        {
            return $"Nowhere within {query.MaxDistance:N0} light years is reported to sell {wanted}.";
        }

        var report = new StringBuilder();

        report.AppendLine(result.Total == result.Stations.Count
            ? $"{result.Total} station{(result.Total == 1 ? "" : "s")} sell {wanted}."
            : $"{result.Total} stations sell {wanted}; here are the nearest {result.Stations.Count}.");

        foreach (var station in result.Stations)
        {
            report.AppendLine();
            report.Append($"{station.Name} in {station.SystemName}");

            if (station.Distance is not null)
            {
                report.Append($" — {station.Distance.Value.ToString("N2", CultureInfo.InvariantCulture)} ly");
            }

            if (station.DistanceToArrival is not null)
            {
                report.Append(
                    $", {station.DistanceToArrival.Value.ToString("N0", CultureInfo.InvariantCulture)} ls from arrival");
            }

            if (station.Type is not null)
            {
                report.Append($"; {station.Type}");
            }

            if (station.HasLargePad)
            {
                report.Append("; large pad");
            }

            if (station.StockLastSeen is not null)
            {
                report.Append($"; stock last reported {station.StockLastSeen.Value:yyyy-MM-dd}");
            }
        }

        return report.ToString().TrimEnd();
    }

    private static string Describe(StationQuery query)
    {
        var size = query.ModuleClass is not null || query.ModuleRating is not null
            ? $"{query.ModuleClass}{query.ModuleRating} "
            : string.Empty;

        return $"a {size}{query.Module}".Replace("  ", " ", StringComparison.Ordinal);
    }

    private const string Unavailable =
        "Galaxy search is switched off, so I can't look that up. The Commander can turn it on in settings.";

    /// <summary>
    /// The result as prose. Written for a model that is about to speak it, so it is short lines
    /// rather than a table, and it says plainly how many were left out.
    /// </summary>
    private static string Describe(GalaxySearchResult result)
    {
        if (result.Systems.Count == 0)
        {
            return "Nothing matched that search.";
        }

        var report = new StringBuilder();

        report.Append(result.Total == result.Systems.Count
            ? $"{result.Total} system{(result.Total == 1 ? "" : "s")} matched"
            : $"{result.Total} systems matched; here are the nearest {result.Systems.Count}");

        if (result.Reference is not null)
        {
            report.Append($", measured from {result.Reference}");
        }

        report.AppendLine(".");

        foreach (var system in result.Systems)
        {
            report.AppendLine();
            report.Append(system.Name);

            if (system.Distance is not null)
            {
                report.Append($" — {system.Distance.Value.ToString("N2", CultureInfo.InvariantCulture)} ly");
            }

            var facts = new List<string>();

            if (system.Allegiance is not null)
            {
                facts.Add(system.Allegiance);
            }

            if (system.Government is not null)
            {
                facts.Add(system.Government);
            }

            if (system.PrimaryEconomy is not null)
            {
                facts.Add(system.PrimaryEconomy);
            }

            if (system.Security is not null)
            {
                facts.Add($"{system.Security} security");
            }

            if (system.Population is > 0)
            {
                facts.Add($"population {system.Population.Value.ToString("N0", CultureInfo.InvariantCulture)}");
            }

            if (system.StationCount is > 0)
            {
                facts.Add($"{system.StationCount} station{(system.StationCount == 1 ? "" : "s")}");
            }

            if (system.NeedsPermit)
            {
                facts.Add("permit required");
            }

            if (facts.Count > 0)
            {
                report.Append($"; {string.Join(", ", facts)}");
            }
        }

        return report.ToString().TrimEnd();
    }
}
