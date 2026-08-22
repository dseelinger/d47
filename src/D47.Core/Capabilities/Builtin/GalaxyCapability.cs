using System.Globalization;
using System.Text;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Looking things up in the galaxy (list.md Phase 14, "Galaxy Search").
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
    /// <param name="galaxy">
    /// The service, or null where none is composed — under the designer and in a test that is not
    /// about it. Null and switched off give the same answer for the same reason: a capability
    /// that cannot act says so and the turn carries on (list.md Phase 3).
    /// </param>
    public static CapabilityDescriptor Create(
        IGalaxyService? galaxy,
        Func<string?> currentSystem,
        Configuration.SettingsService settings) => new()
    {
        Id = "galaxy",
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
                        Description =
                            "Measure from this system. Defaults to the Commander's own.",
                    },
                    new ToolParameter
                    {
                        Name = "distance",
                        Type = ToolParameterType.String,
                        Description = "How far to look, in light years. For example \"30\" or \"10-50\".",
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
                        Description = "The system to measure from. Defaults to the Commander's current system.",
                    },
                ],
                Handler = (arguments, cancellationToken) =>
                    DistanceAsync(galaxy, currentSystem, settings, arguments, cancellationToken),
            },
            new ToolDefinition
            {
                Name = "find_nearest_station",
                Description =
                    "Find the nearest station selling a named module or ship.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "module",
                        Type = ToolParameterType.String,
                        Description =
                            "A module to be sold there, by name — for example \"Frame Shift Drive\" or "
                            + "\"Bi-Weave Shield Generator\".",
                    },
                    new ToolParameter
                    {
                        Name = "ship",
                        Type = ToolParameterType.String,
                        Description = "A ship to be sold there, by name — for example \"Krait MkII\".",
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
                        Description = "Search out from this system. Defaults to the Commander's own.",
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
                    new ToolParameter
                    {
                        Name = "limit",
                        Type = ToolParameterType.Integer,
                        Description = "How many to return, 1 to 20. Default 5.",
                    },
                ],
                Handler = (arguments, cancellationToken) =>
                    FindStationAsync(galaxy, currentSystem, settings, arguments, cancellationToken),
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
                        Description =
                            "The kind of body, by name — for example \"Earth-like world\", \"Neutron Star\", "
                            + "\"Water world\" or \"Class I gas giant\".",
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
                            "Exactly how many of that signal — not a minimum. Leave it out unless the "
                            + "Commander asked for a specific number.",
                    },
                    new ToolParameter
                    {
                        Name = "hotspot",
                        Type = ToolParameterType.String,
                        Description =
                            "A mining hotspot material in one of the body's rings — for example \"Painite\", "
                            + "\"Low Temperature Diamonds\" or \"Void Opal\".",
                    },
                    new ToolParameter
                    {
                        Name = "hotspot_count",
                        Type = ToolParameterType.Integer,
                        Description =
                            "Exactly how many overlapping hotspots of that material — not a minimum. "
                            + "A double or triple hotspot is 2 or 3.",
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
                        Description = "Search out from this system. Defaults to the Commander's own.",
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
                    + "question is relative to you, leave this machine. Off by default; see Privacy for "
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
                    + "where you are never leaves this machine. Off by default; see Privacy.",
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

    /// <summary>The catalogue of notable places a generated adventure may draw on (list.md Phase 47).</summary>
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
        Func<string?> currentSystem,
        Configuration.SettingsService settings,
        ToolArguments arguments,
        CancellationToken cancellationToken)
    {
        if (galaxy is null || !settings.Current.Knowledge.GalaxySearch)
        {
            return ToolResult.Error(Unavailable);
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

        if (!arguments.TryGetInt32("limit", out var limit))
        {
            limit = 5;
        }

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

            return ToolResult.Ok(Describe(result, query));
        }
        catch (GalaxyUnavailableException ex)
        {
            return ToolResult.Error(ex.Message);
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
