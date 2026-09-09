using System.Globalization;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Engineering: what a blueprint costs and does, and how a fitted module's roll actually went (Phase
/// 14, <c>#102</c> "Know what engineering actually does").
/// </summary>
public static class EngineeringCapability
{
    public const string Id = "engineering";

    /// <summary><param name="galaxy"> The search service, or null where none is composed.</summary>
    /// <param name="galaxy">The search service, or null where none is composed.</param>
    /// <param name="clipboard">What d47 last offered to copy (asked for 2026-08-21).</param>
    public static CapabilityDescriptor Create(
        Func<CommanderGameState?> commander,
        IGalaxyService? galaxy = null,
        Conversation.ClipboardOffer? clipboard = null) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Engineering",
        Summary = "What a blueprint costs and changes, and how the craft on a fitted module went.",
        Examples =
        [
            "what does increased FSD range cost",
            "how good is my frame shift drive roll",
            "how good is my frame shift drive craft",
            "what can I engineer on a power plant",
        ],
        // Each names its tool (#161).
        Keywords =
        [
            new("my engineering", "get_module_engineering"),

            // A phrase rather than the bare word. "blueprint" turns up in sentences that are not about
            // engineering at all, and a single-word keyword hijacks every one of them.
            new("blueprint cost", "get_blueprint"),
            new("how good is my roll", "get_module_engineering"),
            new("how good is my craft", "get_module_engineering"),
        ],
        Tools =
        [
            new ToolDefinition
            {
                Name = "get_blueprint",
                Description =
                    "What a blueprint changes and costs: effects and materials per grade, who offers "
                    + "it and to what grade, the module's experimental effects, and — folding the "
                    + "Commander's rank — the exact total for a full grade. Name a blueprint, or a "
                    + "module to list what it can take.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "blueprint",
                        Type = ToolParameterType.String,
                        Description =
                            "A blueprint by name — for example \"Increased FSD Range\", \"Dirty Drive "
                            + "Tuning\" or \"Lightweight\".",
                    },
                    new ToolParameter
                    {
                        Name = "module",
                        Type = ToolParameterType.String,
                        Description =
                            "A kind of module — for example \"Frame Shift Drive\" or \"Power Plant\". "
                            + "Alone it lists what that module can take; with a blueprint it says which "
                            + "of several modules that blueprint is meant for.",
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(Blueprints(commander, arguments))),
            },
            new ToolDefinition
            {
                Name = "get_module_engineering",
                Description =
                    "How a fitted module's engineering turned out: blueprint, grade, experimental "
                    + "effect, who crafted it, whether the grade is finished, and what it changed in real "
                    + "units. Name a slot or module, or omit for every engineered module aboard.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "module",
                        Type = ToolParameterType.String,
                        Description =
                            "A fitted module or its slot — for example \"frame shift drive\", "
                            + "\"MainEngines\" or \"power plant\". Omit for every engineered module.",
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(Fitted(commander, arguments))),
            },
            new ToolDefinition
            {
                Name = "find_material",
                Description =
                    "Where to get an engineering material: where it is found, and — for a raw one — the "
                    + "nearest landable bodies carrying it, best share first. Also says what the Commander "
                    + "already holds and what a material trader could turn into it.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "material",
                        Type = ToolParameterType.String,
                        Description = "The material, by name — \"Yttrium\", \"Imperial Shielding\".",
                        Required = true,
                    },
                    new ToolParameter
                    {
                        Name = "near",
                        Type = ToolParameterType.String,
                        Description = "Search out from this system. Defaults to the Commander's own.",
                    },
                ],
                Handler = (arguments, cancellationToken) =>
                    FindMaterialAsync(galaxy, commander, clipboard, arguments, cancellationToken),
            },
            new ToolDefinition
            {
                Name = "find_material_trader",
                Description = "Find the nearest material trader, optionally of one kind.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "type",
                        Type = ToolParameterType.String,
                        Description = "Which kind of trader.",
                        AllowedValues = StationQuery.TraderTypes,
                    },
                    new ToolParameter
                    {
                        Name = "near",
                        Type = ToolParameterType.String,
                        Description = "Search out from this system. Defaults to the Commander's own.",
                    },
                ],
                Handler = (arguments, cancellationToken) =>
                    FindTraderAsync(galaxy, commander, clipboard, arguments, cancellationToken),
            },
        ],
        Display = new CapabilityDisplay { PanelTitle = "Engineering", Order = 52 },
    };

    // ---- What a blueprint costs ----------------------------------------------------------------

    private static string Blueprints(Func<CommanderGameState?> commander, ToolArguments arguments)
    {
        arguments.TryGetString("blueprint", out var wanted);
        arguments.TryGetString("module", out var module);

        if (!string.IsNullOrWhiteSpace(wanted))
        {
            var grades = BlueprintCatalogue.Named(wanted, module);

            return grades.Count > 0
                ? Describe(grades, commander())
                : Catalogue.Unknown("blueprint", wanted.Trim(), BlueprintCatalogue.Near(wanted));
        }

        if (string.IsNullOrWhiteSpace(module))
        {
            return "Name a blueprint, or a kind of module to see what it can take.";
        }

        var offered = BlueprintCatalogue.ForModule(module);

        return offered.Count > 0
            ? DescribeModule(offered)
            : Catalogue.Unknown("module", module.Trim(), Catalogue.Near(BlueprintCatalogue.Modules, module));
    }

    /// <summary>Every modification a module kind can take.</summary>
    private static string DescribeModule(IReadOnlyList<Blueprint> offered)
    {
        var report = new StringBuilder();

        var module = offered[0].Module;

        var modifications = offered
            .Where(blueprint => blueprint.Kind == BlueprintKind.Modification)
            .GroupBy(blueprint => blueprint.Name, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();

        report.AppendLine(
            $"{module} takes {modifications.Length} blueprint{(modifications.Length == 1 ? "" : "s")}:");

        foreach (var blueprint in modifications)
        {
            var top = blueprint.Max(grade => grade.Grade ?? 0);

            report.AppendLine($"  {blueprint.Key} — to grade {top}");
        }

        var experimentals = offered
            .Where(blueprint => blueprint.Kind == BlueprintKind.Experimental)
            .Select(blueprint => blueprint.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (experimentals.Length > 0)
        {
            report.AppendLine();
            report.AppendLine($"Experimental effects: {string.Join(", ", experimentals)}.");
        }

        return report.ToString().TrimEnd();
    }

    private static string Describe(IReadOnlyList<Blueprint> grades, CommanderGameState? active)
    {
        var report = new StringBuilder();

        var first = grades[0];
        var top = grades[^1];

        report.AppendLine(Heading(grades));

        if (top.Effects.Count > 0)
        {
            var where = top.Grade is { } grade ? $"At grade {grade}" : "What it does";

            report.AppendLine();
            report.AppendLine($"{where}: {string.Join(", ", top.Effects.Select(Effect))}.");
        }

        // Per grade, because the recipe changes as the grade climbs and a Commander gathering for grade 3
        // needs that row rather than the top one.
        if (grades.Any(blueprint => blueprint.Ingredients.Count > 0))
        {
            report.AppendLine();
            report.AppendLine(
                first.Kind == BlueprintKind.Modification
                    ? "Per application:"
                    : "Cost:");

            foreach (var blueprint in grades)
            {
                var label = blueprint.Grade is { } grade ? $"  Grade {grade}: " : "  ";

                report.AppendLine(label + Ingredients(blueprint.Ingredients));
            }
        }

        var offers = Offers(grades);

        if (offers.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("Offered by: " + string.Join(
                ", ",
                offers.Select(offer => $"{offer.Engineer} to grade {offer.MaxGrade}")) + ".");
        }

        // The half no table can answer.
        if (Standing(top, offers, active) is { Length: > 0 } standing)
        {
            report.AppendLine();
            report.AppendLine(standing);
        }

        if (first.Kind == BlueprintKind.Modification
            && BlueprintCatalogue.ExperimentalsFor(first.Module) is { Count: > 0 } experimentals)
        {
            report.AppendLine();
            report.AppendLine(
                $"Experimental effects for {first.Module}: "
                + string.Join(", ", experimentals.Select(effect => effect.Name).Order(StringComparer.Ordinal))
                + ".");
        }

        return report.ToString().TrimEnd();
    }

    private static string Heading(IReadOnlyList<Blueprint> grades)
    {
        var first = grades[0];

        var ladder = grades
            .Select(blueprint => blueprint.Grade)
            .Where(grade => grade is not null)
            .ToArray();

        var kind = first.Kind switch
        {
            BlueprintKind.Modification => $"{first.Module} blueprint",
            BlueprintKind.Experimental => $"an experimental effect for {first.Module}",
            BlueprintKind.Synthesis => "synthesis",
            BlueprintKind.TechBroker => "a tech broker unlock",
            BlueprintKind.Unlock => "an engineer's invitation",
            BlueprintKind.Suit => "a suit upgrade",
            BlueprintKind.Weapon => "a hand weapon upgrade",
            BlueprintKind.Vendor => "an on-foot vendor's stock",

            // A kind this build does not recognise.
            _ => "a recipe of a kind I do not recognise",
        };

        return ladder.Length > 1
            ? $"{first.Name} — {kind}, grades {ladder[0]} to {ladder[^1]}."
            : $"{first.Name} — {kind}.";
    }

    private static string Effect(BlueprintEffect effect) =>
        effect.IsGood ? $"{effect.Property} {effect.Change}" : $"{effect.Property} {effect.Change} (worse)";

    private static string Ingredients(IReadOnlyList<BlueprintIngredient> ingredients) =>
        ingredients.Count == 0
            ? "nothing"
            : string.Join(", ", ingredients.Select(ingredient =>
                $"{ingredient.Size} × {ingredient.Material?.Name ?? ingredient.Symbol}"));

    private sealed record Offer(string Engineer, int MaxGrade);

    /// <summary>Who offers this blueprint and to what grade, best first.</summary>
    private static IReadOnlyList<Offer> Offers(IReadOnlyList<Blueprint> grades) =>
        [.. grades
            .SelectMany(blueprint => blueprint.Engineers.Select(engineer => (engineer, blueprint.Grade ?? 0)))
            .GroupBy(pair => pair.engineer, StringComparer.Ordinal)
            .Select(group => new Offer(group.Key, group.Max(pair => pair.Item2)))
            .OrderByDescending(offer => offer.MaxGrade)
            .ThenBy(offer => offer.Engineer, StringComparer.Ordinal)];

    /// <summary>
    /// What the top grade costs this Commander, through the best engineer they can actually reach.
    /// </summary>
    private static string Standing(Blueprint top, IReadOnlyList<Offer> offers, CommanderGameState? active)
    {
        if (active?.Engineers is not { IsKnown: true } progress
            || top.Kind != BlueprintKind.Modification
            || top.Grade is not { } grade)
        {
            return string.Empty;
        }

        var reachable = offers
            .Where(offer => offer.MaxGrade >= grade)
            .Select(offer => (offer, Standing: EngineerDirectory.ByName(offer.Engineer) is { } engineer
                ? progress.For(engineer.Id)
                : null))
            .ToArray();

        var best = reachable
            .Where(pair => pair.Standing is { IsUnlocked: true })
            .OrderByDescending(pair => pair.Standing!.Rank ?? 0)
            .FirstOrDefault();

        if (best.Standing is null)
        {
            // Unlocked nobody who grades it.
            var invited = reachable.Where(pair => pair.Standing is { IsInvited: true }).ToArray();

            return invited.Length > 0
                ? $"You have an invitation from {invited[0].offer.Engineer} and have not taken it up; "
                  + $"nobody you have unlocked grades this to {grade}."
                : $"Nobody you have unlocked grades this to {grade}.";
        }

        var rank = best.Standing.Rank ?? 0;
        var engineer = best.offer.Engineer;
        var where = EngineerDirectory.ByName(engineer)?.Where;
        var at = where is null ? engineer : $"{engineer}, at {where}";

        if (EngineeringRules.RollsFor(grade, rank) is not { } rolls)
        {
            // The gate.
            return $"You are grade {rank} with {at}, and grade {grade} needs rank {grade}. "
                   + EngineeringRules.RankRises;
        }

        var total = top.TotalFor(rank);

        return total is null
            ? $"You are grade {rank} with {at}: a full grade {grade} is {rolls} craft{(rolls == 1 ? "" : "s")}."
            : $"You are grade {rank} with {at}: a full grade {grade} is {rolls} craft{(rolls == 1 ? "" : "s")}, "
              + $"so {Ingredients(total)}.";
    }

    // ---- Where to get it ---------------------------------------------------------------------------

    /// <summary>Where a material comes from, and what the Commander could do about it today.</summary>
    private static async Task<ToolResult> FindMaterialAsync(
        IGalaxyService? galaxy,
        Func<CommanderGameState?> commander,
        Conversation.ClipboardOffer? clipboard,
        ToolArguments arguments,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetString("material", out var wanted) || string.IsNullOrWhiteSpace(wanted))
        {
            return ToolResult.Ok("Name a material.");
        }

        if (MaterialCatalogue.Find(wanted) is not { } material)
        {
            return ToolResult.Ok(Catalogue.Unknown("material", wanted.Trim(), MaterialCatalogue.Near(wanted)));
        }

        var active = commander();
        var report = new StringBuilder();

        report.AppendLine(Describe(material));

        if (material.Ledger != MaterialLedger.Material)
        {
            // A commodity or an Odyssey good rather than a ship material.
            return ToolResult.Ok(MaterialSeam.NotThisOne(material, MaterialSeam.MaterialTool));
        }

        report.Append(Held(material, active));

        arguments.TryGetString("near", out var near);
        near ??= active?.Location.StarSystem;

        var found = new Sourced();

        report.Append(await SourceAsync(galaxy, material, near, found, cancellationToken).ConfigureAwait(false));
        report.Append(Netting(material, active));

        // The offer last, under the answer, because it is about what to do next rather than part of what was
        // found.
        if (clipboard?.Offer(found.System, "the system") is { } offer)
        {
            report.AppendLine();
            report.Append(offer);
        }

        return ToolResult.Ok(report.ToString().TrimEnd());
    }

    private static string Describe(MaterialEntry material)
    {
        var what = material.Ledger switch
        {
            MaterialLedger.Material => material.Category is { } category
                ? $"a grade {material.Grade?.ToString() ?? "?"} {category.ToLowerInvariant()} material"
                : "a ship material",
            MaterialLedger.ShipLocker => "an Odyssey ship-locker item, not a ship material",
            MaterialLedger.Cargo => "a market commodity measured in tonnes, not a ship material",
            MaterialLedger.RareCargo => "a rare commodity, not a ship material",
            _ => "something I have no ledger for",
        };

        return $"{material.Name} — {what}.";
    }

    /// <summary>What the Commander has of it now.</summary>
    private static string Held(MaterialEntry material, CommanderGameState? active)
    {
        if (active?.Materials is not { SnapshotSeen: true } inventory)
        {
            return string.Empty;
        }

        var held = inventory.CountOf(material.Symbol);
        var cap = material.Grade is { } grade ? MaterialGrades.CapacityOfGrade(grade) : null;

        return cap is { } capacity
            ? $"You hold {held} of a possible {capacity}." + Environment.NewLine
            : $"You hold {held}." + Environment.NewLine;
    }

    /// <summary>Where to go for it.</summary>
    private sealed class Sourced
    {
        public string? System { get; set; }
    }

    private static async Task<string> SourceAsync(
        IGalaxyService? galaxy,
        MaterialEntry material,
        string? near,
        Sourced found,
        CancellationToken cancellationToken)
    {
        var report = new StringBuilder();

        if (material.Origins.Count > 0)
        {
            report.AppendLine("Found at: " + string.Join("; ", material.Origins) + ".");
        }

        if (galaxy is null)
        {
            return report.ToString();
        }

        var isRaw = string.Equals(material.Category, "Raw", StringComparison.OrdinalIgnoreCase);

        try
        {
            if (isRaw)
            {
                report.Append(await BodiesAsync(galaxy, material, near, found, cancellationToken).ConfigureAwait(false));
            }
            else if (EmissionRules.Holding(material.Symbol) is { } group)
            {
                report.Append(await SystemsAsync(galaxy, group, near, found, cancellationToken).ConfigureAwait(false));
            }
        }
        catch (GalaxyUnavailableException failure)
        {
            report.AppendLine($"I could not reach the galaxy search: {failure.Message}");
        }

        return report.ToString();
    }

    private static async Task<string> BodiesAsync(
        IGalaxyService galaxy,
        MaterialEntry material,
        string? near,
        Sourced found,
        CancellationToken cancellationToken)
    {
        // Three of the twenty-eight raw materials are not in the index at all, so a search for one comes back
        // empty — and empty reads as "there is none near you".
        if (BodyCatalogue.MatchSurfaceMaterial(material.Name) is not { } indexed)
        {
            return $"The body index does not carry {material.Name}, so I cannot search for it — that is a "
                   + "gap in the index rather than a shortage in the galaxy." + Environment.NewLine;
        }

        var result = await galaxy
            .FindBodiesAsync(BodyQuery.ForMaterial(near, indexed, maxDistance: 50, size: 20), cancellationToken)
            .ConfigureAwait(false);

        if (result.Bodies.Count == 0)
        {
            return $"No landable body within 50 light years is recorded as carrying {material.Name}."
                   + Environment.NewLine;
        }

        // The index will not sort on share and will not filter on it, so the ranking is local and the
        // sentence says what it is over. "The best of what I fetched" is true; "the best in the galaxy" would
        // not be.
        var ranked = result.Bodies
            .Select(body => (Body: body, Share: body.Materials
                .Where(entry => string.Equals(entry.Name, indexed, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Share)
                .DefaultIfEmpty(0)
                .Max()))
            .OrderByDescending(pair => pair.Share)
            .Take(5)
            .ToArray();

        var report = new StringBuilder();

        report.AppendLine(
            $"Richest of the {result.Bodies.Count} nearest landable bodies carrying it"
            + (result.Reference is { } reference ? $", from {reference}:" : ":"));

        // The system rather than the body: a body name is not something the galaxy map takes, and pasting a
        // destination into it is the errand this ends.
        found.System = ranked[0].Body.SystemName;

        foreach (var (body, share) in ranked)
        {
            report.Append($"  {body.Name} — {share.ToString("0.0", CultureInfo.InvariantCulture)}%");

            if (body.Distance is { } distance)
            {
                report.Append($", {distance.ToString("0.0", CultureInfo.InvariantCulture)} ly");
            }

            report.AppendLine();
        }

        return report.ToString();
    }

    /// <summary>
    /// Where a high-grade-emission material can be found, from <see cref="EmissionRules"/> — the same
    /// table the callout answers from.
    /// </summary>
    private static async Task<string> SystemsAsync(
        IGalaxyService galaxy,
        EmissionGroup group,
        string? near,
        Sourced found,
        CancellationToken cancellationToken)
    {
        var requested = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["distance"] = "50",
            ["allegiance"] = group.Allegiance,
        };

        // Only where the group is about one.
        if (group.States.Count > 0)
        {
            requested["state"] = string.Join(",", group.States.Select(Spaced));
        }

        // Asked for well beyond the five that get read out, because the population floor is applied
        // afterwards and a page of sparsely populated systems would otherwise leave nothing.
        if (!GalaxyQuery.TryParse(near, requested, size: 50, out var query, out var failure))
        {
            return failure + Environment.NewLine;
        }

        var result = await galaxy.SearchAsync(query, cancellationToken).ConfigureAwait(false);

        var populous = result.Systems
            .Where(system => system.Population is { } people && people >= EmissionRules.MinimumPopulation)
            .Take(5)
            .ToList();

        var described =
            $"{group.Allegiance}-aligned"
            + (group.States.Count > 0 ? $", in {string.Join(" or ", group.States.Select(Spaced))}" : string.Empty)
            + $", over {EmissionRules.MinimumPopulation.ToString("N0", CultureInfo.InvariantCulture)} people";

        if (populous.Count == 0)
        {
            // Said as the filter rather than as "nothing found", so a Commander can tell a genuine shortage
            // from a search that was looking for the wrong thing.
            return $"No system within 50 light years is {described}." + Environment.NewLine;
        }

        var report = new StringBuilder();

        // "Reported" rather than "in".
        report.AppendLine($"Nearest systems reported {described}:");

        found.System = populous[0].Name;

        foreach (var system in populous)
        {
            report.Append($"  {system.Name}");

            if (system.Distance is { } distance)
            {
                report.Append($" — {distance.ToString("0.0", CultureInfo.InvariantCulture)} ly");
            }

            if (system.Population is { } population)
            {
                report.Append($", population {population.ToString("N0", CultureInfo.InvariantCulture)}");
            }

            report.AppendLine();
        }

        return report.ToString();
    }

    /// <summary>What a trader could turn into this, out of what the Commander is actually carrying.</summary>
    private static string Netting(MaterialEntry material, CommanderGameState? active)
    {
        if (active?.Materials is not { SnapshotSeen: true } inventory
            || material.Line is null
            || material.Grade is not { } grade)
        {
            return string.Empty;
        }

        var offers = new List<(string Name, int Held, MaterialExchange Exchange, int Yield)>();

        foreach (var holding in inventory.All)
        {
            if (holding.Count <= 0 || MaterialCatalogue.Find(holding.Name) is not { } from)
            {
                continue;
            }

            // Same ledger, same type, not itself.
            if (from.Symbol == material.Symbol
                || from.Ledger != MaterialLedger.Material
                || from.Line is null
                || from.Grade is not { } fromGrade
                || !string.Equals(from.Category, material.Category, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sameLine = string.Equals(from.Line, material.Line, StringComparison.Ordinal);

            if (EngineeringRules.TradeRate(fromGrade, grade, sameLine) is not { } exchange
                || EngineeringRules.IsBeyondCapacity(fromGrade, grade, sameLine))
            {
                continue;
            }

            var yield = holding.Count / exchange.Paid * exchange.Received;

            if (yield > 0)
            {
                offers.Add((from.Name, holding.Count, exchange, yield));
            }
        }

        if (offers.Count == 0)
        {
            return string.Empty;
        }

        var report = new StringBuilder();

        report.AppendLine(
            "A material trader — the station service, never an engineer and never anybody named "
            + "elsewhere in this turn — could make it out of what you already hold:");

        foreach (var offer in offers.OrderByDescending(offer => offer.Yield).Take(3))
        {
            report.AppendLine(
                $"  {offer.Held} × {offer.Name} at {offer.Exchange.Paid} for {offer.Exchange.Received} "
                + $"— up to {offer.Yield}");
        }

        return report.ToString();
    }

    private static async Task<ToolResult> FindTraderAsync(
        IGalaxyService? galaxy,
        Func<CommanderGameState?> commander,
        Conversation.ClipboardOffer? clipboard,
        ToolArguments arguments,
        CancellationToken cancellationToken)
    {
        if (galaxy is null)
        {
            return ToolResult.Ok("The galaxy search is not switched on, so I cannot look for traders.");
        }

        arguments.TryGetString("type", out var type);
        arguments.TryGetString("near", out var near);
        near ??= commander()?.Location.StarSystem;

        try
        {
            var result = await galaxy
                .FindStationsAsync(StationQuery.ForTrader(near, type, maxDistance: 100, size: 5), cancellationToken)
                .ConfigureAwait(false);

            if (result.Stations.Count == 0)
            {
                return ToolResult.Ok("I found no material trader within 100 light years.");
            }

            var report = new StringBuilder();

            report.AppendLine(
                (type is null
                    ? "Nearest material traders"
                    : $"Nearest traders dealing in {type.ToLowerInvariant()} materials")
                + (result.Reference is { } reference ? $", from {reference}:" : ":"));

            foreach (var station in result.Stations)
            {
                report.Append($"  {station.Name} in {station.SystemName}");

                if (station.Distance is { } distance)
                {
                    report.Append($" — {distance.ToString("0.0", CultureInfo.InvariantCulture)} ly");
                }

                // The index answers the type outright, and null is a real state: one trader in fifty carries
                // the service and no type at all.
                report.Append(station.TraderType is { } kind ? $", {kind.ToLowerInvariant()}" : ", kind unrecorded");

                if (station.DistanceToArrival is { } arrival)
                {
                    report.Append($", {arrival.ToString("N0", CultureInfo.InvariantCulture)} ls in");
                }

                report.AppendLine();
            }

            if (clipboard?.Offer(result.Stations[0].SystemName, "the trader's system") is { } offer)
            {
                report.AppendLine();
                report.Append(offer);
            }

            return ToolResult.Ok(report.ToString().TrimEnd());
        }
        catch (GalaxyUnavailableException failure)
        {
            return ToolResult.Ok($"I could not reach the galaxy search: {failure.Message}");
        }
    }

    // ---- How the roll actually went --------------------------------------------------------------

    private static string Fitted(Func<CommanderGameState?> commander, ToolArguments arguments)
    {
        var active = commander();

        if (active is null)
        {
            return "No Elite Dangerous journal has been detected yet.";
        }

        // The ship being flown rather than the live loadout alone (<a
        // href=".com/dseelinger/d47/issues/337">#337</a>): where no Loadout has been read this session, the
        // one remembered for this ship in ships.json answers.
        var ship = active.FlownShip;

        if (!ship.IsKnown)
        {
            // Loadout is written on entering the game and on every outfitting change, so silence before that
            // is missing evidence rather than a ship with nothing fitted — and with the fallback above, this
            // is now a ship d47 has never seen at all.
            return "I have no loadout yet — it is written when you enter the game.";
        }

        // A remembered loadout is a fact about a moment and not about now (see ShipLoadouts): the Commander
        // may have re-outfitted at a station d47 was not running for.
        var remembered = active.Ship.IsKnown
            ? string.Empty
            : " This is the loadout I last saw on this ship, not one read this session.";

        var engineered = ship.Engineered;

        arguments.TryGetString("module", out var wanted);

        if (string.IsNullOrWhiteSpace(wanted))
        {
            return Summarise(ship, engineered, active.Engineers) + remembered;
        }

        var matches = Matching(ship.Modules, wanted);

        if (matches.Count == 0)
        {
            return $"Nothing fitted matches '{wanted.Trim()}'. "
                   + $"Engineered right now: {(engineered.Count == 0
                       ? "nothing"
                       : string.Join(", ", engineered.Select(module => Where(module))))}."
                   + remembered;
        }

        // More than one is an answer, not a failure — "shield booster" names four slots on plenty of ships,
        // and picking one silently would report about the wrong module with total confidence.
        if (matches.Count > 1)
        {
            var report = new StringBuilder();

            report.AppendLine($"{matches.Count} fitted modules match '{wanted.Trim()}':");

            foreach (var module in matches)
            {
                report.AppendLine("  " + Line(module, active.Engineers));
            }

            return report.ToString().TrimEnd() + remembered;
        }

        return Detail(matches[0], active.Engineers) + remembered;
    }

    private static string Summarise(
        ShipLoadout ship,
        IReadOnlyList<ShipModule> engineered,
        EngineerProgressState progress)
    {
        var report = new StringBuilder();

        var described = ship.Describe();

        report.AppendLine(
            $"{engineered.Count} of {ship.Modules.Count} modules engineered"
            + (described is null ? "." : $" on {described}."));

        if (engineered.Count == 0)
        {
            return report.ToString().TrimEnd();
        }

        report.AppendLine();

        foreach (var module in engineered)
        {
            report.AppendLine("  " + Line(module, progress));
        }

        return report.ToString().TrimEnd();
    }

    private static string Line(ShipModule module, EngineerProgressState progress)
    {
        var report = new StringBuilder(Where(module));

        if (module.Blueprint is { } blueprint)
        {
            report.Append($" — {ModuleNames.Readable(blueprint)}");

            if (module.BlueprintLevel is { } level)
            {
                report.Append($", grade {level}");
            }

            report.Append(", " + Progress(module, progress));
        }
        else
        {
            report.Append(" — not engineered");
        }

        return report.ToString();
    }

    private static string Detail(ShipModule module, EngineerProgressState progress)
    {
        var report = new StringBuilder();

        report.AppendLine(Where(module) + ".");

        if (module.Blueprint is not { } blueprint)
        {
            report.AppendLine("Not engineered.");

            return report.ToString().TrimEnd();
        }

        // The game writes the blueprint as a symbol and never localises it — measured across 20,526
        // engineered modules and 6,272 EngineerCraft events in the corpus, neither carries a readable
        // blueprint name.
        report.Append($"{ModuleNames.Readable(blueprint)}");

        if (module.BlueprintLevel is { } level)
        {
            report.Append($" at grade {level}");
        }

        report.AppendLine(module.Engineer is { } engineer
            ? $", crafted by {engineer}."

            // 27 of 772 engineered modules measured carry an engineer id and no name, every one of them id
            // 399999 — a module that arrived already engineered rather than one somebody rolled.
            : module.EngineerId is not null
                ? ". No engineer is named on it, which is what a module that arrived already "
                  + "engineered looks like."
                : ".");

        report.AppendLine(Progress(module, progress, full: true));

        if (module.Experimental is { } experimental)
        {
            report.AppendLine($"Experimental effect: {experimental}.");
        }

        if (module.Modifiers.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("What the craft did:");

            foreach (var modifier in module.Modifiers)
            {
                report.AppendLine("  " + Modifier(modifier));
            }
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>Whether the grade is finished, and how much is left when it is not.</summary>
    private static string Progress(ShipModule module, EngineerProgressState progress, bool full = false)
    {
        if (module.Quality is not { } quality)
        {
            return full ? "The game reports no progress figure for it." : "progress unknown";
        }

        // One decimal at least, because a fill of "1" reads as a count and this is a fraction.
        var fill = quality.ToString("0.0#", CultureInfo.InvariantCulture);

        if (quality >= EngineeringRules.CompleteAt)
        {
            return full
                ? $"The grade is finished at {fill}, where {EngineeringRules.CompleteAt
                    .ToString("0.0#", CultureInfo.InvariantCulture)} is as far as the game insists."
                : $"finished ({fill})";
        }

        var rank = module.EngineerId is { } id ? progress.For((int)id)?.Rank : null;

        var remaining = module.BlueprintLevel is { } level && rank is { } rolled
            ? EngineeringRules.RollsRemaining(level, rolled, quality)
            : null;

        if (remaining is not { } rolls)
        {
            return full
                ? $"The grade is part crafted, at {fill} of 1."
                : $"part crafted ({fill})";
        }

        return full
            ? $"The grade is part crafted, at {fill} of 1 — {rolls} more craft{(rolls == 1 ? "" : "s")} to fill it."
            : $"{rolls} craft{(rolls == 1 ? "" : "s")} to go ({fill})";
    }

    /// <summary>One modifier in real units.</summary>
    private static string Modifier(ShipModifier modifier)
    {
        var label = Spaced(modifier.Label);

        if (modifier.Value is not { } value)
        {
            // 16 of 3,384 modifiers measured carry text and no number at all — a damage type rather than a
            // quantity.
            return modifier.Text is { } text ? $"{label}: {text}" : label;
        }

        var report = new StringBuilder($"{label} {Number(value)}");

        if (modifier.OriginalValue is { } original)
        {
            report.Append($", was {Number(original)}");
        }

        if (modifier.Change is { } change)
        {
            var direction = modifier.IsImprovement switch
            {
                true => ", better",
                false => ", worse",
                _ => string.Empty,
            };

            report.Append($" ({(change > 0 ? "+" : "")}{Number(change)}{direction})");
        }

        return report.ToString();
    }

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Elite writes modifier labels as run-together words — <c>FSDOptimalMass</c>, <c>PowerDraw</c>.
    /// </summary>
    private static string Spaced(string label)
    {
        var spaced = new StringBuilder(label.Length + 8);

        for (var index = 0; index < label.Length; index++)
        {
            var character = label[index];

            var boundary = index > 0
                           && char.IsUpper(character)
                           && !char.IsWhiteSpace(label[index - 1])
                           && (!char.IsUpper(label[index - 1])
                               || (index + 1 < label.Length && char.IsLower(label[index + 1])));

            if (boundary)
            {
                spaced.Append(' ');
            }

            spaced.Append(character);
        }

        return spaced.ToString();
    }

    /// <summary>What a module is and where it sits.</summary>
    private static string Where(ShipModule module)
    {
        var name = EliteSpecifications.Module(module.Item)?.Name ?? ModuleNames.Readable(module.Item);
        var slot = Spaced(module.Slot.Replace('_', ' '));

        return string.Equals(Catalogue.Relax(slot), Catalogue.Relax(name), StringComparison.Ordinal)
            ? name
            : $"{name} in {slot}";
    }

    /// <summary>Fitted modules matching what was said, against the slot and the module's name.</summary>
    private static IReadOnlyList<ShipModule> Matching(IReadOnlyList<ShipModule> modules, string spoken)
    {
        var wanted = Catalogue.Relax(spoken);

        return wanted.Length == 0
            ? []
            : [.. modules.Where(module =>
                Catalogue.Relax(module.Slot).Contains(wanted, StringComparison.Ordinal)
                || Catalogue.Relax(EliteSpecifications.Module(module.Item)?.Name ?? module.Item)
                    .Contains(wanted, StringComparison.Ordinal))];
    }
}
