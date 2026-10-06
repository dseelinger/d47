using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using D47.Core.Checklists;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Core.Ships;

/// <summary>What the Commander said about a plan, as the model read it.</summary>
public enum BuildRemarkKind
{
    /// <summary>A question about one slot: "why that thruster".</summary>
    Question,

    /// <summary>"Is this build any good."</summary>
    Critique,

    /// <summary>"Make it jump further", which produces a proposal.</summary>
    Goal,
}

/// <summary>One round of a conversation about a plan: what the Commander said and what the core answered.</summary>
public sealed record BuildRemark(string Remark, string? Reply);

/// <summary>One proposed change to one slot's plan. <see cref="Before"/> is null where the slot had no plan.</summary>
public sealed record SlotChange(string Slot, SlotPlan? Before, SlotPlan After, string Reason);

/// <summary>
/// The answer to one remark about a plan. A proposal is never applied here; <see cref="Cost"/> is the plan
/// with every change applied, computed by <see cref="EngineeringPlan"/>.
/// </summary>
public sealed record BuildAdvice(
    BuildRemarkKind Kind,
    string? Reply,
    IReadOnlyList<SlotChange> Changes,
    PlanCosting? Cost,
    IReadOnlyList<string> Dropped,
    string? Refusal = null)
{
    public bool Succeeded => Refusal is null;
}

/// <summary>Discusses one ship's plan with the model: a question, a critique, or a goal that produces a proposal.</summary>
public sealed class ShipPlanAdvisor(
    Func<ILlmProvider?> provider,
    Func<string?> model,
    Func<string?> persona,
    Func<string?> aboutMe,
    Func<CommanderGameState?> state,
    SpendTracker? spend,
    PriceTable? prices,
    ILogger logger)
{
    /// <summary>The most modules a reply names.</summary>
    public const int MostModulesNamed = 3;

    private const int Budget = 3000;

    public async Task<BuildAdvice> AdviseAsync(
        ShipBuild build,
        IReadOnlyList<BuildRemark> exchange,
        string remark,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(exchange);
        ArgumentException.ThrowIfNullOrWhiteSpace(remark);

        if (provider() is null)
        {
            return new BuildAdvice(BuildRemarkKind.Question, null, [], null, [], "No language model is configured, and advice on a build has to come from one.");
        }

        var game = state();
        var instruction = Instruction(build, game, exchange, remark);
        var first = await AskJsonAsync(instruction, cancellationToken).ConfigureAwait(false);

        if (first is null || Read(first) is not { } answer)
        {
            return new BuildAdvice(BuildRemarkKind.Question, null, [], null, [], "The model's answer was not one I could read. Try again.");
        }

        var checkedChanges = Check(build, game, answer);

        if (answer.Kind == BuildRemarkKind.Goal && checkedChanges.Any(change => change.Refusal is not null))
        {
            var again = await AskJsonAsync(
                Retry(instruction, first, [.. checkedChanges.Where(change => change.Refusal is not null).Select(change => change.Refusal!)]),
                cancellationToken).ConfigureAwait(false);

            if (again is not null && Read(again) is { Kind: BuildRemarkKind.Goal } second)
            {
                answer = second;
                checkedChanges = Check(build, game, second);
            }
        }

        if (answer.Kind != BuildRemarkKind.Goal)
        {
            return new BuildAdvice(answer.Kind, Limit(answer.Reply, []), [], null, []);
        }

        var changes = checkedChanges.Where(change => change.Change is not null).Select(change => change.Change!).ToList();
        var dropped = checkedChanges.Where(change => change.Refusal is not null).Select(change => change.Refusal!).ToList();

        var tail = new List<string>();

        if (dropped.Count > 0)
        {
            var said = checkedChanges.Where(change => change.Refusal is not null).Select(change => change.Said ?? "a slot").Distinct().ToList();

            tail.Add($"I left out the {(said.Count == 1 ? "change" : "changes")} to {And(said)}, which the outfitting tables refused.");
        }

        if (changes.Count > MostModulesNamed)
        {
            tail.Add($"{changes.Count.ToString(CultureInfo.InvariantCulture)} slots change; they are on the ship's page.");
        }

        var applied = changes.Aggregate(build, (plan, change) => plan.With(change.After));

        return new BuildAdvice(BuildRemarkKind.Goal, Limit(answer.Reply, tail), changes, Cost(applied, game), dropped);
    }

    private static string And(IReadOnlyList<string> items) => items.Count switch
    {
        1 => items[0],
        _ => $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}",
    };

    /// <summary>What <see cref="EngineeringPlan"/> says a build costs.</summary>
    public static PlanCosting Cost(ShipBuild build, CommanderGameState? state)
    {
        ArgumentNullException.ThrowIfNull(build);

        var planned = build.Slots
            .Where(slot => slot.Blueprint is not null || slot.Grade > 0 || slot.Experimental is not null)
            .Select(slot => slot.ToRequest())
            .ToList();

        return EngineeringPlan.Cost(
            EngineeringPlan.Items(build.Scope ?? ChecklistScope.Universal, build.Hull, planned),
            state);
    }

    private async Task<string?> AskJsonAsync(string instruction, CancellationToken cancellationToken)
    {
        var reply = await FlavourTurn.AskAsync(
            provider(),
            model(),
            persona(),
            aboutMe(),
            instruction,
            gameState: null,
            spend,
            prices,
            logger,
            cancellationToken,
            maxOutputTokens: Budget,
            effort: ThinkingEffort.Medium,
            sampling: LlmSampling.Lore).ConfigureAwait(false);

        return reply is null ? null : Adventures.AdventureGenerator.Unfence(reply);
    }

    // ---- the instruction --------------------------------------------------------------------

    /// <summary>The whole user turn for one remark.</summary>
    internal static string Instruction(
        ShipBuild build,
        CommanderGameState? state,
        IReadOnlyList<BuildRemark> exchange,
        string remark)
    {
        var text = new StringBuilder();

        text.AppendLine(
            "The Commander is asking you about their plan for one ship in Elite Dangerous. Decide which "
            + "kind of remark it is: \"question\" (about a slot or a choice in the plan), \"critique\" (how "
            + "good the plan is), or \"goal\" (something they want the ship to do better).");
        text.AppendLine();
        text.AppendLine("Rules:");
        text.AppendLine("- A question or a critique is answered in the reply and proposes no changes.");
        text.AppendLine(
            "- A goal proposes changes, one per slot. Each change states the slot's whole plan after the "
            + "change: the module, its class and rating (and mount for a weapon), the blueprint, the grade "
            + "and the experimental effect. Omit module to keep the slot's planned module. Omit blueprint, "
            + "grade or experimental for none.");
        text.AppendLine(
            "- Use only slots, modules, blueprints and experimentals named below, spelt as they are here. "
            + "A module must fit its slot's size, and a restricted slot takes only what it lists.");
        text.AppendLine(
            "- Materials, caps and engineer ranks are given below and are not yours to work out. Do not "
            + "quote material counts.");
        text.AppendLine(
            $"- The reply is spoken aloud. It names at most {MostModulesNamed.ToString(CultureInfo.InvariantCulture)} "
            + "modules and is a few sentences long.");
        text.AppendLine("- Nothing you propose is applied until the Commander accepts it.");
        text.AppendLine();

        Hull(text, build, state);
        Costing(text, Cost(build, state));
        Blueprints(text, build);

        if (exchange.Count > 0)
        {
            text.AppendLine("The conversation so far:");

            foreach (var round in exchange)
            {
                text.AppendLine($"Commander: {round.Remark}");

                if (round.Reply is { Length: > 0 } reply)
                {
                    text.AppendLine($"You: {reply}");
                }
            }

            text.AppendLine();
        }

        text.AppendLine($"The Commander now says: {remark}");
        text.AppendLine();
        text.AppendLine("Answer with one JSON object and nothing else:");
        text.AppendLine(
            "{\"kind\":\"question|critique|goal\",\"reply\":\"...\",\"changes\":[{\"slot\":\"MainEngines\","
            + "\"module\":\"Thrusters\",\"class\":5,\"rating\":\"A\",\"mount\":null,\"blueprint\":\"Dirty Drive Tuning\","
            + "\"grade\":5,\"experimental\":\"Drag Drives\",\"reason\":\"one line\"}]}");

        return text.ToString();
    }

    private static void Hull(StringBuilder text, ShipBuild build, CommanderGameState? state)
    {
        text.AppendLine($"Ship: {build.Describe()}.");

        var fitted = Fitted(build, state);

        text.AppendLine(fitted is null
            ? "What is fitted is not known: the ship is not owned or its loadout has not been seen."
            : "What is fitted is as the journal last reported.");
        text.AppendLine();

        var slots = EliteSpecifications.Slots(build.Hull);

        if (slots.Count == 0)
        {
            text.AppendLine("d47 has no slot layout for this hull. Planned slots:");

            foreach (var plan in build.Slots)
            {
                text.AppendLine($"- {plan.Slot}: plan {plan.Describe()}");
            }

            text.AppendLine();
            return;
        }

        // Slots that take the same modules share one list, printed once.
        var lists = new List<string>();

        text.AppendLine("Slots (name, size, plan, fitted, modules it takes):");

        foreach (var slot in slots)
        {
            var takes = string.Join(", ", EliteSpecifications.ModulesFor(slot)
                .Select(module => module.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase));

            var at = lists.IndexOf(takes);

            if (at < 0)
            {
                lists.Add(takes);
                at = lists.Count - 1;
            }

            var restricted = slot.Restrict.Count > 0 ? ", restricted" : string.Empty;
            var plan = build.For(slot.Name) is { IsEmpty: false } planned ? planned.Describe() : "none";

            text.AppendLine(
                $"- {slot.Name} ({slot.Describe()}), size {slot.Size.ToString(CultureInfo.InvariantCulture)}{restricted}; "
                + $"plan: {plan}; fitted: {Fitted(fitted, slot.Name)}; takes list {(at + 1).ToString(CultureInfo.InvariantCulture)}");
        }

        text.AppendLine();
        text.AppendLine("Module lists:");

        for (var index = 0; index < lists.Count; index++)
        {
            text.AppendLine($"{(index + 1).ToString(CultureInfo.InvariantCulture)}. {(lists[index].Length > 0 ? lists[index] : "nothing")}");
        }

        text.AppendLine();
    }

    /// <summary>The fitted module in one slot: empty, unknown to d47, or named. The first two are never merged.</summary>
    private static string Fitted(IReadOnlyList<ShipModule>? fitted, string slot)
    {
        if (fitted is null)
        {
            return "not known";
        }

        if (fitted.FirstOrDefault(module => string.Equals(module.Slot, slot, StringComparison.OrdinalIgnoreCase)) is not { } module)
        {
            return "empty";
        }

        if (EliteSpecifications.Module(module.Item) is not { } spec)
        {
            return $"a module d47 does not know ({module.Item})";
        }

        var said = EliteSpecifications.ModuleName(spec.Symbol) ?? spec.Name;

        if (module.Blueprint is { Length: > 0 } blueprint)
        {
            said += $", {BlueprintCatalogue.NameOf(blueprint) ?? blueprint}";

            if (module.BlueprintLevel is { } level)
            {
                said += $" grade {level.ToString(CultureInfo.InvariantCulture)}";
            }
        }

        if (module.ExperimentalSymbol is { Length: > 0 } experimental)
        {
            said += $", {BlueprintCatalogue.NameOf(experimental) ?? module.Experimental ?? experimental}";
        }

        return said;
    }

    /// <summary>The modules fitted to this build's own ship, or null where none have been seen.</summary>
    private static IReadOnlyList<ShipModule>? Fitted(ShipBuild build, CommanderGameState? state)
    {
        if (build.ShipId is not { } shipId || state is null)
        {
            return null;
        }

        if (state.Ship is { IsKnown: true } live && live.ShipId == shipId)
        {
            return live.Modules;
        }

        return state.Loadouts.For(shipId)?.Loadout.Modules;
    }

    private static void Costing(StringBuilder text, PlanCosting costing)
    {
        text.AppendLine("What the plan as it stands costs (computed by d47):");

        if (costing.Ingredients.Count == 0)
        {
            text.AppendLine("- no engineering is planned");
        }

        foreach (var ingredient in costing.Shortfall)
        {
            text.AppendLine(
                $"- short of {ingredient.Material.Name}: needs {ingredient.Needed.ToString(CultureInfo.InvariantCulture)}, "
                + $"holds {ingredient.Held.ToString(CultureInfo.InvariantCulture)}");
        }

        if (costing.Ingredients.Count > 0 && costing.Shortfall.Count == 0)
        {
            text.AppendLine("- every material is held");
        }

        foreach (var ingredient in costing.OverCapacity)
        {
            text.AppendLine($"- {ingredient.Material.Name} needs more than one load: over the cap of {ingredient.Capacity?.ToString(CultureInfo.InvariantCulture)}");
        }

        foreach (var gate in costing.Gates)
        {
            text.AppendLine($"- gated: {gate}");
        }

        text.AppendLine();
    }

    /// <summary>The blueprints and experimentals of every module the hull takes.</summary>
    private static void Blueprints(StringBuilder text, ShipBuild build)
    {
        var slots = EliteSpecifications.Slots(build.Hull);
        var modules = slots
            .SelectMany(EliteSpecifications.ModulesFor)
            .GroupBy(module => module.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(module => module.Name, StringComparer.OrdinalIgnoreCase);

        text.AppendLine("Blueprints by module:");

        foreach (var module in modules)
        {
            if (BlueprintCatalogue.For(module) is not { Count: > 0 } recipes)
            {
                continue;
            }

            var blueprints = recipes
                .Where(recipe => recipe.Kind == BlueprintKind.Modification)
                .GroupBy(recipe => recipe.Name, StringComparer.Ordinal)
                .Select(group => Graded(group.Key, [.. group.Select(recipe => recipe.Grade).OfType<int>()]));

            var experimentals = recipes
                .Where(recipe => recipe.Kind == BlueprintKind.Experimental)
                .Select(recipe => recipe.Name)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            // " | " because some blueprint names contain a comma.
            text.AppendLine(experimentals.Count > 0
                ? $"- {module.Name}: {string.Join(" | ", blueprints)}; experimentals: {string.Join(" | ", experimentals)}"
                : $"- {module.Name}: {string.Join(" | ", blueprints)}");
        }

        text.AppendLine();
    }

    private static string Graded(string name, IReadOnlyList<int> grades) => grades.Count == 0
        ? name
        : grades.Min() == grades.Max()
            ? $"{name} (grade {grades.Min().ToString(CultureInfo.InvariantCulture)})"
            : $"{name} (grades {grades.Min().ToString(CultureInfo.InvariantCulture)}-{grades.Max().ToString(CultureInfo.InvariantCulture)})";

    private static string Retry(string instruction, string previous, IReadOnlyList<string> refusals)
    {
        var text = new StringBuilder(instruction);

        text.AppendLine();
        text.AppendLine("Your previous answer:");
        text.AppendLine(previous);
        text.AppendLine();
        text.AppendLine("These changes were refused:");

        foreach (var refusal in refusals)
        {
            text.AppendLine($"- {refusal}");
        }

        text.AppendLine();
        text.AppendLine("Answer again with the whole proposal, correcting or leaving out each refused change.");

        return text.ToString();
    }

    // ---- reading and checking the answer ---------------------------------------------------

    private sealed record Answer(BuildRemarkKind Kind, string? Reply, IReadOnlyList<Proposed> Changes);

    private sealed record Proposed(
        string Slot,
        string? Module,
        int? Class,
        string? Rating,
        string? Mount,
        string? Blueprint,
        int Grade,
        string? Experimental,
        string Reason);

    private sealed record Checked(SlotChange? Change, string? Refusal, string? Said = null);

    private static Answer? Read(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var reply = Text(root, "reply");
            var changes = new List<Proposed>();

            if (root.TryGetProperty("changes", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in list.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object || Text(item, "slot") is not { } slot)
                    {
                        continue;
                    }

                    changes.Add(new Proposed(
                        slot,
                        Text(item, "module"),
                        Number(item, "class"),
                        Text(item, "rating"),
                        Text(item, "mount"),
                        Text(item, "blueprint"),
                        Number(item, "grade") ?? 0,
                        Text(item, "experimental"),
                        Text(item, "reason") ?? string.Empty));
                }
            }

            var kind = Text(root, "kind")?.ToLowerInvariant() switch
            {
                "critique" => BuildRemarkKind.Critique,
                "goal" => BuildRemarkKind.Goal,
                "question" => BuildRemarkKind.Question,
                _ => changes.Count > 0 ? BuildRemarkKind.Goal : BuildRemarkKind.Question,
            };

            return new Answer(kind, reply, changes);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text)
        && !string.Equals(text.Trim(), "none", StringComparison.OrdinalIgnoreCase)
            ? text.Trim()
            : null;

    private static int? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;

    /// <summary>Each proposed change against the hull's slots and the blueprint table.</summary>
    private static List<Checked> Check(ShipBuild build, CommanderGameState? state, Answer answer)
    {
        var fitted = Fitted(build, state);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var checkedChanges = new List<Checked>();

        foreach (var proposed in answer.Changes)
        {
            var result = Check(build, fitted, proposed);

            if (result.Change is { } change && !seen.Add(change.Slot))
            {
                result = new Checked(null, $"{change.Slot}: proposed twice; only the first is kept.", result.Said);
            }

            checkedChanges.Add(result);
        }

        return checkedChanges;
    }

    private static Checked Check(ShipBuild build, IReadOnlyList<ShipModule>? fitted, Proposed proposed)
    {
        if (EliteSpecifications.Slot(build.Hull, proposed.Slot) is not { } slot)
        {
            return new Checked(null, $"{proposed.Slot}: not a slot on the {build.HullName}.", proposed.Slot);
        }

        var before = build.For(slot.Name);
        var fits = EliteSpecifications.ModulesFor(slot);
        string? moduleName;
        string? variant;
        IReadOnlyList<ModuleSpecification> candidates;

        if (proposed.Module is { } named)
        {
            var sameName = fits.Where(module => string.Equals(module.Name, named, StringComparison.OrdinalIgnoreCase)).ToList();

            if (sameName.Count == 0)
            {
                return new Checked(null, $"{slot.Name}: {named} does not fit {slot.Describe()} (size {slot.Size.ToString(CultureInfo.InvariantCulture)}{(slot.Restrict.Count > 0 ? ", restricted" : string.Empty)}).", slot.Describe());
            }

            moduleName = sameName[0].Name;
            variant = null;
            candidates = sameName;

            if (proposed.Class is not null || proposed.Rating is not null || proposed.Mount is not null)
            {
                var exact = sameName
                    .Where(module => proposed.Class is null || module.Class == proposed.Class)
                    .Where(module => proposed.Rating is null || string.Equals(module.Rating, proposed.Rating, StringComparison.OrdinalIgnoreCase))
                    .Where(module => proposed.Mount is null || string.Equals(module.Mount, proposed.Mount, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (exact.Count == 0)
                {
                    var size = $"{proposed.Class?.ToString(CultureInfo.InvariantCulture)}{proposed.Rating}".Trim();

                    return new Checked(null, $"{slot.Name}: {size} {named}{(proposed.Mount is { } mount ? $", {mount}" : string.Empty)} does not fit {slot.Describe()} (size {slot.Size.ToString(CultureInfo.InvariantCulture)}).", slot.Describe());
                }

                candidates = exact;
                variant = exact.Count == 1 ? exact[0].Symbol : null;
            }
        }
        else
        {
            moduleName = before?.Module;
            variant = before?.Variant;
            candidates = Planned(before, fits) ?? FittedSpec(fitted, slot.Name) ?? fits;
        }

        var label = moduleName ?? slot.Describe();
        var recipes = candidates.SelectMany(module => BlueprintCatalogue.For(module) ?? []).ToList();

        if (proposed.Grade is < 0 or > 5)
        {
            return new Checked(null, $"{slot.Name}: grade {proposed.Grade.ToString(CultureInfo.InvariantCulture)} is not a grade.", slot.Describe());
        }

        if (proposed.Blueprint is { } blueprint)
        {
            var grades = recipes
                .Where(recipe => recipe.Kind == BlueprintKind.Modification
                                 && string.Equals(recipe.Name, blueprint, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (grades.Count == 0)
            {
                return new Checked(null, $"{slot.Name}: there is no blueprint called {blueprint} for {label}.", slot.Describe());
            }

            if (proposed.Grade > 0 && grades.All(recipe => recipe.Grade != proposed.Grade))
            {
                return new Checked(null, $"{slot.Name}: {blueprint} has no grade {proposed.Grade.ToString(CultureInfo.InvariantCulture)}.", slot.Describe());
            }
        }
        else if (proposed.Grade > 0)
        {
            return new Checked(null, $"{slot.Name}: a grade needs a blueprint.", slot.Describe());
        }

        if (proposed.Experimental is { } experimental
            && !recipes.Any(recipe => recipe.Kind == BlueprintKind.Experimental
                                      && string.Equals(recipe.Name, experimental, StringComparison.OrdinalIgnoreCase)))
        {
            return new Checked(null, $"{slot.Name}: there is no experimental called {experimental} for {label}.", slot.Describe());
        }

        var after = new SlotPlan(
            slot.Name,
            Canonical(recipes, proposed.Blueprint, BlueprintKind.Modification),
            proposed.Grade,
            Canonical(recipes, proposed.Experimental, BlueprintKind.Experimental),
            moduleName)
        {
            Variant = variant,
            Priority = before?.Priority ?? 1,
        };

        return new Checked(new SlotChange(slot.Name, before, after, proposed.Reason), null, slot.Describe());
    }

    /// <summary>The catalogue's own spelling of a name the model gave.</summary>
    private static string? Canonical(IReadOnlyList<Blueprint> recipes, string? name, BlueprintKind kind) =>
        name is null
            ? null
            : recipes.FirstOrDefault(recipe => recipe.Kind == kind && string.Equals(recipe.Name, name, StringComparison.OrdinalIgnoreCase))?.Name ?? name;

    private static IReadOnlyList<ModuleSpecification>? Planned(SlotPlan? plan, IReadOnlyList<ModuleSpecification> fits)
    {
        if (plan?.Variant is { Length: > 0 } variant && EliteSpecifications.Module(variant) is { } exact)
        {
            return [exact];
        }

        if (plan?.Module is { Length: > 0 } module)
        {
            var named = fits.Where(spec => string.Equals(spec.Name, module, StringComparison.OrdinalIgnoreCase)).ToList();

            return named.Count > 0 ? named : null;
        }

        return null;
    }

    private static IReadOnlyList<ModuleSpecification>? FittedSpec(IReadOnlyList<ShipModule>? fitted, string slot) =>
        fitted?.FirstOrDefault(module => string.Equals(module.Slot, slot, StringComparison.OrdinalIgnoreCase)) is { } module
        && EliteSpecifications.Module(module.Item) is { } spec
            ? [spec]
            : null;

    // ---- the three-module limit ------------------------------------------------------------

    private static readonly Lazy<IReadOnlyList<string>> ModuleNames = new(
        () => [.. EliteSpecifications.Modules
            .Where(module => !module.IsBulkhead)
            .Select(module => module.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(name => name.Length)],
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The distinct module names a text mentions.</summary>
    internal static IReadOnlySet<string> ModulesNamed(string text)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rest = text;

        // Longest first, each match removed, so "Shield Generator" inside "Bi-Weave Shield Generator" is not counted twice.
        foreach (var name in ModuleNames.Value)
        {
            var pattern = $@"(?<![\w-]){Regex.Escape(name)}s?(?!\w)";

            if (Regex.IsMatch(rest, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                found.Add(name);
                rest = Regex.Replace(rest, pattern, " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
        }

        return found;
    }

    /// <summary>
    /// The model's reply cut at a sentence so that, with the closing sentences, it names at most <see
    /// cref="MostModulesNamed"/> modules.
    /// </summary>
    private static string? Limit(string? reply, IReadOnlyList<string> tail)
    {
        var closing = string.Join(" ", tail);
        var named = new HashSet<string>(ModulesNamed(closing), StringComparer.OrdinalIgnoreCase);
        var kept = new List<string>();

        foreach (var sentence in Sentences(reply ?? string.Empty))
        {
            var more = new HashSet<string>(named, StringComparer.OrdinalIgnoreCase);
            more.UnionWith(ModulesNamed(sentence));

            if (more.Count > MostModulesNamed)
            {
                break;
            }

            named = more;
            kept.Add(sentence);
        }

        var said = string.Join(" ", kept.Append(closing).Where(part => part.Length > 0));

        return said.Length > 0 ? said : null;
    }

    private static IEnumerable<string> Sentences(string text) =>
        Regex.Split(text.Trim(), @"(?<=[.!?])\s+", RegexOptions.CultureInvariant)
            .Select(sentence => sentence.Trim())
            .Where(sentence => sentence.Length > 0);
}
