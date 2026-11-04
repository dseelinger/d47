using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Core.Stories;

/// <summary>One objective a detour may be: a short title and its trigger, with its place resolved.</summary>
public sealed record DetourStep(string Title, AdventureTrigger Trigger);

/// <summary>
/// What the Commander's words came to: one <see cref="Step"/>, or a question in <see cref="Reply"/> with up to three
/// <see cref="Options"/>. <see cref="Reply"/> is D47's own line, said to the Commander.
/// </summary>
public sealed record DetourAnswer(string Reply, DetourStep? Step, IReadOnlyList<DetourStep> Options)
{
    public bool IsQuestion => Step is null;

    public static DetourAnswer Question(string reply, IReadOnlyList<DetourStep>? options = null) => new(reply, null, options ?? []);
}

/// <summary>
/// The Commander's latest <see cref="Words"/>, the rounds before them, how many questions were already asked, the
/// objective they would leave, and the activities the story may not ask for.
/// </summary>
public sealed record DetourAsk(string Words, IReadOnlyList<AdventureRemark> Exchange, int Questions, AdventureTrigger Current, IReadOnlyList<string> Refused, bool CrimeRefused = false);

/// <summary>
/// Turns the Commander's words about a story objective into one detour step, or a clarifying question, with one small model
/// call and the galaxy search. The step goes through the same place resolution and refused-activity check as a drawn one.
/// </summary>
public sealed class DetourProposer(
    Func<ILlmProvider?> provider,
    Func<string?> model,
    Func<string?> persona,
    Func<CommanderGameState?> state,
    Func<IGalaxyService?> galaxy,
    SpendTracker? spend,
    PriceTable? prices,
    ILogger logger)
{
    /// <summary>Output ceiling for the one call.</summary>
    public const int OutputCeiling = 4000;

    /// <summary>The most clarifying questions asked before the Commander is told what to name.</summary>
    public const int MostQuestions = 2;

    /// <summary>The most options a question offers.</summary>
    public const int MostOptions = 3;

    /// <summary>The reply once <see cref="MostQuestions"/> questions found no usable answer.</summary>
    public const string NamePlaceOrActivity = "Name a place or an activity.";

    /// <summary>The reply when the model's answer could not be read.</summary>
    public const string NotUnderstood = "I did not catch that. Name a place to go or an activity to do.";

    /// <summary>How long the call may run on a provider that is not on this machine.</summary>
    public static readonly TimeSpan CallLimit = TimeSpan.FromSeconds(45);

    /// <summary>The home system the Commander has set; null for none.</summary>
    public Func<string?> HomeSystem { get; set; } = () => null;

    /// <summary>The clock <see cref="CallLimit"/> and the galaxy retry run on.</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;

    private const string SchemaName = "story_detour";

    private static readonly IReadOnlyList<string> Kinds =
    [
        "arrive", "dock",
        .. Enum.GetValues<TriggerKind>()
            .Where(kind => AdventureTrigger.IsCountedKind(kind) && kind != TriggerKind.Event)
            .Select(kind => kind.ToString().ToLowerInvariant()),
    ];

    public async Task<DetourAnswer> ProposeAsync(DetourAsk ask, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ask);

        var game = state();
        var here = game?.Location.StarSystem;
        var home = HomeSystem() is { } set && !string.IsNullOrWhiteSpace(set) ? set.Trim() : null;
        var asking = provider();
        var reply = await FlavourTurn.AskForAsync(
            asking,
            model(),
            persona(),
            aboutMe: null,
            Instruction(ask, here, home, game?.Carrier),
            gameState: null,
            spend,
            prices,
            logger,
            cancellationToken,
            maxOutputTokens: OutputCeiling,
            effort: ThinkingEffort.Low,
            sampling: LlmSampling.Adventure,
            limits: asking is { RunsOnThisMachine: true } ? null : new FlavourLimits(CallLimit, CallLimit) { Time = Time },
            responseSchema: new LlmResponseSchema(SchemaName, Schema())).ConfigureAwait(false);

        var read = reply.Line is { } line && AdventureGenerator.Unfence(line) is { } json ? Read(json) : null;

        if (read is null)
        {
            logger.LogInformation("Detour step not read: {Miss}", reply.Line is null ? reply.Miss.ToString() : "the answer was not JSON");
            return Asked(ask, NotUnderstood, []);
        }

        logger.LogInformation("Detour step answered {Answer}, kind {Kind}, system {System}", read.Question ? "question" : "step", read.Kind ?? "none", read.System ?? "none");

        if (read.Question)
        {
            return Asked(ask, read.Reply ?? NamePlaceOrActivity, await OptionsAsync(read, here, cancellationToken).ConfigureAwait(false));
        }

        return await StepAsync(ask, read, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A question, or <see cref="NamePlaceOrActivity"/> once <see cref="MostQuestions"/> have been asked.</summary>
    private static DetourAnswer Asked(DetourAsk ask, string question, IReadOnlyList<DetourStep> options) =>
        ask.Questions >= MostQuestions ? DetourAnswer.Question(NamePlaceOrActivity) : DetourAnswer.Question(question, options);

    private async Task<DetourAnswer> StepAsync(DetourAsk ask, ReadAnswer read, CancellationToken cancellationToken)
    {
        if (read.Kind is null || !AdventureValidation.TryKind(read.Kind, out var kind) || !Kinds.Contains(read.Kind.Trim().ToLowerInvariant()))
        {
            return Asked(ask, NotUnderstood, []);
        }

        var title = string.IsNullOrWhiteSpace(read.Title) ? null : read.Title.Trim();
        AdventureTrigger trigger;

        if (kind is TriggerKind.Arrive or TriggerKind.Dock)
        {
            if (galaxy() is not { } search)
            {
                return Asked(ask, "Galaxy search is not available, so no place can be checked. Name an activity instead.", []);
            }

            var resolved = await new AdventureResolver(search) { Time = Time }
                .ResolveAsync(kind, read.System, read.Station, null, "The detour", null, cancellationToken).ConfigureAwait(false);

            if (resolved.Trigger is not { } found)
            {
                logger.LogInformation("Detour place not resolved: {Refusal}", resolved.Refusal);
                var named = kind == TriggerKind.Dock && !string.IsNullOrWhiteSpace(read.Station) ? $"{read.Station.Trim()} in {read.System?.Trim() ?? "that system"}" : read.System?.Trim();
                return Asked(ask, named is null ? "Which system do you mean?" : $"I could not find {named}. Which place do you mean?", []);
            }

            trigger = found;
        }
        else
        {
            trigger = new AdventureTrigger
            {
                Kind = kind,
                Count = read.Count,
                Faction = Blank(read.Faction),
                MissionFamily = Blank(read.Mission),
                Commodity = Blank(read.Commodity),
            };

            if (AdventureValidation.CountedProblems("The detour", trigger).FirstOrDefault() is { } problem)
            {
                logger.LogInformation("Detour step refused: {Problem}", problem);
                return Asked(ask, "How many, and of what?", []);
            }
        }

        if (ask.CrimeRefused && CrimeObjectives.Is(trigger))
        {
            return Asked(ask, "You set Crime to 0, so a detour cannot ask you to commit a crime. Name a place, or a different activity.", []);
        }

        if (RefusedActivities.Refuses(ask.Refused, trigger.Kind, trigger.MissionFamily))
        {
            var phrase = RefusedActivities.Key(trigger) is { } key ? RefusedActivities.Phrase(key) : "that";
            return Asked(ask, $"This story no longer asks for {phrase}. Name a place, or a different activity.", []);
        }

        var step = new DetourStep(title ?? Capital(trigger.Describe()), trigger);
        return new DetourAnswer(string.IsNullOrWhiteSpace(read.Reply) ? Capital(trigger.Describe()) + "." : read.Reply.Trim(), step, []);
    }

    /// <summary>
    /// The arrive steps a question offers: the nearest systems of the faction or Power the answer names, otherwise the
    /// systems it lists, each resolved, at most <see cref="MostOptions"/>.
    /// </summary>
    private async Task<IReadOnlyList<DetourStep>> OptionsAsync(ReadAnswer read, string? here, CancellationToken cancellationToken)
    {
        if (galaxy() is not { } search)
        {
            return [];
        }

        var resolver = new AdventureResolver(search) { Time = Time };
        var options = new List<DetourStep>();

        try
        {
            IEnumerable<string> systems = read.Options;

            if (here is not null && Near(read) is { } filter
                && GalaxyQuery.TryParse(here, filter, MostOptions, out var query, out _))
            {
                var nearest = await search.SearchAsync(query, cancellationToken).ConfigureAwait(false);
                systems = nearest.Systems.OrderBy(system => system.Distance ?? double.MaxValue).Select(system => system.Name);
            }

            foreach (var system in systems.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (options.Count == MostOptions)
                {
                    break;
                }

                if ((await resolver.ResolveAsync(TriggerKind.Arrive, system, null, null, "An option", null, cancellationToken).ConfigureAwait(false)).Trigger is { } trigger)
                {
                    options.Add(new DetourStep($"Go to {trigger.System}", trigger));
                }
            }
        }
        catch (GalaxyUnavailableException ex)
        {
            logger.LogWarning("The galaxy search could not find a detour's options: {Message}", ex.Message);
        }

        return options;
    }

    /// <summary>The galaxy search filter for the faction or Power the answer names, or null.</summary>
    private static Dictionary<string, string>? Near(ReadAnswer read)
    {
        if (AdventureValidation.PowerNamed(read.Power) is { } power)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal) { ["power"] = power };
        }

        return Blank(read.Faction) is { } faction
            ? new Dictionary<string, string>(StringComparer.Ordinal) { ["faction"] = faction }
            : null;
    }

    private static string Instruction(DetourAsk ask, string? here, string? home, CarrierState? carrier)
    {
        var text = new StringBuilder();

        text.AppendLine(
            "The Commander is flying a story in Elite Dangerous and wants to step away from its current objective to do one thing "
            + "first. The story waits until that one thing is done. Read what they said and turn it into exactly one objective, or, "
            + "when it is too vague to be one, ask one short question.");
        text.AppendLine();
        text.AppendLine($"The objective they are leaving: {ask.Current.Describe()}.");
        text.AppendLine();
        text.AppendLine("Places they may mean without naming a system:");
        text.AppendLine($"- Where they are now: {here ?? "unknown"}.");
        text.AppendLine(home is null ? "- Home: they have set no home system." : $"- Home: {home}.");

        if (carrier is { Owned: true, StarSystem: { Length: > 0 } carried })
        {
            text.AppendLine($"- Their fleet carrier: in {carried}.");
        }

        text.AppendLine("- Sol: the centre of the bubble, populated space.");
        text.AppendLine("- Colonia: the settled region 22,000 light years from Sol.");

        if (ask.Refused.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Never propose these activities: " + string.Join(", ", ask.Refused.Select(RefusedActivities.Phrase)) + ".");
        }

        if (ask.Exchange.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("What was said so far, oldest first:");

            foreach (var round in ask.Exchange)
            {
                text.AppendLine($"- The Commander: \"{round.Remark}\"");

                if (!string.IsNullOrWhiteSpace(round.Reply))
                {
                    text.AppendLine($"- You: \"{round.Reply}\"");
                }
            }
        }

        text.AppendLine();
        text.AppendLine($"What the Commander says now: \"{ask.Words.Trim()}\"");
        text.AppendLine();
        text.AppendLine(
            "Answer with one JSON object and nothing else. \"answer\" is \"step\" or \"question\". \"reply\" is one short line you say to "
            + "the Commander, as yourself: for a step, what they will do; for a question, the question. It is not a story line.");
        text.AppendLine(
            "For a step: \"title\" is the objective in a few words; \"kind\" is one of " + string.Join(", ", Kinds) + ". "
            + "arrive needs \"system\", spelt as the game spells it. dock needs \"system\" and \"station\". "
            + "Every other kind needs \"count\", and may name \"faction\", \"mission\" (the mission family) or \"commodity\". "
            + "Use only a place they name or one listed above.");
        text.AppendLine(
            "Ask a question when they name neither a place nor an activity, such as \"something closer to home\" with no home set. "
            + "For a question, \"options\" lists up to three systems from the places above that would answer it. When they name a "
            + "minor faction or a Power and you ask which of its systems, put its name in \"faction\" or \"power\" and leave \"options\" "
            + "empty: d47 finds the nearest ones.");
        text.AppendLine("Leave every field that does not apply null, and \"options\" empty.");

        return text.ToString();
    }

    private static string Schema()
    {
        static JsonObject Maybe(string type) => new() { ["type"] = new JsonArray(type, "null") };

        (string Name, JsonNode Schema)[] properties =
        [
            ("answer", new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("step", "question") }),
            ("reply", new JsonObject { ["type"] = "string" }),
            ("title", Maybe("string")),
            ("kind", Maybe("string")),
            ("system", Maybe("string")),
            ("station", Maybe("string")),
            ("count", Maybe("integer")),
            ("faction", Maybe("string")),
            ("mission", Maybe("string")),
            ("commodity", Maybe("string")),
            ("power", Maybe("string")),
            ("options", new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } }),
        ];

        var shape = new JsonObject();

        foreach (var (name, schema) in properties)
        {
            shape[name] = schema;
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = shape,
            ["required"] = new JsonArray([.. properties.Select(property => (JsonNode)JsonValue.Create(property.Name)!)]),
            ["additionalProperties"] = false,
        }.ToJsonString();
    }

    private sealed record ReadAnswer(
        bool Question, string? Reply, string? Title, string? Kind, string? System, string? Station, int? Count,
        string? Faction, string? Mission, string? Commodity, string? Power, IReadOnlyList<string> Options);

    private static ReadAnswer? Read(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var answer = Text(root, "answer");
            var options = root.TryGetProperty("options", out var list) && list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!.Trim()).ToList()
                : [];

            return new ReadAnswer(
                string.Equals(answer, "question", StringComparison.OrdinalIgnoreCase),
                Text(root, "reply"),
                Text(root, "title"),
                Text(root, "kind"),
                Text(root, "system"),
                Text(root, "station"),
                root.TryGetProperty("count", out var count) && count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out var whole) ? whole : null,
                Text(root, "faction"),
                Text(root, "mission"),
                Text(root, "commodity"),
                Text(root, "power"),
                options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string Capital(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], CultureInfo.InvariantCulture) + text[1..];
}
