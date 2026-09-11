using System.Globalization;
using System.Text;
using D47.Core.Knowledge;
using D47.Core.Listening;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Looking things up in the galaxy (Phase 14, "Galaxy Search").</summary>
/// <param name="Names">
/// Their own catalogue, read at call time rather than captured — a system they jumped into a minute ago
/// is one they may be about to say.
/// </param>
/// <param name="Watch">
/// Which name is being waited on, and whether they have been asked once already.
/// </param>
/// <param name="Learn">Told about a correction that resolved.</param>
public sealed record SpokenNamesSurface(
    Func<Listening.SpokenNames> Names,
    Listening.MishearingWatch Watch,
    Action<string, string> Learn)
{
    /// <summary>Every member supplied and none of them learning anything, for a test registry to bind.</summary>
    public static SpokenNamesSurface Inert => new(
        () => Listening.SpokenNames.Empty,
        new Listening.MishearingWatch(),
        (_, _) => { });

    /// <summary>A name resolved: learn the correction if one was outstanding.</summary>
    public void Confirm(string resolved)
    {
        if (Watch.Confirmed(resolved) is { } correction)
        {
            Learn(correction.Heard, correction.Meant);
        }
    }
}

public static class GalaxyCapability
{
    /// <summary>The descriptor's id, named once so a help link cannot spell it differently.</summary>
    public const string Id = "galaxy";

    /// <summary>How far the commodity search looks when nobody said (#157).</summary>
    private const double DefaultDistance = 50;

    /// <summary>
    /// How stale a quoted price may be when nobody said, in hours — one month, the bound the trade
    /// planner already uses.
    /// </summary>
    private const int DefaultPriceAgeHours = 720;

    /// <summary>
    /// The oldest price the search will consider, a year, matching <see cref="TradeQuery"/>'s own
    /// bound.
    /// </summary>
    private const int MaxPriceAgeHours = 8_760;

    /// <summary>How many results the station search returns when nobody said.</summary>
    private const int DefaultLimit = 5;

    /// <summary>The most <c>limit</c> will return, and the fewest.</summary>
    private const int MaxLimit = 20;

    /// <summary>The other end of the same bound.</summary>
    private const int MinLimit = 1;

    /// <summary>
    /// <param name="galaxy"> The service, or null where none is composed — under the designer and in a
    /// test that is not about it.
    /// </summary>
    /// <param name="galaxy">
    /// The service, or null where none is composed — under the designer and in a test that is not about
    /// it.
    /// </param>
    /// <param name="trade">
    /// The trade planner, which is where whole markets already live (Phase 49).
    /// </param>
    public static CapabilityDescriptor Create(
        IGalaxyService? galaxy,
        Func<string?> currentSystem,
        Configuration.SettingsService settings,
        ITradePlanService? trade = null,
        Func<string?>? currentStation = null,
        CommodityBoard? board = null,
        Func<DateTimeOffset>? now = null,

        // What this Commander has met, and what their transcriber gets wrong (#134).
        SpokenNamesSurface? heard = null,

        // Where a nearest-first commodity search puts its winner (#325): on the clipboard, the same way
        // plot_course does, and into `lastFound` so "set a course" has something to mean.
        IClipboard? clipboard = null,
        Conversation.LastFoundSystem? lastFound = null) => new()
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

        // No keywords.
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

                        // The range syntax is spelled out in this tool's own description, one line up.
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
                    DistanceAsync(galaxy, currentSystem, settings, heard, arguments, cancellationToken),
            },
            new ToolDefinition
            {
                Name = "find_nearest_station",
                Description =
                    "Find the nearest station selling a named module or ship, or trading a commodity "
                    + "— cargo carried in tonnes, never an engineering material. A rare good answers "
                    + "with its one selling station and the quantity currently on offer there.",
                Parameters =
                [
                    // Three short descriptions on purpose.
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
                    // The commodity half's own two knobs (#157).
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
                    // The four INARA has and this did not (#296).
                    new ToolParameter
                    {
                        Name = "max_station_distance",
                        Type = ToolParameterType.Number,
                        Description = "Furthest from the star, in light seconds.",
                    },
                    new ToolParameter
                    {
                        Name = "min_supply",
                        Type = ToolParameterType.Integer,
                        Description = "Least in stock, or least demand when selling.",
                    },
                    new ToolParameter
                    {
                        Name = "surface_stations",
                        Type = ToolParameterType.Boolean,
                        Description = "Also planetary ports and settlements. Default false.",
                    },
                    new ToolParameter
                    {
                        Name = "order_by",
                        Type = ToolParameterType.String,
                        Description = "Nearest first, or best price first. Default price.",
                        AllowedValues = ["distance", "price"],
                    },
                    // Carried onto the posting so a page reading the shared CommodityBoard can tell its own
                    // baked question apart from any other (#303).
                    new ToolParameter
                    {
                        Name = "tag",
                        Type = ToolParameterType.String,
                        Description = "Internal. Leave unset.",
                    },
                ],
                Handler = (arguments, cancellationToken) =>
                    FindStationAsync(
                        galaxy, trade, currentSystem, currentStation, settings, board, now, clipboard, lastFound,
                        arguments, cancellationToken),
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

                        // Two examples rather than four.
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

    /// <summary>A name that will not resolve now asks rather than shrugging (#134).</summary>
    private static ToolResult CouldNotFind(
        SpokenNamesSurface? heard,
        string from,
        string to,
        string current)
    {
        // Where the Commander is standing was written by Elite, not said by anybody.
        if (!string.Equals(from, current, StringComparison.OrdinalIgnoreCase)
            && heard?.Names() is { IsKnown: true } catalogue
            && !catalogue.Knows(to)
            && catalogue.Knows(from))
        {
            return ToolResult.Error(Ask(heard, from));
        }

        return heard is null
            ? ToolResult.Error($"I couldn't find one of those systems — '{from}' or '{to}'.")
            : ToolResult.Error(Ask(heard, to));

        static string Ask(SpokenNamesSurface heard, string spoken) =>
            MishearingWatch.Ask(
                "system", spoken, heard.Names().Near(spoken), heard.Watch.Rejected(spoken));
    }

    private static async Task<ToolResult> DistanceAsync(
        IGalaxyService? galaxy,
        Func<string?> currentSystem,
        Configuration.SettingsService settings,
        SpokenNamesSurface? heard,
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
                return CouldNotFind(heard, from, to, currentSystem() ?? string.Empty);
            }

            // **The correction is the retry, and this is where it is noticed** (#134).
            heard?.Confirm(to);

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
        IClipboard? clipboard,
        Conversation.LastFoundSystem? lastFound,
        ToolArguments arguments,
        CancellationToken cancellationToken)
    {
        // **A material is not cargo, and d47 holds the table that says so** (<a
        // href=".com/dseelinger/d47/issues/54">#54</a>).
        if (arguments.TryGetString("commodity", out var named)
            && MaterialCatalogue.Find(named) is
                { Ledger: not (MaterialLedger.Cargo or MaterialLedger.RareCargo) } material)
        {
            return ToolResult.Ok(MaterialSeam.NotThisOne(material, MaterialSeam.MarketTool));
        }

        // A rare good is sold at one station and nowhere else, so the radius sweep cannot answer for it
        // (#118). The table names the station and the one market's quote gives the quantity.
        if (arguments.TryGetString("commodity", out var rareNamed)
            && RareCatalogue.Find(rareNamed) is { } rare)
        {
            var answer = await RareAsync(rare, trade, settings, now, cancellationToken).ConfigureAwait(false);

            arguments.TryGetBoolean("selling", out var selling);

            // Where a rare can be sold is a different question, and the index does answer that one.
            if (!selling || galaxy is null || !settings.Current.Knowledge.GalaxySearch)
            {
                return ToolResult.Ok(answer);
            }

            var sold = await FindCommodityAsync(
                    trade, currentSystem, currentStation, board, now, clipboard, lastFound, arguments,
                    rare.Name, cancellationToken)
                .ConfigureAwait(false);

            return ToolResult.Ok($"{answer} {sold.Content}");
        }

        if (galaxy is null || !settings.Current.Knowledge.GalaxySearch)
        {
            return ToolResult.Error(Unavailable);
        }

        // The commodity half is a different source and a different ranking, so it forks here rather than
        // threading a second kind of answer through the module search (Phase 49).
        if (arguments.TryGetString("commodity", out var commodity) && !string.IsNullOrWhiteSpace(commodity))
        {
            return await FindCommodityAsync(
                trade, currentSystem, currentStation, board, now, clipboard, lastFound, arguments, commodity,
                cancellationToken)
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

    /// <summary>Where to buy a commodity, or where to dump one (Phase 49).</summary>
    private static async Task<ToolResult> FindCommodityAsync(
        ITradePlanService? trade,
        Func<string?> currentSystem,
        Func<string?>? currentStation,
        CommodityBoard? board,
        Func<DateTimeOffset>? now,
        IClipboard? clipboard,
        Conversation.LastFoundSystem? lastFound,
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

        // No upper clamp (#157).
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

        // The four knobs INARA's search has and this one lacked (#296).
        double? maxStationDistance = arguments.TryGetDouble("max_station_distance", out var lightSeconds)
                                     && lightSeconds > 0
            ? lightSeconds
            : null;

        int? minAvailable = arguments.TryGetInt32("min_supply", out var least) && least > 0 ? least : null;

        arguments.TryGetBoolean("surface_stations", out var surfaceStations);

        var orderBy = arguments.TryGetString("order_by", out var order)
                      && string.Equals(order, "distance", StringComparison.OrdinalIgnoreCase)
            ? CommodityOrder.Distance
            : CommodityOrder.Price;

        // Carried straight through to the posting (#303): a caller who baked this question — the Community
        // Goal search is the one that does — can tag it, so a page reading the shared board back knows this
        // answer is its own kind rather than whichever search ran last.
        arguments.TryGetString("tag", out var tag);

        var query = new CommodityQuery(
            commodity.Trim(),
            selling ? TradeSide.Selling : TradeSide.Buying,
            tonnes,
            maxDistance,
            largePad,
            includeCarriers,
            limit,
            maxStationDistance,
            minAvailable,
            surfaceStations,
            orderBy,
            tag);

        try
        {
            var answer = await trade
                .FindCommodityAsync(
                    new CommoditySearch(near, currentStation?.Invoke(), query, maxPriceAge), cancellationToken)
                .ConfigureAwait(false);

            // Posted on the way out, so the Routing tab draws the answer the Commander was just told rather
            // than running a second search that could disagree with it (Phase 49; the arrangement
            // RoutePlanBook already makes for routes).
            if (board is not null)
            {
                board.Post(new CommodityPosting(
                    query, answer, near, now?.Invoke() ?? DateTimeOffset.UtcNow));

                board.Announce();
            }

            var said = DescribeCommodity(query, answer, near, maxPriceAge, askedAge, askedLimit);

            // Nearest first means the nearest is the answer, and the answer is a place to go (#325) — so the
            // winning system goes on the clipboard unconditionally, the same way plot_course puts one there,
            // and is remembered for "set a course" and "set a course and take us out" to act on without the
            // Commander repeating it.
            if (query.OrderBy == CommodityOrder.Distance)
            {
                var winner = answer.Offers.Count > 0 ? answer.Offers[0].Market.System : null;

                lastFound?.Remember(winner);

                if (winner is not null && clipboard is not null
                    && await clipboard.SetTextAsync(winner, cancellationToken).ConfigureAwait(false))
                {
                    said += " System is on your clipboard.";
                }
            }

            return ToolResult.Ok(said);
        }
        catch (GalaxyUnavailableException ex)
        {
            return ToolResult.Error(ex.Message);
        }
    }

    /// <summary>The answer, and the date on every price in it.</summary>
    /// <param name="query">What was asked, knobs and all.</param>
    /// <param name="answer">What came back.</param>
    /// <param name="near">The reference system.</param>
    /// <param name="maxPriceAge">The staleness bound actually used, in hours.</param>
    /// <param name="askedPriceAge">
    /// The staleness bound the model asked for, if it asked.
    /// </param>
    /// <param name="askedLimit">How many results the model asked for, if it asked.</param>
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
            // The radius answered is the radius searched (#156).
            var nothing = answer.Horizon is null && answer.Complete
                ? $"Nothing within {query.MaxDistance:0} light years of {near} is {verb} {query.Commodity}"
                : answer.Considered == 0
                    ? $"Nothing the market index sent me is {verb} {query.Commodity}"
                    : $"Nothing in the {answer.Considered} markets I could check is {verb} {query.Commodity}";

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
            else if (!answer.Complete)
            {
                // No distance, because there is none to give (#350).
                nothing +=
                    " I cannot say how far out the index looked, so that is not an answer for the "
                    + "whole radius — there is more I have not seen.";
            }

            if (answer.DroppedAsStale > 0)
            {
                nothing +=
                    $" {answer.DroppedAsStale} of the {answer.Considered} markets nearby were left out for "
                    + "quoting prices too old to trust rather than for having none.";
            }

            return nothing + AsAsked(query, maxPriceAge, askedPriceAge, askedLimit);
        }

        if (query.OrderBy == CommodityOrder.Distance)
        {
            // Nearest first means the nearest is the answer (#296), and the ear now gets just enough of it to
            // go: what was searched for, the winning station, its system, and the distance to it (#325).
            var winner = answer.Offers[0];

            // Say where it measured from (#331).
            var nearest = CommunityGoalSearch.Whose(query.Tag) is { } whose
                ? $"Nearest to {near}, {whose}, for {verb} {query.Commodity}: "
                  + $"{winner.Market.Station} ({winner.Market.System})"
                : $"Nearest for {verb} {query.Commodity}, {winner.Market.Station} ({winner.Market.System})";

            if (answer.OriginKnown)
            {
                nearest += $", {winner.Distance:0.#} ly";
            }

            nearest += ".";

            if (!answer.OriginKnown)
            {
                nearest +=
                    " I could not place " + near + " from the markets I have, so this is ranked on price "
                    + "alone and the distance is unknown.";
            }

            // "Nearest" is the one claim a cut list cannot support (#350).
            if (!answer.Complete)
            {
                nearest +=
                    " The index did not send me everything inside that radius, so there may be a "
                    + "closer one it did not.";
            }

            return nearest;
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

            // The other half of "how far" (#296), said only when the index knows it: a pad 60,000 light
            // seconds out is the trip the Commander feels after the jump.
            if (offer.Market.DistanceToArrival is { } arrival)
            {
                line += $", {arrival:N0} Ls from the star";
            }

            line += $" — {Age(offer)}";

            lines.Add(line);
        }

        // The same rule on the way out (#156).
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

        // The heading above names the radius that was asked for, and a cut reply does not cover it (#350).
        if (!answer.Complete)
        {
            report +=
                $" That is the {answer.Considered} markets the index sent me rather than "
                + $"everything within {query.MaxDistance:0} light years, so there may be nearer stock "
                + "it did not send.";
        }

        report += AsAsked(query, maxPriceAge, askedPriceAge, askedLimit);

        report += " Prices are reported by other Commanders and can be out of date; supply moves fastest.";

        return report;
    }

    /// <summary>
    /// The knobs the Commander turned, said back to them, and the one that could not be turned as far
    /// as they asked (#157).
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

        // Same rule as the radius above: a search that returned twelve and one that returned five read
        // identically otherwise (#178).
        if (query.Limit != DefaultLimit && LimitRefused(askedLimit).Length == 0)
        {
            turned.Add($"up to {query.Limit} results");
        }

        // Left out when the ask was refused, because the refusal below says the same number with the reason
        // attached.
        if (maxPriceAge != DefaultPriceAgeHours && !refused)
        {
            turned.Add($"prices up to {Span(maxPriceAge)} old");
        }

        if (query.IncludeCarriers)
        {
            turned.Add("fleet carriers included");
        }

        // The four from #296, under the same rule: said only when turned.
        if (query.MaxStationDistance is { } lightSeconds)
        {
            turned.Add($"pads within {lightSeconds:N0} Ls of the star");
        }

        if (query.MinAvailable is { } floor)
        {
            turned.Add(query.Side == TradeSide.Buying
                ? $"at least {floor:N0} in stock"
                : $"demand of at least {floor:N0}");
        }

        if (query.SurfaceStations)
        {
            turned.Add("surface stations included");
        }

        if (query.OrderBy == CommodityOrder.Distance)
        {
            turned.Add("nearest first");
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
    /// How many results to return, and what the model asked for if it asked - the pair the answer needs
    /// to tell an honoured ask from a refused one (#178).
    /// </summary>
    private static int Limit(ToolArguments arguments, out int? asked)
    {
        asked = arguments.TryGetInt32("limit", out var wanted) ? wanted : null;

        return Math.Clamp(asked ?? DefaultLimit, MinLimit, MaxLimit);
    }

    /// <summary>The sentence a refused <c>limit</c> requires, or nothing at all where the ask was honoured.</summary>
    private static string LimitRefused(int? asked) => asked switch
    {
        > MaxLimit => $" You asked for {asked}; limit stops at {MaxLimit}, so that is what I looked for.",
        < MinLimit => $" You asked for {asked}; limit starts at {MinLimit}, so that is what I looked for.",
        _ => string.Empty,
    };

    /// <summary>How old one quote is, in the words a Commander would use.</summary>
    private static string Age(CommodityOffer offer)
    {
        if (offer.Market.UpdatedAt is not { } when)
        {
            return offer.IsTheirs ? "your own reading, undated" : "undated";
        }

        var whose = offer.IsTheirs ? "you saw it" : "reported";

        return $"{whose} {Since(DateTimeOffset.UtcNow - when)}";
    }

    /// <summary>How long ago a report was taken, in the words a Commander would use.</summary>
    private static string Since(TimeSpan old) => old switch
    {
        { TotalHours: < 1 } => "within the hour",
        { TotalHours: < 24 } => $"{old.TotalHours:0} hours ago",
        { TotalDays: < 14 } => $"{old.TotalDays:0} days ago",
        _ => $"{old.TotalDays / 7:0} weeks ago",
    };

    /// <summary>
    /// A rare good's one station, and how much that station last reported on offer (#118). The quantity
    /// on offer is the ceiling per visit; it moves with the system's economic state, so no table holds
    /// it and the station and system are still answered where the index cannot be read.
    /// </summary>
    private static async Task<string> RareAsync(
        RareEntry rare,
        ITradePlanService? trade,
        Configuration.SettingsService settings,
        Func<DateTimeOffset>? now,
        CancellationToken cancellationToken)
    {
        var where = $"{rare.Name} is a rare good, sold at {rare.Station} in {rare.System} and nowhere else.";

        var quote = trade is not null && settings.Current.Knowledge.GalaxySearch
            ? await QuoteOrNothing(trade, rare, cancellationToken).ConfigureAwait(false)
            : null;

        if (quote is null)
        {
            return where
                   + " The amount on offer per visit is set by the station and I have no recent report of"
                   + " it. That amount, whatever it is on the day, is the ceiling on what can be bought in"
                   + " one visit.";
        }

        var reported = quote.UpdatedAt is { } when
            ? Since((now?.Invoke() ?? DateTimeOffset.UtcNow) - when)
            : "undated";

        return where
               + $" Last reported stock {quote.Stock.ToString("N0", CultureInfo.InvariantCulture)},"
               + $" {reported}. How much is on offer per visit is set by the station's economic state and"
               + " can be far higher in a boom; that amount is the ceiling on what can be bought in one"
               + " visit.";
    }

    /// <summary>Null wherever the index cannot be read, since the station and system still can be.</summary>
    private static async Task<StationQuote?> QuoteOrNothing(
        ITradePlanService trade,
        RareEntry rare,
        CancellationToken cancellationToken)
    {
        try
        {
            return await trade.QuoteAsync(rare.MarketId, rare.Symbol, cancellationToken).ConfigureAwait(false);
        }
        catch (GalaxyUnavailableException)
        {
            return null;
        }
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

    /// <summary>A boolean argument as three states rather than two.</summary>
    private static bool? Flag(ToolArguments arguments, string name) =>
        arguments.Values.ContainsKey(name) && arguments.TryGetBoolean(name, out var value) ? value : null;

    private static int? Count(ToolArguments arguments, string name) =>
        arguments.TryGetInt32(name, out var value) ? value : null;

    private static double? Distance(ToolArguments arguments, string name) =>
        arguments.Values.TryGetValue(name, out var raw)
        && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    /// <summary>Body results as prose, and it reads differently depending on what was asked.</summary>
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
            // Only the rings that carry what was asked for.
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

            // Hotspots are crowd-reported like outfitting stock, and a report nobody has refreshed since the
            // last balance pass is a claim about a ring rather than a fact about one.
            if (ring.SignalsSeen is { } seen)
            {
                report.Append($", reported {seen:yyyy-MM-dd}");
            }
        }
    }

    /// <summary>Station results as prose, with the age of the data said out loud.</summary>
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

    /// <summary>The result as prose.</summary>
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
