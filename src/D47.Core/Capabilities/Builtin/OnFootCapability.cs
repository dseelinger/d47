using System.Globalization;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// On foot: what the Commander is wearing, what upgrading or modifying it costs, who does it, and
/// where the materials come from (Phase 20).
/// <para>
/// <b>Two axes, and neither one is the ship model.</b> Grade 1 to 5 is bought at Pioneer Supplies
/// for a fixed list and a fixed price — no roll, no quality, nothing random. Modifications come
/// from an engineer and are ungraded, binary and <b>permanent</b>: four slots at most, one earned
/// per grade above 1, and a wrong choice recoverable only by buying and re-upgrading a fresh item.
/// So every figure here is exact, and none of the ship side's floor-and-rolls hedging applies.
/// </para>
/// <para>
/// <b>The numbers are not the ones the sources publish.</b> Every on-foot quantity in EDEngineer
/// predates the patch that cut them — modifications by half, grade upgrades by two thirds, with
/// Power Regulators removed outright — and <c>tools/gen-blueprints.py</c> restates them to what the
/// game actually charges, measured against 16 real upgrades and four locker deltas. Quoting the
/// published figures would have been two to three times wrong about everything on this page while
/// agreeing with every other tool. See <c>docs/spikes/journal-corpus-on-foot.md</c> §3.
/// </para>
/// <para>
/// <b>Ordering is a routing fact rather than a preference.</b> A grade 1 item has zero modification
/// slots, and an engineer's base has no Pioneer Supplies — only an Apex desk — so the upgrade
/// cannot be done on arrival. "Grade first" is a step in the list, not a footnote under it.
/// </para>
/// </summary>
public static class OnFootCapability
{
    public const string Id = "on-foot";

    /// <param name="plans">
    /// The Commander's suit and weapon plans (Phase 27). Null under the designer and in
    /// tests that are not about them, and the plan tools then answer that nothing is being
    /// tracked rather than throwing.
    /// </param>
    public static CapabilityDescriptor Create(
        Func<CommanderGameState?> commander, OnFootPlanService? plans = null) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "On foot",
        Summary =
            "What the Commander is wearing and carrying, what an on-foot grade or modification costs, "
            + "and where its materials come from.",
        Examples =
        [
            "what suit am I wearing",
            "what does night vision cost",
            "what would grade 5 on this suit take",
            "where do I find graphene",
        ],
        // Each names its tool (#161); five here take no required argument, two of which change
        // a plan rather than report one.
        Keywords =
        [
            new("what am I wearing", "get_on_foot_loadout"),
            new("my suit", "get_on_foot_loadout"),
            new("suit grade", "get_on_foot_loadout"),
        ],
        Tools =
        [
            new ToolDefinition
            {
                Name = "get_on_foot_loadout",
                Description =
                    "What the Commander is wearing and carrying on foot: the suit and its grade, the "
                    + "modifications fitted to it and how many slots are left, and each weapon with its "
                    + "grade and modifications. Read from the journal, so it is about this Commander.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(DescribeLoadout(commander))),
            },
            new ToolDefinition
            {
                Name = "get_on_foot_engineering",
                Description =
                    "What an on-foot modification or grade upgrade costs and who can do it. Give a "
                    + "modification by name for its recipe, engineers and effect; give a suit or weapon "
                    + "with a grade for what reaching that grade takes in materials and credits. "
                    + "Quantities are exact — nothing on foot is rolled.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "modification",
                        Type = ToolParameterType.String,
                        Description =
                            "A modification by name — for example \"Night Vision\", \"Magazine Size\" "
                            + "or \"Quieter Footsteps\".",
                    },
                    new ToolParameter
                    {
                        Name = "equipment",
                        Type = ToolParameterType.String,
                        Description =
                            "A suit or weapon by name — for example \"Maverick\", \"Dominator\" or "
                            + "\"Karma AR-50\". Defaults to what the Commander is wearing.",
                    },
                    new ToolParameter
                    {
                        Name = "grade",
                        Type = ToolParameterType.Integer,
                        Description = "The grade to reach, 2 to 5.",
                    },
                ],
                Handler = (arguments, _) =>
                    Task.FromResult(ToolResult.Ok(DescribeEngineering(commander, arguments))),
            },
            new ToolDefinition
            {
                Name = "find_micro_resource",
                Description =
                    "Where one on-foot material comes from and what the Bartender will exchange for it: "
                    + "which settlement types, which buildings and which containers hold it, how many the "
                    + "Commander has, and what trading for it costs in other Components.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "material",
                        Type = ToolParameterType.String,
                        Description =
                            "A micro-resource by name — for example \"Graphene\", \"Circuit Board\" "
                            + "or \"Opinion Polls\".",
                        Required = true,
                    },
                    new ToolParameter
                    {
                        Name = "wanted",
                        Type = ToolParameterType.Integer,
                        Description = "How many are wanted, for the trade arithmetic. Defaults to the shortfall.",
                    },
                ],
                Handler = (arguments, _) =>
                    Task.FromResult(ToolResult.Ok(DescribeMaterial(commander, arguments))),
            },

            // The three plan tools are Protected, so none of them is advertised to the model and
            // all three are reachable by phrase and by press. That is Phase 26's finding applied
            // rather than rediscovered: the advertised surface is re-billed every turn and was
            // measured at 39,693 bytes against a 40,000 ceiling, so a capability that can be a
            // phrase should be. `plan_on_foot_build` stays the one model-callable route, because
            // reading "take my Maverick to grade 5 with night vision" out of free English is the
            // one thing here that needs a model.
            new ToolDefinition
            {
                Protected = true,
                Name = "get_on_foot_plans",
                Description =
                    "Every suit and weapon the Commander is carrying or has planned for, with the grade "
                    + "each is at and how many slots its plan has an opinion about.",
                Commands =
                [
                    new ToolCommandPhrase("what have I planned on foot", Nothing),
                    new ToolCommandPhrase("read my suit plans", Nothing),
                ],
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(Kit(plans))),
            },

            new ToolDefinition
            {
                Protected = true,
                Name = "promote_on_foot_plan",
                Description =
                    "Offer a suit's or weapon's plan to the checklist. It is a proposal: the Commander "
                    + "accepts, and the grade always comes first because a grade 1 item has no "
                    + "modification slots and an engineer's base has no Pioneer Supplies.",
                Parameters = [Which, Weapon],
                Commands =
                [
                    new ToolCommandPhrase("put that on my checklist", Nothing),
                    new ToolCommandPhrase("promote this suit plan", Nothing),
                ],
                Handler = (arguments, _) => Task.FromResult(Promote(plans, arguments)),
            },

            // Protected for safety as well as cost. A plan is often weeks of intent, the model
            // consumes untrusted text, and a hostile in-game message asking d47 to throw one away
            // is exactly the shape of thing the trust boundary exists for.
            new ToolDefinition
            {
                Protected = true,
                Name = "drop_on_foot_plan",
                Description =
                    "Drop a suit's or weapon's plan. The Commander's own act: not offered to the model, "
                    + "and refused if it asks. What the plan already put on the checklist is kept.",
                Parameters = [Which, Weapon],
                Handler = (arguments, _) => Task.FromResult(Drop(plans, arguments)),
            },
        ],
        Display = new CapabilityDisplay { PanelTitle = "On foot", Order = 57 },
    };

    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Which item a plan tool is about. The same pair on both, so they read alike.</summary>
    private static ToolParameter Which => new()
    {
        Name = "item",
        Type = ToolParameterType.String,
        Description =
            "The suit or weapon by name. Omit for the suit being worn, or the one weapon carried.",
    };

    private static ToolParameter Weapon => new()
    {
        Name = "weapon",
        Type = ToolParameterType.Boolean,
        Description = "True when this is about a hand weapon rather than the suit.",
    };

    /// <summary>
    /// What the model is told about the on-foot plans on every turn, below the cache breakpoint.
    /// A count and nothing else: the plans themselves are a tool call away, and this block is
    /// re-billed every turn.
    /// </summary>
    public static string? Live(OnFootPlanService? plans)
    {
        if (plans is null)
        {
            return null;
        }

        var planned = plans.Kit().Count(entry => entry.Planned > 0);

        return planned == 0
            ? null
            : $"On foot: {planned.ToString(CultureInfo.InvariantCulture)} suit or weapon plan"
              + (planned == 1 ? "." : "s.");
    }

    private static string Kit(OnFootPlanService? plans)
    {
        if (plans is null)
        {
            return "I am not tracking any suit or weapon plans.";
        }

        var kit = plans.Kit();

        if (kit.Count == 0)
        {
            return "I have not seen you on foot yet, and nothing is planned.";
        }

        return string.Join(Environment.NewLine, kit.Select(entry => entry.Planned > 0
            ? $"{entry.Describe()}, {entry.Planned.ToString(CultureInfo.InvariantCulture)} planned"
            : entry.Describe()));
    }

    private static ToolResult Promote(OnFootPlanService? plans, ToolArguments arguments)
    {
        if (plans is null)
        {
            return ToolResult.Error("I am not tracking any suit or weapon plans.");
        }

        return Whichever(plans, arguments) is not { } build
            ? ToolResult.Error("I could not tell which suit or weapon you mean.")
            : ToolResult.Ok(plans.Promote(build.Id));
    }

    private static ToolResult Drop(OnFootPlanService? plans, ToolArguments arguments)
    {
        if (plans is null)
        {
            return ToolResult.Error("I am not tracking any suit or weapon plans.");
        }

        return Whichever(plans, arguments) is not { } build
            ? ToolResult.Error("I could not tell which suit or weapon you mean.")
            : ToolResult.Ok(plans.Delete(build.Id));
    }

    /// <summary>The item a tool call names, through the service's own matcher.</summary>
    private static OnFootBuild? Whichever(OnFootPlanService plans, ToolArguments arguments) =>
        OnFootPlanService.Which(
            plans,
            arguments.TryGetString("item", out var item) && !string.IsNullOrWhiteSpace(item)
                ? item.Trim()
                : null,
            arguments.TryGetBoolean("weapon", out var weapon) && weapon);

    private static string DescribeLoadout(Func<CommanderGameState?> commander)
    {
        var active = commander();

        if (active is null)
        {
            return "No Elite Dangerous journal has been detected yet.";
        }

        var loadout = active.OnFoot;

        if (!loadout.IsKnown)
        {
            // Written when the Commander goes on foot, so silence is missing evidence rather than
            // a Commander wearing nothing.
            return "I have not seen you on foot this session, so I do not know what you are wearing.";
        }

        var report = new StringBuilder();

        report.Append(CultureInfo.InvariantCulture, $"Wearing: {loadout.Speak()}");

        if (loadout.LoadoutName is { Length: > 0 } named)
        {
            report.Append(CultureInfo.InvariantCulture, $" — loadout \"{named}\"");
        }

        report.AppendLine(".");

        report.AppendLine(Slots("Suit", loadout.Grade, loadout.SuitModifications, loadout.FreeSlots));

        foreach (var weapon in loadout.Weapons.OrderBy(weapon => weapon.Slot, StringComparer.Ordinal))
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"{weapon.Slot}: {weapon.Speak()}.");
            report.AppendLine(Slots(weapon.Slot, weapon.Grade, weapon.Modifications, weapon.FreeSlots));
        }

        // Said once, at the end, because it is the reason this capability exists and not a caveat
        // about any one line: nothing on foot can be undone.
        report.Append(
            "Modifications are permanent — they cannot be removed or replaced, and a wrong one is "
            + "recoverable only by buying and re-upgrading a fresh item.");

        return report.ToString();
    }

    private static string Slots(
        string what, int? grade, IReadOnlyList<FittedModification> fitted, int? free)
    {
        var report = new StringBuilder();

        if (fitted.Count == 0)
        {
            report.Append(CultureInfo.InvariantCulture, $"  {what}: nothing fitted");
        }
        else
        {
            report.Append(CultureInfo.InvariantCulture,
                $"  {what}: {string.Join(", ", fitted.Select(modification => modification.Speak()))}");
        }

        if (grade is null or < OnFootRules.ModifiableFrom)
        {
            // Not a shortage and not a slow route: grade 1 cannot be modified at all, and the fix
            // is Pioneer Supplies rather than an engineer.
            report.Append(". Nothing can be fitted below grade 2 — that is an upgrade first, not a trip to an engineer.");
            return report.ToString();
        }

        report.Append(CultureInfo.InvariantCulture,
            $", {free ?? 0} of {OnFootRules.SlotsAt(grade.Value)} slot"
            + $"{(OnFootRules.SlotsAt(grade.Value) == 1 ? "" : "s")} free.");

        // A symbol d47 cannot name is said as one, exactly as a fitted ship blueprint is, rather
        // than being dropped from the list as though nothing were there.
        var unnamed = fitted.Where(modification => !modification.IsNamed).ToArray();

        if (unnamed.Length > 0)
        {
            var symbols = string.Join(", ", unnamed.Select(modification => modification.Speak()));

            report.Append(CultureInfo.InvariantCulture,
                $" I have no name for {symbols} — Elite writes those as symbols and nothing I ship joins the two spellings.");
        }

        return report.ToString();
    }

    private static string DescribeEngineering(Func<CommanderGameState?> commander, ToolArguments arguments)
    {
        var active = commander();

        if (Text(arguments, "modification") is { Length: > 0 } wanted)
        {
            return DescribeModification(active, wanted);
        }

        return DescribeUpgrade(active, Text(arguments, "equipment"), Number(arguments, "grade"));
    }

    private static string DescribeModification(CommanderGameState? active, string wanted)
    {
        var recipes = BlueprintCatalogue.All
            .Where(blueprint => blueprint.Kind is BlueprintKind.Suit or BlueprintKind.Weapon)
            .ToArray();

        var names = recipes.Select(blueprint => blueprint.Name).Distinct(StringComparer.Ordinal).ToArray();

        if (Catalogue.Match(names, wanted) is not { } name)
        {
            var near = Catalogue.Near(names, wanted);

            return near.Count == 0
                ? $"I have no on-foot modification called \"{wanted}\"."
                : $"I have no on-foot modification called \"{wanted}\". Did you mean {Join(near)}?";
        }

        var report = new StringBuilder();

        foreach (var recipe in recipes.Where(blueprint => blueprint.Name == name))
        {
            report.Append(CultureInfo.InvariantCulture,
                $"{recipe.Name} — a {(recipe.Kind == BlueprintKind.Suit ? "suit" : "weapon")} modification.");
            report.AppendLine();

            report.AppendLine(CultureInfo.InvariantCulture, $"  Costs: {Ingredients(recipe, active)}");

            report.AppendLine(recipe.Engineers.Count == 0
                ? "  I have no engineer for it."
                : $"  From: {Join(recipe.Engineers)}.");

            foreach (var effect in recipe.Effects)
            {
                report.Append(CultureInfo.InvariantCulture,
                    $"  {effect.Property}: {effect.Change}");
                report.AppendLine();
            }
        }

        report.Append(
            "It is ungraded and permanent, and it needs the item at grade 2 or better before it can "
            + "be applied at all.");

        return report.ToString();
    }

    private static string DescribeUpgrade(CommanderGameState? active, string? equipment, int? grade)
    {
        var entry = equipment is { Length: > 0 } named
            ? OnFootCatalogue.Named(named)
            : active?.OnFoot.Suit;

        if (entry is null)
        {
            return equipment is { Length: > 0 } unknown
                ? $"I have no suit or weapon called \"{unknown}\"."
                : "Name a suit or a weapon, or go on foot so I can see what you are wearing.";
        }

        if (grade is not { } target || target is < 2 or > 5)
        {
            return $"{entry.Name}: name a grade between 2 and 5 and I will price reaching it.";
        }

        var recipes = BlueprintCatalogue.All
            .Where(blueprint => blueprint.Kind == BlueprintKind.Vendor
                                && string.Equals(blueprint.Name, Vendor(entry.Name), StringComparison.OrdinalIgnoreCase)
                                && blueprint.Grade == target)
            .ToArray();

        if (recipes.Length == 0)
        {
            return $"I have no grade {target} upgrade recipe for {entry.Name}.";
        }

        var report = new StringBuilder();

        report.Append(CultureInfo.InvariantCulture,
            $"{entry.Name} to grade {target}, at Pioneer Supplies.");
        report.AppendLine();

        foreach (var recipe in recipes)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"  Costs: {Ingredients(recipe, active)}");
        }

        // The upgrade cost from where the Commander actually is, where that is known, because
        // "grade 5 costs X" and "grade 5 from here costs X" are different numbers.
        var from = entry.Grade ?? 1;

        if (OnFootRules.UpgradeCost(OnFootCatalogue.BasePrice(entry), from, target) is { } credits)
        {
            report.Append(CultureInfo.InvariantCulture,
                $"  Credits from grade {from}: {credits.ToString("N0", CultureInfo.InvariantCulture)}.");
            report.AppendLine();
        }
        else
        {
            // Null rather than a partial sum. Six of the eleven weapons have no published price,
            // and a total missing a step reads exactly like a total that is not.
            report.AppendLine("  I do not know its purchase price, so I cannot give a credit cost.");
        }

        var slots = OnFootRules.SlotsAt(target);

        report.Append(CultureInfo.InvariantCulture,
            $"  Grade {target} carries {slots} modification slot{(slots == 1 ? "" : "s")}. An engineer's base has no Pioneer Supplies, so this has to happen before the trip.");

        return report.ToString();
    }

    /// <summary>
    /// The name the upgrade recipes are filed under, which is the equipment's without the word
    /// "Suit" on the end — EDDiscovery writes "Maverick Suit" and EDEngineer writes "Maverick".
    /// </summary>
    private static string Vendor(string name) =>
        name.EndsWith(" Suit", StringComparison.OrdinalIgnoreCase) ? name[..^" Suit".Length] : name;

    private static string Ingredients(Blueprint recipe, CommanderGameState? active) =>
        recipe.Ingredients.Count == 0
            ? "nothing I have a recipe for"
            : string.Join(", ", recipe.Ingredients.Select(ingredient =>
            {
                var material = ingredient.Material;
                var name = material?.Name ?? ingredient.Symbol;
                var held = active?.Suit.CountOf(ingredient.Symbol) ?? 0;
                var missing = Math.Max(0, ingredient.Size - held);

                return missing == 0
                    ? $"{ingredient.Size} × {name} (have {held})"
                    : $"{ingredient.Size} × {name} (have {held}, short {missing})";
            }));

    private static string DescribeMaterial(Func<CommanderGameState?> commander, ToolArguments arguments)
    {
        if (Text(arguments, "material") is not { Length: > 0 } wanted)
        {
            return "Name a micro-resource.";
        }

        var material = MaterialCatalogue.Find(wanted);

        if (material is null || material.Ledger != MaterialLedger.ShipLocker)
        {
            var near = MaterialCatalogue.Near(wanted);

            return material is not null
                ? MaterialSeam.NotThisOne(material, MaterialSeam.MicroResourceTool)
                : near.Count == 0
                    ? $"I have no on-foot material called \"{wanted}\"."
                    : $"I have no on-foot material called \"{wanted}\". Did you mean {Join(near)}?";
        }

        var active = commander();
        var held = active?.Suit.CountOf(material.Symbol) ?? 0;

        var report = new StringBuilder();

        report.Append(CultureInfo.InvariantCulture,
            $"{material.Name} — {material.Category ?? "an on-foot material"}. You have {held}.");
        report.AppendLine();

        report.AppendLine(material.Origins.Count == 0
            ? "  I have no sourcing for it."
            : $"  Found in: {Join(material.Origins)}.");

        // The locker cap is per category and it is 1,000 — measured, because the sources
        // disagreed. Worth saying when it is close, because gathering past it is wasted time.
        if (active?.Suit is { IsKnown: true } inventory && material.Category is { } category)
        {
            var total = inventory.ShipLockerTotal(CategoryOf(category));

            if (total > OnFootRules.LockerCapacityPerCategory * 3 / 4)
            {
                report.AppendLine(CultureInfo.InvariantCulture,
                    $"  Your locker holds {total} of a {OnFootRules.LockerCapacityPerCategory} cap on "
                    + $"{CategoryOf(category)}, and the cap is per category rather than per item.");
            }
        }

        report.Append(Barter(material, Number(arguments, "wanted") ?? 1, active));

        return report.ToString();
    }

    /// <summary>
    /// What the Bartender would take for it, which is arithmetic rather than a guess.
    /// <para>
    /// Components only. Items and Data are sold for credits and cannot be exchanged at all, and
    /// saying which is which is the answer rather than a caveat on one.
    /// </para>
    /// </summary>
    private static string Barter(MaterialEntry material, int wanted, CommanderGameState? active)
    {
        if (material.BarterCost is not { } cost)
        {
            return "  The Bartender does not exchange this — only Components can be bartered for; "
                   + "Items and Data are sold for credits.";
        }

        var report = new StringBuilder();

        report.Append(CultureInfo.InvariantCulture,
            $"  The Bartender charges {cost} barter value for each.");
        report.AppendLine();

        var offers = MaterialCatalogue.All
            .Where(other => other.BarterValue is not null && other.Symbol != material.Symbol)
            .Select(other => (other, Held: active?.Suit.CountOf(other.Symbol) ?? 0))
            .Where(pair => pair.Held > 0)
            .Select(pair => (
                pair.other,
                pair.Held,
                Cost: OnFootRules.BarterCostOf(wanted, pair.other.BarterValue, cost)))
            .Where(pair => pair.Cost is { } needed && needed <= pair.Held)
            .OrderBy(pair => pair.Cost)
            .Take(5)
            .ToArray();

        if (offers.Length == 0)
        {
            report.Append("  Nothing you are carrying covers a trade for it.");
            return report.ToString();
        }

        report.Append(CultureInfo.InvariantCulture,
            $"  For {wanted}, you could hand over any of:");
        report.AppendLine();

        foreach (var offer in offers)
        {
            report.Append(CultureInfo.InvariantCulture,
                $"    {offer.Cost} × {offer.other.Name} (you have {offer.Held})");
            report.AppendLine();
        }

        // Said because it changes what a Commander should offer: rounding is theirs to lose, so
        // trading in one lump beats trading in ones.
        report.Append("  Whatever value the last unit does not use is lost, so trade in one go.");

        return report.ToString();
    }

    /// <summary>
    /// Elite's four locker tabs, from the category the material table carries. The table's
    /// categories are the community's spelling — "Component" against the tab's "Components".
    /// </summary>
    private static string CategoryOf(string category) => category switch
    {
        "Component" => "Components",
        "Consumable" => "Consumables",
        "Data" => "Data",
        _ => "Items",
    };

    private static string? Text(ToolArguments arguments, string name) =>
        arguments.TryGetString(name, out var value) && value.Trim().Length > 0 ? value.Trim() : null;

    private static int? Number(ToolArguments arguments, string name) =>
        arguments.TryGetInt32(name, out var value) ? value : null;

    private static string Join(IReadOnlyList<string> values) =>
        values.Count switch
        {
            0 => string.Empty,
            1 => values[0],
            _ => string.Join(", ", values.Take(values.Count - 1)) + " or " + values[^1],
        };
}
