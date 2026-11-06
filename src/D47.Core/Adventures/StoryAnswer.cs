using System.Text.Json.Nodes;
using D47.Core.Conversation;

namespace D47.Core.Adventures;

/// <summary>A story call's instruction text and the JSON schema its answer must match.</summary>
internal sealed record StoryCall(string Text, LlmResponseSchema Schema);

/// <summary>
/// The cast ids a story answer may board and leave at an objective, and whether the opening may carry them too; a direction
/// with no ids has no field.
/// </summary>
internal sealed record StorySwitches(IReadOnlyList<string> Boarding, IReadOnlyList<string> Leaving, bool Opening);

/// <summary>The fields and kinds a story answer has, written once for both the instruction's JSON line and the schema.</summary>
internal static class StoryAnswer
{
    internal const string SpineName = "story_spine";
    internal const string BeatsName = "story_beats";
    internal const string ReplacementsName = "story_replacements";
    internal const string FidelityName = "story_fidelity";
    internal const string HeadingName = "story_heading";
    internal const string PlainName = "story_plain";
    internal const string LinesName = "story_lines";
    internal const string LineRepairsName = "story_line_repairs";
    internal const string BriefName = "story_brief";

    internal static readonly IReadOnlyList<string> SpineFields = ["name", "premise", "want", "stake", "turn", "ending"];

    /// <summary>Kinds offered in every story.</summary>
    private static readonly string[] AlwaysKinds =
    [
        "arrive", "dock", "land", "scan", "rank", "bounty", "bond", "onfootbond", "mission", "sell", "mine", "onfoot", "collect",
        "organic", "map", "signal", "wreck", "codex", "datasale", "salvage", "uss", "rescue", "engineer", "srv", "boost", "discover", "footfall", "contribute", "claim", "unlock", "crew",
        "suitmod", "conflict", "faction",
    ];

    /// <summary>The kinds a drawn chapter offers only for the activities drawn; the others stand for places and milestones.</summary>
    private static readonly HashSet<string> DrawnKinds = new(StringComparer.Ordinal)
    {
        "bounty", "bond", "onfootbond", "mission", "sell", "mine", "onfoot", "collect", "organic", "map", "signal", "wreck", "codex", "datasale",
        "salvage", "uss", "rescue", "srv", "boost", "discover", "footfall", "contribute", "suitmod", "conflict", "faction",
    };

    internal const string EventKind = "event";

    /// <summary>The kinds offered only to a chapter that drew them.</summary>
    internal static readonly IReadOnlySet<string> PictureKinds = new HashSet<string>(StringComparer.Ordinal) { "codexpicture", "newcodexpicture" };

    private enum Shape
    {
        Text,
        MaybeText,
        MaybeWhole,
        MaybeFlag,
    }

    private static readonly (string Name, Shape Shape)[] BeatFields =
    [
        ("reason", Shape.MaybeText), ("system", Shape.MaybeText), ("station", Shape.MaybeText), ("body", Shape.MaybeText),
        ("career", Shape.MaybeText), ("rank", Shape.MaybeWhole), ("ship", Shape.MaybeText), ("count", Shape.MaybeWhole),
        ("faction", Shape.MaybeText), ("mission", Shape.MaybeText), ("commodity", Shape.MaybeText), ("filter", Shape.MaybeText),
        ("organic", Shape.MaybeFlag), ("engineer", Shape.MaybeText), ("stage", Shape.MaybeText),
    ];

    /// <summary>The beat fields, with <c>task</c> only where <c>event</c> is a kind.</summary>
    private static IEnumerable<(string Name, Shape Shape)> FieldsFor(IReadOnlyList<string> kinds) =>
        kinds.Contains(EventKind) ? [.. BeatFields, ("task", Shape.MaybeText)] : BeatFields;

    /// <summary>
    /// The kinds one beats call offers: those always offered, board when a hull can be named, the team kinds, then beacon and reach when the story has them, and site when sites are listed.
    /// A drawn chapter offers, of the activity kinds, only <paramref name="drawn"/>.
    /// </summary>
    internal static IReadOnlyList<string> Kinds(IEnumerable<TriggerKind> team, bool beacon, bool reach, bool board, bool site, IReadOnlyCollection<TriggerKind>? drawn = null)
    {
        var kinds = new List<string>(AlwaysKinds);
        kinds.InsertRange(AlwaysKinds.Length - 2, team.Select(kind => kind.ToString().ToLowerInvariant()));

        if (board)
        {
            kinds.Insert(kinds.IndexOf("rank") + 1, "board");
        }

        if (beacon)
        {
            kinds.Add("beacon");
        }

        if (reach)
        {
            kinds.Add("reach");
        }

        if (site)
        {
            kinds.Add("site");
        }

        if (drawn is not null)
        {
            var names = drawn.Select(kind => kind.ToString().ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);

            kinds.RemoveAll(kind => DrawnKinds.Contains(kind) && !names.Contains(kind));

            if (names.Contains(EventKind))
            {
                kinds.Add(EventKind);
            }

            kinds.AddRange(PictureKinds.Where(names.Contains));
        }

        return kinds;
    }

    internal static LlmResponseSchema SpineSchema() =>
        new(SpineName, Object(SpineFields.Select(field => (field, (JsonNode)Text()))).ToJsonString());

    /// <summary>What an objection may say a quoted statement is: the three checked kinds, then the story's own fiction.</summary>
    internal static readonly IReadOnlyList<string> StatementKinds = ["action", "history", "event", "fiction"];

    internal const string FidelityShape =
        "{\"objections\": [{\"objective\": number, \"quote\": string, \"kind\": \"action\" | \"history\" | \"event\" | \"fiction\", \"reason\": string}]}";

    internal static LlmResponseSchema FidelitySchema() =>
        new(
            FidelityName,
            Object([("objections", new JsonObject
            {
                ["type"] = "array",
                ["items"] = Object([
                    ("objective", new JsonObject { ["type"] = "integer" }),
                    ("quote", Text()),
                    ("kind", new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. StatementKinds.Select(kind => (JsonNode)JsonValue.Create(kind)!)]) }),
                    ("reason", Text()),
                ]),
            })]).ToJsonString());

    /// <summary>What a plain-wording objection may call a quoted phrase.</summary>
    internal static readonly IReadOnlyList<string> PlainKinds = ["figurative", "invented"];

    internal const string PlainShape =
        "{\"objections\": [{\"part\": string, \"quote\": string, \"kind\": \"figurative\" | \"invented\", \"reason\": string, \"plain\": string}]}";

    internal static LlmResponseSchema PlainSchema() =>
        new(
            PlainName,
            Object([("objections", new JsonObject
            {
                ["type"] = "array",
                ["items"] = Object([
                    ("part", Text()),
                    ("quote", Text()),
                    ("kind", new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. PlainKinds.Select(kind => (JsonNode)JsonValue.Create(kind)!)]) }),
                    ("reason", Text()),
                    ("plain", Text()),
                ]),
            })]).ToJsonString());

    /// <summary>The answer's JSON shape for the step that reads a changed objective's words, with <paramref name="activities"/> the ids it may name.</summary>
    internal static string HeadingShape(IReadOnlyList<string> activities) =>
        "{\"home\": boolean, \"system\": string|null, \"activity\": " + string.Join("|", activities.Select(id => $"\"{id}\"")) + "|null, \"count\": number|null, \"reply\": string}";

    /// <summary>
    /// The schema of the step that reads a changed objective's words into constraints: whether they ask to go home, the system they
    /// name, one of <paramref name="activities"/>, the count they said, and the reply.
    /// </summary>
    internal static LlmResponseSchema HeadingSchema(IReadOnlyList<string> activities) =>
        new(HeadingName, Object(
        [
            ("home", new JsonObject { ["type"] = "boolean" }),
            ("system", Maybe("string")),
            ("activity", new JsonObject { ["type"] = new JsonArray("string", "null"), ["enum"] = new JsonArray([.. activities.Select(id => (JsonNode)JsonValue.Create(id)!), null]) }),
            ("count", Maybe("integer")),
            ("reply", Text()),
        ]).ToJsonString());

    /// <summary>The brief step's answer as the instruction states it; <paramref name="revising"/> adds the "changes" flag.</summary>
    internal static string BriefShape(IReadOnlyList<string> activities, IReadOnlyList<string> ships, bool revising) =>
        "{\"home\": boolean, \"system\": string|null, \"activities\": [{\"activity\": " + string.Join("|", activities.Select(id => $"\"{id}\""))
        + ", \"count\": number|null}], \"ship\": " + string.Join("|", ships.Select(ship => $"\"{ship}\"")) + "|null"
        + (revising ? ", \"leaveOut\": [" + string.Join("|", activities.Select(id => $"\"{id}\"")) + "], \"changes\": boolean" : string.Empty) + "}";

    /// <summary>
    /// The schema of the step that reads a short story's brief, or the Commander's remark on a draft, into constraints: whether they
    /// ask to go home, the system they name, the <paramref name="activities"/> they ask for with any count said, the one of
    /// <paramref name="ships"/> they name, and with <paramref name="revising"/> the activities they ask to drop and whether the latest
    /// remark changes any of them.
    /// </summary>
    internal static LlmResponseSchema BriefSchema(IReadOnlyList<string> activities, IReadOnlyList<string> ships, bool revising)
    {
        var answer = new List<(string, JsonNode)>
        {
            ("home", new JsonObject { ["type"] = "boolean" }),
            ("system", Maybe("string")),
            ("activities", new JsonObject
            {
                ["type"] = "array",
                ["items"] = Object(
                [
                    ("activity", new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. activities.Select(id => (JsonNode)JsonValue.Create(id)!)]) }),
                    ("count", Maybe("integer")),
                ]),
            }),
            ("ship", new JsonObject { ["type"] = new JsonArray("string", "null"), ["enum"] = new JsonArray([.. ships.Select(ship => (JsonNode)JsonValue.Create(ship)!), null]) }),
        };

        if (revising)
        {
            answer.Add(("leaveOut", new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. activities.Select(id => (JsonNode)JsonValue.Create(id)!)]) },
            }));
            answer.Add(("changes", new JsonObject { ["type"] = "boolean" }));
        }

        return new LlmResponseSchema(BriefName, Object(answer).ToJsonString());
    }

    /// <summary>The answer's JSON shape as the instruction states it; <paramref name="heading"/> adds a changed objective's "heading" flag.</summary>
    internal static string BeatsShape(
        bool speakers, IReadOnlyList<string> kinds, bool destination, IReadOnlyList<string>? ships = null, bool aboard = false, StorySwitches? switches = null, bool heading = false) =>
        "{" + string.Join(", ", SpineFields.Select(field => $"\"{field}\": string"))
        + ", \"opening\": " + OpeningShape(speakers)
        + ", \"reply\": string, \"beats\": [" + BeatShape(speakers, kinds, ships, objective: false, switches) + "]"
        + (destination ? ", " + DestinationShape : string.Empty)
        + (aboard ? ", \"aboard\": [string]" : string.Empty)
        + (switches is { Opening: true } ? SwitchesShape(switches, "openingBoards", "openingLeaves") : string.Empty)
        + (heading ? ", \"heading\": boolean" : string.Empty) + "}";

    /// <summary>A repair answer's JSON shape: the replacements, and the opening and destination only when they are asked for again.</summary>
    internal static string ReplacementsShape(
        bool speakers, IReadOnlyList<string> kinds, bool opening, bool destination, IReadOnlyList<string>? ships = null, StorySwitches? switches = null) =>
        "{\"replacements\": [" + BeatShape(speakers, kinds, ships, objective: true, switches) + "]"
        + (opening ? ", \"opening\": " + OpeningShape(speakers) : string.Empty)
        + (destination ? ", " + DestinationShape : string.Empty) + "}";

    private static string SwitchesShape(StorySwitches? switches, string boards, string leaves) =>
        (switches?.Boarding is { Count: > 0 } boarding ? $", \"{boards}\": [{Ids(boarding)}]" : string.Empty)
        + (switches?.Leaving is { Count: > 0 } leaving ? $", \"{leaves}\": [{Ids(leaving)}]" : string.Empty);

    private static string Ids(IReadOnlyList<string> ids) => string.Join("|", ids.Select(id => $"\"{id}\""));

    private const string DestinationShape = "\"destination\": {\"system\": string, \"body\": string}";

    private static string OpeningShape(bool speakers) => speakers ? "[{\"text\": string, \"speaker\": string}]" : "string";

    private static string BeatShape(bool speakers, IReadOnlyList<string> kinds, IReadOnlyList<string>? ships, bool objective, StorySwitches? switches) =>
        "{" + (objective ? "\"objective\": number, " : string.Empty) + "\"title\": string, \"function\": string, \"kind\": "
        + string.Join("|", kinds.Select(kind => $"\"{kind}\""))
        + string.Concat(FieldsFor(kinds).Select(field => $", \"{field.Name}\": {(field.Name == "ship" && ships is not null ? string.Join("|", ships.Select(ship => $"\"{ship}\"")) + "|null" : Prose(field.Shape))}"))
        + ", " + (speakers ? "\"lines\": [{\"text\": string, \"speaker\": string}]" : "\"line\": string")
        + SwitchesShape(switches, "boards", "leaves")
        + (objective ? string.Empty : ", \"then\": [" + StepShape(kinds, ships) + "]") + "}";

    private static string StepShape(IReadOnlyList<string> kinds, IReadOnlyList<string>? ships) =>
        "{\"kind\": " + string.Join("|", kinds.Select(kind => $"\"{kind}\""))
        + string.Concat(FieldsFor(kinds).Select(field => $", \"{field.Name}\": {(field.Name == "ship" && ships is not null ? string.Join("|", ships.Select(ship => $"\"{ship}\"")) + "|null" : Prose(field.Shape))}"))
        + "}";

    /// <summary>
    /// The schema of a chapter's answer; a <paramref name="cast"/> with any id adds the "aboard" list of them,
    /// <paramref name="switches"/> the boardings and leavings due, and <paramref name="heading"/> a changed objective's "heading" flag.
    /// </summary>
    internal static LlmResponseSchema BeatsSchema(
        IReadOnlyList<string>? speakers,
        IReadOnlyList<string> kinds,
        bool destination,
        IReadOnlyList<string>? ships = null,
        IReadOnlyList<string>? cast = null,
        StorySwitches? switches = null,
        bool heading = false)
    {
        var answer = new List<(string, JsonNode)>(SpineFields.Select(field => (field, (JsonNode)Text())))
        {
            ("opening", speakers is not null ? Lines(speakers) : Text()),
            ("reply", Text()),
            ("beats", new JsonObject { ["type"] = "array", ["items"] = Object([.. Beat(speakers, kinds, ships, switches), ("then", new JsonObject { ["type"] = "array", ["items"] = Object(Step(kinds, ships)) })]) }),
        };

        if (destination)
        {
            answer.Add(("destination", Destination()));
        }

        if (cast is { Count: > 0 })
        {
            answer.Add(("aboard", IdList(cast)));
        }

        if (switches is { Opening: true })
        {
            answer.AddRange(Switches(switches, "openingBoards", "openingLeaves"));
        }

        if (heading)
        {
            answer.Add(("heading", new JsonObject { ["type"] = "boolean" }));
        }

        return new LlmResponseSchema(BeatsName, Object(answer).ToJsonString());
    }

    /// <summary>
    /// A planned chapter's answer as the instruction states it: the spine, the opening and reply, and the words of each objective
    /// by number; <paramref name="task"/> adds an event step's "task".
    /// </summary>
    internal static string LinesShape(bool task, bool aboard, StorySwitches? switches, bool speakers = true) =>
        "{" + string.Join(", ", SpineFields.Select(field => $"\"{field}\": string"))
        + ", \"opening\": " + OpeningShape(speakers)
        + ", \"reply\": string, \"objectives\": [" + ObjectiveWordsShape(task, switches, speakers) + "]"
        + (aboard ? ", \"aboard\": [string]" : string.Empty)
        + (switches is { Opening: true } ? SwitchesShape(switches, "openingBoards", "openingLeaves") : string.Empty) + "}";

    /// <summary>A planned chapter's repair answer as the instruction states it: the objectives written again, and the opening and the spine only when asked for.</summary>
    internal static string LineRepairsShape(bool task, bool opening, bool spine, StorySwitches? switches, bool speakers = true) =>
        "{\"objectives\": [" + ObjectiveWordsShape(task, switches, speakers) + "]"
        + (opening ? ", \"opening\": " + OpeningShape(speakers) : string.Empty)
        + (spine ? ", " + string.Join(", ", SpineFields.Select(field => $"\"{field}\": string")) : string.Empty) + "}";

    private static string ObjectiveWordsShape(bool task, StorySwitches? switches, bool speakers = true) =>
        "{\"objective\": number, \"title\": string, \"reason\": string, \"lines\": " + (speakers ? "[{\"text\": string, \"speaker\": string}]" : "[string]")
        + (task ? ", \"task\": string|null" : string.Empty)
        + SwitchesShape(switches, "boards", "leaves") + "}";

    /// <summary>
    /// The schema of a planned chapter's answer: the spine, the opening and reply, and each objective's number, title, reason and
    /// lines, with the task where <paramref name="task"/> and the boardings and leavings due. It has no field for a place, a kind or a count.
    /// With no <paramref name="speakers"/>, as for a short story, the opening is a string and each line is a string.
    /// </summary>
    internal static LlmResponseSchema LinesSchema(IReadOnlyList<string>? speakers, bool task, IReadOnlyList<string>? cast = null, StorySwitches? switches = null)
    {
        var answer = new List<(string, JsonNode)>(SpineFields.Select(field => (field, (JsonNode)Text())))
        {
            ("opening", speakers is not null ? Lines(speakers) : Text()),
            ("reply", Text()),
            ("objectives", new JsonObject { ["type"] = "array", ["items"] = Object(ObjectiveWords(speakers, task, switches)) }),
        };

        if (cast is { Count: > 0 })
        {
            answer.Add(("aboard", IdList(cast)));
        }

        if (switches is { Opening: true })
        {
            answer.AddRange(Switches(switches, "openingBoards", "openingLeaves"));
        }

        return new LlmResponseSchema(LinesName, Object(answer).ToJsonString());
    }

    /// <summary>
    /// The schema of a planned chapter's repair answer: the objectives written again, and the opening and the spine only when asked for.
    /// With no <paramref name="speakers"/>, as for a short story, each line is a string.
    /// </summary>
    internal static LlmResponseSchema LineRepairsSchema(IReadOnlyList<string>? speakers, bool task, bool opening, bool spine, StorySwitches? switches = null)
    {
        var answer = new List<(string, JsonNode)>
        {
            ("objectives", new JsonObject { ["type"] = "array", ["items"] = Object(ObjectiveWords(speakers, task, switches)) }),
        };

        if (opening)
        {
            answer.Add(("opening", speakers is not null ? Lines(speakers) : Text()));
        }

        if (spine)
        {
            answer.AddRange(SpineFields.Select(field => (field, (JsonNode)Text())));
        }

        return new LlmResponseSchema(LineRepairsName, Object(answer).ToJsonString());
    }

    private static List<(string, JsonNode)> ObjectiveWords(IReadOnlyList<string>? speakers, bool task, StorySwitches? switches)
    {
        var words = new List<(string, JsonNode)>
        {
            ("objective", new JsonObject { ["type"] = "integer" }),
            ("title", Text()),
            ("reason", Text()),
            ("lines", speakers is not null ? Lines(speakers) : new JsonObject { ["type"] = "array", ["items"] = Text() }),
        };

        if (task)
        {
            words.Add(("task", Maybe("string")));
        }

        words.AddRange(Switches(switches, "boards", "leaves"));

        return words;
    }

    /// <summary>The schema of a repair answer: each replacement is a beat with the number of the objective it replaces.</summary>
    internal static LlmResponseSchema ReplacementsSchema(
        IReadOnlyList<string>? speakers, IReadOnlyList<string> kinds, bool opening, bool destination, IReadOnlyList<string>? ships = null, StorySwitches? switches = null)
    {
        var replacement = new List<(string, JsonNode)> { ("objective", new JsonObject { ["type"] = "integer" }) };
        replacement.AddRange(Beat(speakers, kinds, ships, switches));

        var answer = new List<(string, JsonNode)> { ("replacements", new JsonObject { ["type"] = "array", ["items"] = Object(replacement) }) };

        if (opening)
        {
            answer.Add(("opening", speakers is not null ? Lines(speakers) : Text()));
        }

        if (destination)
        {
            answer.Add(("destination", Destination()));
        }

        return new LlmResponseSchema(ReplacementsName, Object(answer).ToJsonString());
    }

    private static List<(string, JsonNode)> Beat(IReadOnlyList<string>? speakers, IReadOnlyList<string> kinds, IReadOnlyList<string>? ships, StorySwitches? switches)
    {
        var beat = new List<(string, JsonNode)>
        {
            ("title", Text()),
            ("function", Text()),
        };
        beat.AddRange(Step(kinds, ships));
        beat.Add(speakers is not null ? ("lines", Lines(speakers)) : ("line", Text()));
        beat.AddRange(Switches(switches, "boards", "leaves"));

        return beat;
    }

    /// <summary>A list field for each direction of <paramref name="switches"/> that has ids, holding only those ids.</summary>
    private static IEnumerable<(string, JsonNode)> Switches(StorySwitches? switches, string boards, string leaves)
    {
        if (switches?.Boarding is { Count: > 0 } boarding)
        {
            yield return (boards, IdList(boarding));
        }

        if (switches?.Leaving is { Count: > 0 } leaving)
        {
            yield return (leaves, IdList(leaving));
        }
    }

    private static JsonObject IdList(IReadOnlyList<string> ids) =>
        new()
        {
            ["type"] = "array",
            ["items"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. ids.Select(id => (JsonNode)JsonValue.Create(id)!)]) },
        };

    /// <summary>A step's fields: its kind and the fields that kind uses.</summary>
    private static List<(string, JsonNode)> Step(IReadOnlyList<string> kinds, IReadOnlyList<string>? ships)
    {
        var step = new List<(string, JsonNode)>
        {
            ("kind", new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(kinds.Select(kind => (JsonNode)JsonValue.Create(kind)!).ToArray()) }),
        };
        step.AddRange(FieldsFor(kinds).Select(field => (field.Name, field.Name == "ship" && ships is not null ? Hulls(ships) : Of(field.Shape))));

        return step;
    }

    private static JsonObject Destination() => Object([("system", Text()), ("body", Text())]);

    private static string Prose(Shape shape) => shape switch
    {
        Shape.Text => "string",
        Shape.MaybeText => "string|null",
        Shape.MaybeWhole => "number|null",
        _ => "boolean|null",
    };

    private static JsonNode Of(Shape shape) => shape switch
    {
        Shape.Text => Text(),
        Shape.MaybeText => Maybe("string"),
        Shape.MaybeWhole => Maybe("integer"),
        _ => Maybe("boolean"),
    };

    private static JsonObject Hulls(IReadOnlyList<string> ships) =>
        new()
        {
            ["type"] = new JsonArray("string", "null"),
            ["enum"] = new JsonArray([.. ships.Select(ship => (JsonNode)JsonValue.Create(ship)!), null]),
        };

    private static JsonObject Text() => new() { ["type"] = "string" };

    private static JsonObject Maybe(string type) => new() { ["type"] = new JsonArray(type, "null") };

    private static JsonObject Lines(IReadOnlyList<string> speakers) =>
        new()
        {
            ["type"] = "array",
            ["items"] = Object([("text", Text()), ("speaker", new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(speakers.Select(id => (JsonNode)JsonValue.Create(id)!).ToArray()) })]),
        };

    private static JsonObject Object(IEnumerable<(string Name, JsonNode Schema)> properties)
    {
        var list = properties.ToList();
        var shape = new JsonObject();

        foreach (var (name, schema) in list)
        {
            shape[name] = schema;
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = shape,
            ["required"] = new JsonArray(list.Select(property => (JsonNode)JsonValue.Create(property.Name)!).ToArray()),
            ["additionalProperties"] = false,
        };
    }
}
