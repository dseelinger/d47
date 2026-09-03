using System.Globalization;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Finding exobiology worth landing for (Phase 18, "Find the exobiology").
/// <para>
/// <b>Two halves that answer different questions from different sources, and the difference is the
/// design.</b> The plotter tours biology somebody has <em>already found</em> and can therefore name
/// species and quote money. The journal reports what the Commander's own surface scan just turned
/// up, and names only the genus, because that is all the game says until a sample is taken. Letting
/// those two look like one kind of answer would put a figure next to a genus that cannot carry one.
/// </para>
/// <para>
/// <b>What a plotted route structurally cannot be is a first footfall.</b> An index knows what has
/// been scanned and uploaded, so every system on a plot has been visited — and first footfall, which
/// pays five times over (measured, 79 rows, zero variance), happens only where nobody has been. That
/// is not a gap to close; it is why <see cref="SystemNameCapability"/> exists beside this one, and
/// both answers say so rather than leaving a Commander to work it out after the flight.
/// </para>
/// </summary>
public static class ExobiologyCapability
{
    public const string Id = "exobiology";

    /// <param name="routes">
    /// Null where nothing composed one — under the designer, and in a test that is not about it. The
    /// journal half still answers, which is what a capability being partly off looks like rather
    /// than one being absent (Phase 3).
    /// </param>
    /// <param name="status">
    /// The live <c>Status.json</c>, which is the only thing that knows where the Commander is
    /// standing — <c>ScanOrganic</c> carries no position at all. Null under the designer and in a
    /// test that is not about it, and the sampling answer then carries counts without distances.
    /// </param>
    public static CapabilityDescriptor Create(
        IRouteService? routes,
        Func<CommanderGameState?> commander,
        Func<GameStatus>? status = null) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Exobiology",
        Summary =
            "Plot a circuit through known biology, and read back what your own surface scan found on "
            + "the body you are at.",
        Examples =
        [
            "plot me an exobiology route",
            "what biology is on this body",
            "is this body worth landing on",
            "what did the scan find here",
        ],
        // Each names its tool (#161). "exobiology route" asks for a route and used to be
        // answered with what the scanner found on the body underneath, which is the tool
        // declared first rather than the one the words name.
        Keywords =
        [
            new("exobiology route", "plot_exobiology_route"),
            new("biology on this body", "get_body_biology"),
            new("worth landing on", "get_body_biology"),
            new("what did the scan find", "get_body_biology"),
        ],
        Tools =
        [
            new ToolDefinition
            {
                Name = "get_body_biology",
                Description =
                    "What the Commander's own detailed surface scan found on a body: how many "
                    + "biological signals, which genera, and what else is down there. This is the "
                    + "game's own answer and outranks any prediction. It names the genus only — the "
                    + "species, which sets the price, is not known until a sample is taken, so this "
                    + "never quotes a value.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "body",
                        Type = ToolParameterType.String,
                        Description =
                            "The body name, or its short form such as \"7 b\". Leave out for the most "
                            + "recently scanned body that has biology on it.",
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(Body(commander(), arguments))),
            },
            new ToolDefinition
            {
                Name = "get_sampling_progress",
                Description =
                    "What the Commander has sampled on the body they are on: which genus, how many "
                    + "specimens of the three, how far they have moved since the last one, and what "
                    + "is already finished here. Does not say whether they have moved far enough — "
                    + "the required distance is in the species' Codex entry in-game, and D47 ships "
                    + "no table of it.",
                Parameters = [],
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(Sampling(commander(), status))),
            },
            new ToolDefinition
            {
                Name = "plot_exobiology_route",
                Description =
                    "Plot a circuit through systems with known, already-surveyed biology, with the "
                    + "species on each body and what each is worth. Everything on it has been visited "
                    + "by somebody, so none of it can be a first footfall — for undiscovered systems, "
                    + "read a system name instead.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "from",
                        Type = ToolParameterType.String,
                        Description = "System to start from. Leave out to start where the Commander is.",
                    },
                    new ToolParameter
                    {
                        Name = "jump_range",
                        Type = ToolParameterType.Number,
                        Description = "Laden jump range in light years. Defaults to 50.",
                    },
                    new ToolParameter
                    {
                        Name = "radius",
                        Type = ToolParameterType.Number,
                        Description = "How far from the origin to look, in light years. Defaults to 200.",
                    },
                    new ToolParameter
                    {
                        Name = "max_results",
                        Type = ToolParameterType.Integer,
                        Description = "How many systems to visit. Defaults to 10.",
                    },
                    new ToolParameter
                    {
                        Name = "min_value",
                        Type = ToolParameterType.Integer,
                        Description =
                            "The least a body's biology must be worth to be a stop, in credits. "
                            + "Defaults to 1,000,000.",
                    },
                    new ToolParameter
                    {
                        Name = "loop",
                        Type = ToolParameterType.Boolean,
                        Description = "Whether the circuit returns to the start. Defaults to true.",
                    },
                ],
                Handler = (arguments, cancellationToken) =>
                    PlotAsync(routes, commander, arguments, cancellationToken),
            },
        ],
        Display = new CapabilityDisplay { PanelTitle = "Exobiology", Order = 58 },
    };

    // --------------------------------------------------------------- the body

    private static string Body(CommanderGameState? state, ToolArguments arguments)
    {
        if (state is null)
        {
            return "No Elite Dangerous journal has been detected yet.";
        }

        var wanted = arguments.TryGetString("body", out var given) && !string.IsNullOrWhiteSpace(given)
            ? given.Trim()
            : null;

        var scans = state.Bodies;

        var body = wanted is not null ? scans.Named(wanted) : scans.MostRecentWithBiology ?? scans.All.FirstOrDefault();

        if (body is null)
        {
            if (!scans.IsKnown)
            {
                return
                    "I have no surface scans yet — map a body with the detailed surface scanner and I "
                    + "will know what is on it.";
            }

            return wanted is not null
                ? $"I have no surface scan for \"{wanted}\". I have scanned: "
                    + $"{string.Join("; ", scans.All.Take(8).Select(scan => scan.BodyName))}."
                : "None of the bodies I have scanned had anything on them.";
        }

        var report = new StringBuilder();

        var seenHow = body.Source == BodySignalSource.SurfaceScan ? "surface-scanned" : "FSS-scanned, not mapped,";

        report.AppendLine($"{body.BodyName}, {seenHow} {Stamp(body.SeenAt)}.");

        if (body.Source == BodySignalSource.Fss && body.BiologicalCount > 0)
        {
            // The FSS says how many; it never says which genus. Saying "no biology" here would be
            // wrong, and listing genera would claim knowledge the game has not given up yet.
            report.AppendLine(
                $"  The FSS reported {body.BiologicalCount} biological signal"
                + $"{(body.BiologicalCount == 1 ? "" : "s")}. It has not been mapped, so I do not know "
                + "the genus.");
        }
        else if (!body.HasBiology)
        {
            // A real answer, and the one that saves a landing. The scan ran and found nothing
            // biological, which is different from d47 not having looked.
            report.AppendLine("  No biological signals. Nothing here to sample.");
        }
        else
        {
            report.AppendLine(
                $"  {body.BiologicalCount} biological signal{(body.BiologicalCount == 1 ? "" : "s")}"
                + (body.Genera.Count > 0 ? $": {string.Join(", ", body.Genera)}." : "."));
        }

        var others = body.Signals
            .Where(signal => !signal.Type.Contains("Biological", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (others.Count > 0)
        {
            report.AppendLine(
                "  Also down there: "
                + string.Join(", ", others.Select(signal => $"{signal.Count} {signal.Type}")) + ".");
        }

        if (body.Genera.Count > 0)
        {
            // Said once, on the answer that would otherwise invite the question. The game names the
            // genus and stops; the money is in the species and nothing on disk says which it is.
            report.AppendLine();
            report.AppendLine(
                "Elite names the genus and not the species, and the species is what sets the price — "
                + "so I cannot tell you what this is worth until you sample it.");
        }

        return report.ToString().TrimEnd();
    }

    // ----------------------------------------------------------- the sampling

    private static string Sampling(CommanderGameState? state, Func<GameStatus>? status)
    {
        if (state is null)
        {
            return "No Elite Dangerous journal has been detected yet.";
        }

        if (state.Sampling.MostRecent is not { } body)
        {
            return
                "You have not sampled anything I have seen. I track a run once you take the first "
                + "specimen.";
        }

        var live = status?.Invoke();

        var here = live is { HasPosition: true, PlanetRadius: { } radius }
            ? new SurfaceFix(live.Latitude!.Value, live.Longitude!.Value, radius)
            : (SurfaceFix?)null;

        var report = new StringBuilder();

        foreach (var genus in body.InProgress)
        {
            report.AppendLine($"{genus.Species ?? genus.Genus} — {genus.Taken} of {GenusProgress.Required}, "
                + $"{genus.Remaining} to go.");

            // The live half, and the only reason this is a tool rather than only a callout: a
            // Commander driving away from the last specimen wants the number now, not when the
            // next sample lands.
            if (state.Sampling.MetresSinceLast(genus, here) is { } metres)
            {
                report.AppendLine($"  {OrganicSampling.Metres(metres)} from your last specimen.");
            }
            else
            {
                report.AppendLine(
                    "  I have no position for you, so I cannot say how far you have moved — that "
                    + "needs you to be on the surface.");
            }

            // Measured from this Commander's own accepted samples, and quoted as what it is: an
            // upper bound on the requirement, with its sample size, never the requirement itself.
            if (state.Sampling.ClosestAccepted(genus.Genus) is { } closest)
            {
                report.AppendLine(
                    $"  The closest I have seen {genus.Genus} accepted is "
                    + $"{OrganicSampling.Metres(closest.Metres)}, over {closest.Samples} "
                    + $"sample{(closest.Samples == 1 ? "" : "s")}. That is an upper bound on what it "
                    + "needs, not the figure — the Codex entry has that.");
            }
        }

        var done = body.Completed;

        if (done.Count > 0)
        {
            report.AppendLine();
            report.AppendLine(
                $"Finished here: {string.Join(", ", done.Select(genus => genus.Species ?? genus.Genus))}.");
        }

        if (body.InProgress.Count == 0 && done.Count > 0)
        {
            report.AppendLine("Nothing in progress on this body.");
        }

        return report.ToString().TrimEnd();
    }

    // -------------------------------------------------------------- the route

    private static async Task<ToolResult> PlotAsync(
        IRouteService? routes,
        Func<CommanderGameState?> commander,
        ToolArguments arguments,
        CancellationToken cancellationToken)
    {
        if (routes is null)
        {
            return ToolResult.Ok(
                "Route plotting is not available in this build, so I can only tell you what your own "
                + "scans found.");
        }

        var from = arguments.TryGetString("from", out var given) && !string.IsNullOrWhiteSpace(given)
            ? given.Trim()
            : commander()?.Location.StarSystem;

        if (!ExobiologyQuery.TryParse(
                from,

                // The ship's own range where the Commander did not say one, exactly as the other
                // plotters do — a route plotted at a default 50 for a ship that jumps 18 is a route
                // nobody can fly.
                Number(arguments, "jump_range") ?? commander()?.Ship.MaxJumpRange,
                Number(arguments, "radius"),
                arguments.TryGetInt32("max_results", out var max) ? max : null,
                arguments.TryGetInt32("min_value", out var min) ? min : null,
                maxDistanceToArrival: null,
                Flag(arguments, "loop"),
                out var query,
                out var failure))
        {
            return ToolResult.Ok(failure);
        }

        var route = await routes.PlotExobiologyAsync(query, cancellationToken).ConfigureAwait(false);

        if (route is null || route.Stops.Count == 0)
        {
            return ToolResult.Ok(
                $"No exobiology route from {query.From} within {Number((long)query.Radius)} light years "
                + $"at {Number(query.MinimumValue)} credits a body or better. A wider radius or a lower "
                + "minimum would find something.");
        }

        var report = new StringBuilder();

        report.AppendLine(
            $"{route.Stops.Count} system{(route.Stops.Count == 1 ? "" : "s")} from {query.From}, "
            + $"{route.TotalJumps} jumps, {Number(route.TotalValue)} credits of biology in all:");

        foreach (var stop in route.Stops)
        {
            report.AppendLine();
            report.AppendLine(
                $"{stop.System} — {stop.Jumps} jump{(stop.Jumps == 1 ? "" : "s")}, "
                + $"{stop.Bodies.Count} bod{(stop.Bodies.Count == 1 ? "y" : "ies")}, "
                + $"{Number(stop.Value)} cr.");

            foreach (var body in stop.Bodies)
            {
                var where = body.DistanceToArrival is { } distance
                    ? $", {Number((long)distance)} ls out"
                    : string.Empty;

                report.AppendLine($"  {body.Name} ({body.Subtype ?? "unknown type"}{where}) — "
                    + $"{Number(body.LandmarkValue)} cr");

                foreach (var species in body.Species)
                {
                    report.AppendLine(
                        $"    {species.Name}{(species.Count > 1 ? $" ×{species.Count}" : string.Empty)}"
                        + $" — {Number(species.Value)} cr");
                }
            }
        }

        report.AppendLine();
        report.AppendLine(
            "Every system here has already been surveyed by somebody, so none of it is a first "
            + "footfall — that pays five times as much and only happens where nobody has been.");

        return ToolResult.Ok(report.ToString().TrimEnd());
    }

    private static double? Number(ToolArguments arguments, string name) =>
        arguments.Values.TryGetValue(name, out var raw)
        && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static bool? Flag(ToolArguments arguments, string name) =>
        arguments.Values.ContainsKey(name) && arguments.TryGetBoolean(name, out var value) ? value : null;

    private static string Stamp(DateTimeOffset when) =>
        when.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " game time";

    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
