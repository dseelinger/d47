using System.Globalization;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Engineering: what a blueprint costs and does, and how a fitted module's roll actually went
/// (list.md Phase 14, <c>#102</c> "Know what engineering actually does").
/// <para>
/// The first place the whole loop runs end to end. Every table Steps 1–5 built — materials,
/// blueprints, the roll rules, the engineer directory — meets the Commander's own journal here,
/// and the two halves answer different questions. <b>What a grade 5 costs</b> is a fact about the
/// game and comes from the tables. <b>What it costs <i>you</i></b> depends on your rank with the
/// engineer who would roll it, and that only the journal knows.
/// </para>
/// <para>
/// <b>Two tools and no more.</b> Tool definitions serialise first in every request, so each one
/// costs the cached prefix (architecture.md §6). A blueprint and a fitted module are the two
/// questions; everything else is a sentence the model can build from their answers.
/// </para>
/// </summary>
public static class EngineeringCapability
{
    public const string Id = "engineering";

    public static CapabilityDescriptor Create(Func<CommanderGameState?> commander) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Engineering",
        Summary = "What a blueprint costs and changes, and how the roll on a fitted module went.",
        Examples =
        [
            "what does increased FSD range cost",
            "how good is my frame shift drive roll",
            "what can I engineer on a power plant",
        ],
        Keywords =
        [
            "my engineering",

            // A phrase rather than the bare word. "blueprint" turns up in sentences that are not
            // about engineering at all, and a single-word keyword hijacks every one of them.
            "blueprint cost",
            "how good is my roll",
        ],
        Tools =
        [
            new ToolDefinition
            {
                Name = "get_blueprint",
                Description =
                    "What an engineering blueprint does and costs: the effects at each grade, the "
                    + "materials one application needs, who offers it and to what grade, and the "
                    + "experimental effects that module can take. Folds the Commander's own rank with "
                    + "each engineer, so the cost of a full grade is an exact total rather than a rate. "
                    + "Name a blueprint, or a kind of module to list what it can take.",
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
                    "How the engineering on the Commander's fitted modules actually turned out: the "
                    + "blueprint and grade, the experimental effect, who rolled it, whether the grade is "
                    + "finished, and what the roll changed in real units. Name a slot or a module for "
                    + "one of them, or ask with neither for every engineered module aboard.",
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
        ],
        Display = new CapabilityDisplay { PanelTitle = "Engineering", Order = 51 },
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

    /// <summary>
    /// Every modification a module kind can take. The shape of the answer to "what can I do to
    /// this", which is a different question from "what does this blueprint cost" and would be
    /// buried if both came back as one wall of recipes.
    /// </summary>
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

        // Per grade, because the recipe changes as the grade climbs and a Commander gathering for
        // grade 3 needs that row rather than the top one.
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

        // The half no table can answer. Everything above is true for everybody; this is true for
        // this Commander, and it is the difference between a rate and a shopping list.
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

            // A kind this build does not recognise. Named rather than guessed at, because the
            // arithmetic below is only correct for one of them.
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
    /// What the top grade costs <em>this</em> Commander, through the best engineer they can
    /// actually reach.
    /// <para>
    /// <b>Rank is a gate and not a discount.</b> Rolls needed is <c>5 − (rank − grade)</c>, and
    /// below the grade there is no number at all — so an unreachable grade is reported as the rank
    /// it needs and the profit that buys it, never as a large shopping list the Commander cannot
    /// use (<see cref="EngineeringRules.RollsFor"/>).
    /// </para>
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
            // Unlocked nobody who grades it. The invitation is the next step and the directory
            // already answers what it asks for, so this points at that rather than repeating it.
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
            // The gate. Grade N cannot be rolled below rank N at all, so the answer is the rank
            // and its price rather than a slow path.
            var price = EngineeringRules.ReputationCost(grade);

            var cost = price is { } profit
                ? $" Reaching grade {grade} with them takes {profit.ToString("N0", CultureInfo.InvariantCulture)} "
                  + "cr of profit sold at their workshop."
                : string.Empty;

            return $"You are grade {rank} with {at}, and grade {grade} needs rank {grade}.{cost}";
        }

        var total = top.TotalFor(rank);

        return total is null
            ? $"You are grade {rank} with {at}: a full grade {grade} is {rolls} roll{(rolls == 1 ? "" : "s")}."
            : $"You are grade {rank} with {at}: a full grade {grade} is {rolls} roll{(rolls == 1 ? "" : "s")}, "
              + $"so {Ingredients(total)}.";
    }

    // ---- How the roll actually went --------------------------------------------------------------

    private static string Fitted(Func<CommanderGameState?> commander, ToolArguments arguments)
    {
        var active = commander();

        if (active is null)
        {
            return "No Elite Dangerous journal has been detected yet.";
        }

        var ship = active.Ship;

        if (!ship.IsKnown)
        {
            // Loadout is written on entering the game and on every outfitting change, so silence
            // before that is missing evidence rather than a ship with nothing fitted.
            return "I have no loadout yet — it is written when you enter the game.";
        }

        var engineered = ship.Engineered;

        arguments.TryGetString("module", out var wanted);

        if (string.IsNullOrWhiteSpace(wanted))
        {
            return Summarise(ship, engineered, active.Engineers);
        }

        var matches = Matching(ship.Modules, wanted);

        if (matches.Count == 0)
        {
            return $"Nothing fitted matches '{wanted.Trim()}'. "
                   + $"Engineered right now: {(engineered.Count == 0
                       ? "nothing"
                       : string.Join(", ", engineered.Select(module => Where(module))))}.";
        }

        // More than one is an answer, not a failure — "shield booster" names four slots on plenty
        // of ships, and picking one silently would report about the wrong module with total
        // confidence.
        if (matches.Count > 1)
        {
            var report = new StringBuilder();

            report.AppendLine($"{matches.Count} fitted modules match '{wanted.Trim()}':");

            foreach (var module in matches)
            {
                report.AppendLine("  " + Line(module, active.Engineers));
            }

            return report.ToString().TrimEnd();
        }

        return Detail(matches[0], active.Engineers);
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

        // The game writes the blueprint as a symbol and never localises it — measured across
        // 20,526 engineered modules and 6,272 EngineerCraft events in the corpus, neither carries a
        // readable blueprint name. So this is Frontier's own name with the underscores taken out,
        // the same treatment stored modules already get: ugly and true beats invented.
        report.Append($"{ModuleNames.Readable(blueprint)}");

        if (module.BlueprintLevel is { } level)
        {
            report.Append($" at grade {level}");
        }

        report.AppendLine(module.Engineer is { } engineer
            ? $", rolled by {engineer}."

            // 27 of 772 engineered modules measured carry an engineer id and no name, every one of
            // them id 399999 — a module that arrived already engineered rather than one somebody
            // rolled. Saying so beats reporting an engineer d47 cannot name.
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
            report.AppendLine("What the roll did:");

            foreach (var modifier in module.Modifiers)
            {
                report.AppendLine("  " + Modifier(modifier));
            }
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>
    /// Whether the grade is finished, and how much is left when it is not.
    /// <para>
    /// <b>0.85 decides finished, not 1.0.</b> Of 994 grades in the corpus, 45 were left below 1.0
    /// and moved on from, and every one sat at 0.85 or above — see
    /// <see cref="EngineeringRules.CompleteAt"/>. Gating on 1.0 would tell a Commander that a
    /// module they can see is finished is not.
    /// </para>
    /// <para>
    /// Rolls remaining needs the Commander's rank with the engineer who rolled it, because the
    /// step size is a function of <c>rank − grade</c> and nothing else. Where that is unknown the
    /// fill is still reported and the roll count is not, rather than being guessed at from the
    /// grade alone.
    /// </para>
    /// </summary>
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
                ? $"The grade is part rolled, at {fill} of 1."
                : $"part rolled ({fill})";
        }

        return full
            ? $"The grade is part rolled, at {fill} of 1 — {rolls} more roll{(rolls == 1 ? "" : "s")} to fill it."
            : $"{rolls} roll{(rolls == 1 ? "" : "s")} to go ({fill})";
    }

    /// <summary>
    /// One modifier in real units. The change is arithmetic on two reported numbers rather than a
    /// claim, and <c>LessIsGood</c> is what decides which direction is the good one — Elite writes
    /// it as 0 or 1, and reading it as a JSON boolean reports every improvement on mass, heat and
    /// power draw backwards.
    /// </summary>
    private static string Modifier(ShipModifier modifier)
    {
        var label = Spaced(modifier.Label);

        if (modifier.Value is not { } value)
        {
            // 16 of 3,384 modifiers measured carry text and no number at all — a damage type
            // rather than a quantity. Read as a number they vanish silently.
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
    /// Elite writes modifier labels as run-together words — <c>FSDOptimalMass</c>,
    /// <c>PowerDraw</c>. Spaced out for speech, leaving runs of capitals like FSD alone.
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

    /// <summary>
    /// What a module is and where it sits. The specification table turns the fitted symbol into a
    /// name — it is keyed on exactly the symbol <c>Loadout</c> writes — and where it has none the
    /// symbol goes out with its underscores removed rather than being dropped.
    /// <para>
    /// The slot is dropped where it only repeats the name. Elite calls the frame shift drive's
    /// slot <c>FrameShiftDrive</c>, and "Frame Shift Drive in Frame Shift Drive" is noise; a cargo
    /// rack in <c>Slot01_Size6</c> genuinely needs both, because a ship has several.
    /// </para>
    /// </summary>
    private static string Where(ShipModule module)
    {
        var name = EliteSpecifications.Module(module.Item)?.Name ?? ModuleNames.Readable(module.Item);
        var slot = Spaced(module.Slot.Replace('_', ' '));

        return string.Equals(Catalogue.Relax(slot), Catalogue.Relax(name), StringComparison.Ordinal)
            ? name
            : $"{name} in {slot}";
    }

    /// <summary>
    /// Fitted modules matching what was said, against the slot and the module's name.
    /// <para>
    /// A fragment rather than a catalogue match, and against both, because "frame shift drive" is
    /// the module and "MainEngines" is the slot and a Commander uses whichever they can see.
    /// </para>
    /// </summary>
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
