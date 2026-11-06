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

    /// <summary>"Which ship is best for these roles with this much", which names hulls and proposes nothing.</summary>
    BestShip,
}

/// <summary>One round of a conversation about a plan: what the Commander said and what the core answered.</summary>
public sealed record BuildRemark(string Remark, string? Reply, BuildGoal? Goal = null);

/// <summary>One proposed change to one slot's plan. <see cref="Before"/> is null where the slot had no plan.</summary>
public sealed record SlotChange(string Slot, SlotPlan? Before, SlotPlan After, string Reason);

/// <summary>
/// The answer to one remark about a plan. A proposal is never applied here; <see cref="Cost"/> is the plan
/// with every change applied, computed by <see cref="EngineeringPlan"/>. <see cref="Code"/> names a refusal's cause.
/// <see cref="Goal"/> is the goal read from a goal remark. <see cref="Dropped"/> names the must-haves left out.
/// </summary>
public sealed record BuildAdvice(
    BuildRemarkKind Kind,
    string? Reply,
    IReadOnlyList<SlotChange> Changes,
    PlanCosting? Cost,
    IReadOnlyList<string> Dropped,
    string? Refusal = null,
    string? Code = null,
    BuildGoal? Goal = null)
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

    /// <summary>Output ceiling; adaptive thinking spends from it too.</summary>
    public const int OutputCeiling = 16000;

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
            return new BuildAdvice(BuildRemarkKind.Question, null, [], null, [], "No language model is configured, and advice on a build has to come from one.", "no-model");
        }

        var game = state();
        var first = await AskJsonAsync(Instruction(build, game, exchange, remark), cancellationToken).ConfigureAwait(false);

        if (first is null || Read(first) is not { } answer)
        {
            return new BuildAdvice(BuildRemarkKind.Question, null, [], null, [], "The model's answer was not one I could read. Try again.", "unreadable");
        }

        if (answer.Kind == BuildRemarkKind.BestShip)
        {
            return await BestShipAsync(answer, exchange, game, cancellationToken).ConfigureAwait(false);
        }

        if (answer.Kind != BuildRemarkKind.Goal)
        {
            return new BuildAdvice(answer.Kind, Limit(answer.Reply, []), [], null, []);
        }

        var read = answer.Goal is { } said ? Check(said, Held(exchange)) : null;

        if (read is null)
        {
            return new BuildAdvice(BuildRemarkKind.Question, Limit(answer.Ask ?? AskHowMuch, []), [], null, []);
        }

        var credits = read.Goal.Credits ?? game?.Session.Balance;
        var searched = read.Goal with { Credits = credits };
        var result = BuildSearch.Find(build, FittedLoadout(build, game), searched, cancellationToken);

        var tail = new List<string>();

        if (read.Dropped.Count > 0)
        {
            tail.Add($"I left out {And(read.Dropped)}, which {(read.Dropped.Count == 1 ? "is" : "are")} not in my module table.");
        }

        if (credits is null)
        {
            tail.Add("There is no spending limit, because you named no amount and your balance is not known.");
        }

        if (result.Changes.Count > MostModulesNamed)
        {
            tail.Add($"{result.Changes.Count.ToString(CultureInfo.InvariantCulture)} slots change; they are on the ship's page.");
        }

        var reply = await AskJsonAsync(Explanation(build, remark, searched, result), cancellationToken).ConfigureAwait(false);
        var explained = reply is null ? null : Read(reply)?.Reply;

        return new BuildAdvice(
            BuildRemarkKind.Goal,
            Limit(explained ?? Plainly(result), tail),
            result.Changes,
            Cost(result.Plan, game),
            read.Dropped,
            Goal: read.Goal);
    }

    private static async Task<BuildAdvice> BestShipAsync(
        Answer answer,
        IReadOnlyList<BuildRemark> exchange,
        CommanderGameState? game,
        CancellationToken cancellationToken)
    {
        var read = answer.Goal is { Weights.Count: > 0 } said ? Check(said with { Mode = GoalMode.Weighted }, null) : null;

        if (read is null)
        {
            return new BuildAdvice(BuildRemarkKind.Question, Limit(answer.Ask ?? AskWhichRoles, []), [], null, []);
        }

        var credits = read.Goal.Credits ?? game?.Session.Balance;
        var ranks = game?.Ranks ?? RankState.Empty;
        var owned = BestShip.Owned(game);

        var result = await Task.Run(
            () => BestShip.Choose(EliteSpecifications.Ships, read.Goal, ranks, owned, credits, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        var tail = new List<string>();

        if (read.Dropped.Count > 0)
        {
            tail.Add($"I left out {And(read.Dropped)}, which {(read.Dropped.Count == 1 ? "is" : "are")} not in my module table.");
        }

        return new BuildAdvice(
            BuildRemarkKind.BestShip,
            string.Join(" ", new[] { BestShip.Say(result, read.Goal, credits, game?.Session.Balance is not null) }.Concat(tail)),
            [],
            null,
            read.Dropped);
    }

    private const string AskWhichRoles =
        "Which of combat, exploration and cargo is the ship for, and how much of each?";

    private static string And(IReadOnlyList<string> items) => items.Count switch
    {
        1 => items[0],
        _ => $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}",
    };

    private const string AskHowMuch =
        "How far should I take that: maximise it at the expense of everything else, maximise it without giving up the rest, or any improvement at all?";

    /// <summary>The reply when the second call gives none.</summary>
    private static string Plainly(BuildResult result) => result.Changes.Count == 0
        ? "I found nothing that improves on the plan as it stands."
        : $"I found {result.Changes.Count.ToString(CultureInfo.InvariantCulture)} {(result.Changes.Count == 1 ? "change" : "changes")} for that goal.";

    /// <summary>What <see cref="EngineeringPlan"/> says a build costs.</summary>
    public static PlanCosting Cost(ShipBuild build, CommanderGameState? state)
    {
        ArgumentNullException.ThrowIfNull(build);

        var planned = build.Slots
            .Where(slot => slot.Blueprint is not null || slot.Grade > 0 || slot.Experimental is not null)
            .Select(slot => slot.ToRequest())
            .ToList();

        return EngineeringPlan.Cost(
            EngineeringPlan.Items(build.Scope, build.Hull, planned),
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
            maxOutputTokens: OutputCeiling,
            effort: ThinkingEffort.High,
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
            + "good the plan is), \"goal\" (something they want the ship to do better), or \"best_ship\" (which hull suits some roles and a budget).");
        text.AppendLine();
        text.AppendLine("Rules:");
        text.AppendLine("- " + Stories.PlainWords.Rule);
        text.AppendLine("- A question or a critique is answered in the reply.");
        text.AppendLine(
            "- A goal is read, not answered: d47's own search finds the modules. Give the goal below. Never name a "
            + "slot, a module class, a rating, a blueprint or a grade. Name a module only in mustHaves, and only one the "
            + "Commander said.");
        text.AppendLine(
            "- A remark asking which ship, or which hull, is best for some roles with some money is \"best_ship\": give the "
            + "roles as weights, the profile, floors, mustHaves, credits and engineering, with no mode and no target. If it "
            + "names no role, give \"ask\" and no goal: one question asking which roles. d47 names the hulls.");
        text.AppendLine(
            "- If a goal remark does not say how far to take it, give \"ask\" and no goal: one question asking whether "
            + "they mean to maximise it at the expense of everything else, maximise it without giving up the rest, or "
            + "any improvement at all.");
        text.AppendLine(
            "- When a goal is already held below, a remark such as \"a bit more cargo\" changes it: give the whole "
            + "goal again with that change made.");
        text.AppendLine(
            "- Materials, caps and engineer ranks are given below and are not yours to work out. Do not "
            + "quote material counts.");
        text.AppendLine(
            $"- The reply is spoken aloud. It names at most {MostModulesNamed.ToString(CultureInfo.InvariantCulture)} "
            + "modules and is a few sentences long.");
        text.AppendLine(
            "- Quote the figures under \"What the plan does\" and do not work out others: jump range, power, "
            + "shields, hull, resistances, damage and cargo.");
        text.AppendLine("- Nothing is applied until the Commander accepts it.");
        text.AppendLine();

        Hull(text, build, state);
        Costing(text, Cost(build, state));
        Figures(text, build, state);

        if (Held(exchange) is { } held)
        {
            text.AppendLine("The goal held from this conversation:");
            text.AppendLine(WriteGoal(held));
            text.AppendLine();
        }

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
        text.AppendLine("Answer with one JSON object and nothing else. For a question or a critique:");
        text.AppendLine("{\"kind\":\"question|critique\",\"reply\":\"...\"}");
        text.AppendLine("For a goal, with every value spelt as shown:");
        text.AppendLine(
            "{\"kind\":\"goal\",\"goal\":{\"mode\":\"only|keep_rest|any_gain|weighted\","
            + "\"target\":\"combat|exploration|cargo\",\"weights\":{\"combat\":0.5,\"exploration\":0.5,\"cargo\":0},"
            + "\"profile\":\"bounty|conflict_zone|thargoid\","
            + "\"floors\":[{\"figure\":\"laden_jump|unladen_jump|cargo|shields|hull|sustained_dps\",\"atLeast\":40}],"
            + "\"mustHaves\":[\"Fuel Scoop\"],\"credits\":null,\"engineering\":false}}");
        text.AppendLine(
            "mode: only = the target alone; keep_rest = the target without lowering any other figure; any_gain = the "
            + "fewest changes that raise the target at all; weighted = every role by its weight. target is required "
            + "unless mode is weighted. credits is an amount the Commander named, else null.");
        text.AppendLine(
            "For which ship is best: {\"kind\":\"best_ship\",\"goal\":{\"weights\":{\"combat\":0.5,\"exploration\":0.5,\"cargo\":0},"
            + "\"profile\":\"bounty\",\"floors\":[],\"mustHaves\":[],\"credits\":null,\"engineering\":false}}");
        text.AppendLine("For a goal whose extent is not said: {\"kind\":\"goal\",\"ask\":\"the question\"}");

        return text.ToString();
    }

    /// <summary>The second call: the reply, written from the search's result.</summary>
    private static string Explanation(ShipBuild build, string remark, BuildGoal goal, BuildResult result)
    {
        var text = new StringBuilder();

        text.AppendLine(
            "d47 searched for a build that meets the Commander's goal. Write the reply that tells them what it found. "
            + "You do not choose or change any module.");
        text.AppendLine();
        text.AppendLine("Rules:");
        text.AppendLine("- " + Stories.PlainWords.Rule);
        text.AppendLine(
            $"- The reply is spoken aloud. It names at most {MostModulesNamed.ToString(CultureInfo.InvariantCulture)} "
            + "modules and is a few sentences long.");
        text.AppendLine("- Quote the figures below and do not work out others.");
        text.AppendLine("- Nothing is applied until the Commander accepts it.");
        text.AppendLine();
        text.AppendLine($"Ship: {build.Describe()}.");
        text.AppendLine($"The Commander said: {remark}");
        text.AppendLine($"The goal as read: {WriteGoal(goal)}");
        text.AppendLine();
        text.AppendLine("Figures before (the plan as it stands):");

        foreach (var line in FigureLines(result.StartGauges))
        {
            text.AppendLine("- " + line);
        }

        text.AppendLine("Figures after (the plan with every change):");

        foreach (var line in FigureLines(result.Gauges))
        {
            text.AppendLine("- " + line);
        }

        text.AppendLine();
        text.AppendLine("Changes:");

        if (result.Changes.Count == 0)
        {
            text.AppendLine("- none");
        }

        foreach (var change in result.Changes)
        {
            text.AppendLine($"- {change.Slot}: {change.After.Describe()}" + (change.Reason.Length > 0 ? $" ({change.Reason})" : string.Empty));
        }

        text.AppendLine($"Cost of the changed modules: {result.Credits.ToString("N0", CultureInfo.InvariantCulture)} credits.");

        if (result.Unmet is { } unmet)
        {
            text.AppendLine($"A floor is not met: {unmet.Figure} of at least {unmet.AtLeast.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (result.Missing.Count > 0)
        {
            text.AppendLine($"Must-haves that did not fit: {string.Join(", ", result.Missing)}.");
        }

        text.AppendLine();
        text.AppendLine("Answer with one JSON object and nothing else: {\"reply\":\"...\"}");

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
    private static IReadOnlyList<ShipModule>? Fitted(ShipBuild build, CommanderGameState? state) =>
        FittedLoadout(build, state)?.Modules;

    private static ShipLoadout? FittedLoadout(ShipBuild build, CommanderGameState? state)
    {
        if (build.ShipId is not { } shipId || state is null)
        {
            return null;
        }

        if (state.Ship is { IsKnown: true } live && live.ShipId == shipId)
        {
            return live;
        }

        return state.Loadouts.For(shipId)?.Loadout;
    }

    /// <summary>The figures d47 computes for the plan, and for what is fitted where the two differ.</summary>
    private static void Figures(StringBuilder text, ShipBuild build, CommanderGameState? state)
    {
        text.AppendLine("What the plan does (computed by d47; measured means read off the game, modelled means worked out from the plan):");

        var loadout = FittedLoadout(build, state);
        var planned = FigureLines(ShipGauges.Read(build, loadout));

        if (planned.Count == 0)
        {
            text.AppendLine("- no figures: nothing in the plan or the ship can be totalled");
            text.AppendLine();
            return;
        }

        if (loadout is null)
        {
            foreach (var line in planned)
            {
                text.AppendLine("- " + line);
            }

            text.AppendLine();
            return;
        }

        var fitted = FigureLines(ShipGauges.Read(new ShipBuild(build.CommanderFid, build.Id, build.Hull, build.ShipId), loadout));

        if (planned.SequenceEqual(fitted))
        {
            foreach (var line in planned)
            {
                text.AppendLine("- " + line);
            }
        }
        else
        {
            text.AppendLine("As planned:");

            foreach (var line in planned)
            {
                text.AppendLine("- " + line);
            }

            text.AppendLine("As fitted now:");

            foreach (var line in fitted)
            {
                text.AppendLine("- " + line);
            }
        }

        text.AppendLine();
    }

    private static List<string> FigureLines(BuildGauges gauges)
    {
        var lines = new List<string>();

        string N(double value, string format = "0.##") => value.ToString(format, CultureInfo.InvariantCulture);
        string Percent(double value) => N(value * 100, "0.#") + "%";
        string Set(ResistanceSet set) =>
            $"kinetic {Percent(set.Kinetic)}, thermal {Percent(set.Thermal)}, explosive {Percent(set.Explosive)}, caustic {Percent(set.Caustic)}";

        if (gauges.Jump is { } jump)
        {
            lines.Add(
                $"jump range ({jump.Kind.ToString().ToLowerInvariant()}): {N(jump.Best)} ly with one jump's fuel and no cargo, "
                + $"{N(jump.Middle)} ly with a full tank, {N(jump.Worst)} ly with a full tank and a full hold");
        }

        if (gauges.Power is { } power)
        {
            var capacity = power.Capacity is { } made ? $"{N(made)} MW capacity" : "no power plant seen";

            lines.Add(
                $"power ({power.Kind.ToString().ToLowerInvariant()}): {N(power.Deployed)} MW deployed, {N(power.Retracted)} MW retracted, {capacity}"
                + (power.Overage is { } over ? $"; over by {N(over)} MW" : string.Empty));
        }

        if (gauges.Combat is { } combat)
        {
            var kind = combat.Kind.ToString().ToLowerInvariant();

            if (combat.Shields is { } shields)
            {
                lines.Add($"shields ({kind}): {N(shields)} MJ"
                    + (combat.ShieldResistance is { } shieldSet ? $"; damage resisted: {Set(shieldSet)}" : string.Empty));
            }

            if (combat.Hull is { } hull)
            {
                lines.Add($"hull ({kind}): {N(hull)}"
                    + (combat.HullResistance is { } hullSet ? $"; damage resisted: {Set(hullSet)}" : string.Empty));
            }

            lines.Add($"damage ({kind}): {N(combat.RawDps)} per second raw, {N(combat.SustainedDps)} sustained");
            lines.Add($"cargo ({kind}): {N(combat.Cargo)} t");
        }

        return lines;
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

    // ---- reading and checking the answer ---------------------------------------------------

    private sealed record Answer(BuildRemarkKind Kind, string? Reply, string? Ask, GoalRead? Goal);

    private sealed record GoalRead(
        GoalMode? Mode,
        BuildRole? Target,
        Dictionary<BuildRole, double> Weights,
        CombatProfile? Profile,
        List<BuildFloor> Floors,
        List<string> MustHaves,
        long? Credits,
        bool? Engineering);

    private sealed record Checked(BuildGoal Goal, IReadOnlyList<string> Dropped);

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

            var goal = root.TryGetProperty("goal", out var element) && element.ValueKind == JsonValueKind.Object
                ? ReadGoal(element)
                : null;

            var kind = Text(root, "kind")?.ToLowerInvariant() switch
            {
                "critique" => BuildRemarkKind.Critique,
                "goal" => BuildRemarkKind.Goal,
                "best-ship" or "best_ship" or "bestship" => BuildRemarkKind.BestShip,
                "question" => BuildRemarkKind.Question,
                _ => goal is not null ? BuildRemarkKind.Goal : BuildRemarkKind.Question,
            };

            return new Answer(kind, Text(root, "reply"), Text(root, "ask"), goal);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static GoalRead ReadGoal(JsonElement element)
    {
        var weights = new Dictionary<BuildRole, double>();

        if (element.TryGetProperty("weights", out var shares) && shares.ValueKind == JsonValueKind.Object)
        {
            foreach (var share in shares.EnumerateObject())
            {
                if (Closed<BuildRole>(share.Name) is { } role
                    && share.Value.ValueKind == JsonValueKind.Number
                    && share.Value.TryGetDouble(out var weight)
                    && double.IsFinite(weight)
                    && weight > 0)
                {
                    weights[role] = weight;
                }
            }
        }

        var floors = new List<BuildFloor>();

        if (element.TryGetProperty("floors", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object
                    && Closed<BuildFigure>(Text(item, "figure")) is { } figure
                    && item.TryGetProperty("atLeast", out var at)
                    && at.ValueKind == JsonValueKind.Number
                    && at.TryGetDouble(out var value)
                    && double.IsFinite(value)
                    && value > 0)
                {
                    floors.Add(new BuildFloor(figure, value));
                }
            }
        }

        var mustHaves = new List<string>();

        if (element.TryGetProperty("mustHaves", out var wanted) && wanted.ValueKind == JsonValueKind.Array)
        {
            mustHaves.AddRange(wanted.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                .Select(item => item.GetString()!.Trim()));
        }

        long? credits = element.TryGetProperty("credits", out var money)
            && money.ValueKind == JsonValueKind.Number
            && money.TryGetInt64(out var amount)
            && amount > 0
                ? amount
                : null;

        bool? engineering = element.TryGetProperty("engineering", out var engineered)
            && engineered.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? engineered.GetBoolean()
                : null;

        return new GoalRead(
            Closed<GoalMode>(Text(element, "mode")),
            Closed<BuildRole>(Text(element, "target")),
            weights,
            Closed<CombatProfile>(Text(element, "profile")),
            floors,
            mustHaves,
            credits,
            engineering);
    }

    /// <summary>The member of a closed list a name spells, ignoring case, spaces, hyphens and underscores.</summary>
    private static T? Closed<T>(string? name)
        where T : struct, Enum
    {
        if (name is null)
        {
            return null;
        }

        var key = Squash(name);

        foreach (var value in Enum.GetValues<T>())
        {
            if (Squash(value.ToString()) == key)
            {
                return value;
            }
        }

        return null;
    }

    private static string Squash(string text) =>
        new([.. text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);

    /// <summary>
    /// A goal the search can run, or null where the extent is not said. What the remark did not give comes from
    /// the goal held before it. A must-have is kept only where <see cref="EliteSpecifications"/> names that module.
    /// </summary>
    private static Checked? Check(GoalRead read, BuildGoal? held)
    {
        var mode = read.Mode ?? held?.Mode;
        var target = read.Target ?? held?.Target;
        var weights = read.Weights.Count > 0
            ? read.Weights
            : held is null ? [] : new Dictionary<BuildRole, double>(held.Weights);

        if (mode is null
            || (mode == GoalMode.Weighted && weights.Count == 0)
            || (mode != GoalMode.Weighted && target is null))
        {
            return null;
        }

        var names = new List<string>();
        var dropped = new List<string>();

        foreach (var wanted in read.MustHaves.Count > 0 || held is null ? read.MustHaves : held.MustHaves)
        {
            var known = EliteSpecifications.Modules
                .FirstOrDefault(module => !module.IsBulkhead && string.Equals(module.Name, wanted, StringComparison.OrdinalIgnoreCase))
                ?.Name;

            if (known is null)
            {
                if (!dropped.Contains(wanted, StringComparer.OrdinalIgnoreCase))
                {
                    dropped.Add(wanted);
                }
            }
            else if (!names.Contains(known, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(known);
            }
        }

        var goal = new BuildGoal(
            weights,
            read.Profile ?? held?.Combat ?? CombatProfile.Bounty,
            read.Floors.Count > 0 ? read.Floors : held?.Floors ?? [],
            names,
            read.Credits ?? held?.Credits,
            read.Engineering ?? held?.Engineering ?? false,
            mode.Value,
            target);

        return new Checked(goal, dropped);
    }

    private static BuildGoal? Held(IReadOnlyList<BuildRemark> exchange) =>
        exchange.LastOrDefault(round => round.Goal is not null)?.Goal;

    private static string WriteGoal(BuildGoal goal)
    {
        static string Snake(Enum value) =>
            string.Concat(value.ToString().Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

        return JsonSerializer.Serialize(new
        {
            mode = Snake(goal.Mode),
            target = goal.Target is { } target ? Snake(target) : null,
            weights = goal.Weights.ToDictionary(pair => Snake(pair.Key), pair => pair.Value),
            profile = Snake(goal.Combat),
            floors = goal.Floors.Select(floor => new { figure = Snake(floor.Figure), atLeast = floor.AtLeast }),
            mustHaves = goal.MustHaves,
            credits = goal.Credits,
            engineering = goal.Engineering,
        });
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text)
        && !string.Equals(text.Trim(), "none", StringComparison.OrdinalIgnoreCase)
            ? text.Trim()
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
