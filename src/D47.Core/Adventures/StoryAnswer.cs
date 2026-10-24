using System.Text.Json.Nodes;
using D47.Core.Conversation;

namespace D47.Core.Adventures;

/// <summary>A story call's instruction text and the JSON schema its answer must match.</summary>
internal sealed record StoryCall(string Text, LlmResponseSchema Schema);

/// <summary>The fields and kinds a story answer has, written once for both the instruction's JSON line and the schema.</summary>
internal static class StoryAnswer
{
    internal const string SpineName = "story_spine";
    internal const string BeatsName = "story_beats";
    internal const string ReplacementsName = "story_replacements";
    internal const string FidelityName = "story_fidelity";

    internal static readonly IReadOnlyList<string> SpineFields = ["name", "premise", "want", "stake", "turn", "ending"];

    /// <summary>Kinds offered in every story.</summary>
    private static readonly string[] AlwaysKinds =
    [
        "arrive", "dock", "land", "scan", "rank", "bounty", "bond", "mission", "sell", "mine", "onfoot", "collect",
        "organic", "map", "signal", "wreck", "codex", "datasale", "salvage", "uss", "rescue", "engineer", "srv", "boost", "discover", "footfall", "contribute", "claim", "unlock", "crew",
        "suitmod", "conflict", "faction",
    ];

    /// <summary>The kinds a drawn chapter offers only for the activities drawn; the others stand for places and milestones.</summary>
    private static readonly HashSet<string> DrawnKinds = new(StringComparer.Ordinal)
    {
        "bounty", "bond", "mission", "sell", "mine", "onfoot", "collect", "organic", "map", "signal", "wreck", "codex", "datasale",
        "salvage", "uss", "rescue", "srv", "boost", "discover", "footfall", "contribute", "suitmod", "conflict", "faction",
    };

    internal const string EventKind = "event";

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

    /// <summary>The answer's JSON shape as the instruction states it.</summary>
    internal static string BeatsShape(bool speakers, IReadOnlyList<string> kinds, bool destination, IReadOnlyList<string>? ships = null, bool aboard = false) =>
        "{" + string.Join(", ", SpineFields.Select(field => $"\"{field}\": string"))
        + ", \"opening\": " + OpeningShape(speakers)
        + ", \"reply\": string, \"beats\": [" + BeatShape(speakers, kinds, ships, objective: false) + "]"
        + (destination ? ", " + DestinationShape : string.Empty)
        + (aboard ? ", \"aboard\": [string]" : string.Empty) + "}";

    /// <summary>A repair answer's JSON shape: the replacements, and the opening and destination only when they are asked for again.</summary>
    internal static string ReplacementsShape(bool speakers, IReadOnlyList<string> kinds, bool opening, bool destination, IReadOnlyList<string>? ships = null) =>
        "{\"replacements\": [" + BeatShape(speakers, kinds, ships, objective: true) + "]"
        + (opening ? ", \"opening\": " + OpeningShape(speakers) : string.Empty)
        + (destination ? ", " + DestinationShape : string.Empty) + "}";

    private const string DestinationShape = "\"destination\": {\"system\": string, \"body\": string}";

    private static string OpeningShape(bool speakers) => speakers ? "[{\"text\": string, \"speaker\": string}]" : "string";

    private static string BeatShape(bool speakers, IReadOnlyList<string> kinds, IReadOnlyList<string>? ships, bool objective) =>
        "{" + (objective ? "\"objective\": number, " : string.Empty) + "\"title\": string, \"function\": string, \"kind\": "
        + string.Join("|", kinds.Select(kind => $"\"{kind}\""))
        + string.Concat(FieldsFor(kinds).Select(field => $", \"{field.Name}\": {(field.Name == "ship" && ships is not null ? string.Join("|", ships.Select(ship => $"\"{ship}\"")) + "|null" : Prose(field.Shape))}"))
        + ", " + (speakers ? "\"lines\": [{\"text\": string, \"speaker\": string}]" : "\"line\": string")
        + (objective ? string.Empty : ", \"then\": [" + StepShape(kinds, ships) + "]") + "}";

    private static string StepShape(IReadOnlyList<string> kinds, IReadOnlyList<string>? ships) =>
        "{\"kind\": " + string.Join("|", kinds.Select(kind => $"\"{kind}\""))
        + string.Concat(FieldsFor(kinds).Select(field => $", \"{field.Name}\": {(field.Name == "ship" && ships is not null ? string.Join("|", ships.Select(ship => $"\"{ship}\"")) + "|null" : Prose(field.Shape))}"))
        + "}";

    /// <summary>The schema of a chapter's answer; a <paramref name="cast"/> with any id adds the "aboard" list of them.</summary>
    internal static LlmResponseSchema BeatsSchema(
        IReadOnlyList<string>? speakers, IReadOnlyList<string> kinds, bool destination, IReadOnlyList<string>? ships = null, IReadOnlyList<string>? cast = null)
    {
        var answer = new List<(string, JsonNode)>(SpineFields.Select(field => (field, (JsonNode)Text())))
        {
            ("opening", speakers is not null ? Lines(speakers) : Text()),
            ("reply", Text()),
            ("beats", new JsonObject { ["type"] = "array", ["items"] = Object([.. Beat(speakers, kinds, ships), ("then", new JsonObject { ["type"] = "array", ["items"] = Object(Step(kinds, ships)) })]) }),
        };

        if (destination)
        {
            answer.Add(("destination", Destination()));
        }

        if (cast is { Count: > 0 })
        {
            answer.Add(("aboard", new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. cast.Select(id => (JsonNode)JsonValue.Create(id)!)]) },
            }));
        }

        return new LlmResponseSchema(BeatsName, Object(answer).ToJsonString());
    }

    /// <summary>The schema of a repair answer: each replacement is a beat with the number of the objective it replaces.</summary>
    internal static LlmResponseSchema ReplacementsSchema(IReadOnlyList<string>? speakers, IReadOnlyList<string> kinds, bool opening, bool destination, IReadOnlyList<string>? ships = null)
    {
        var replacement = new List<(string, JsonNode)> { ("objective", new JsonObject { ["type"] = "integer" }) };
        replacement.AddRange(Beat(speakers, kinds, ships));

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

    private static List<(string, JsonNode)> Beat(IReadOnlyList<string>? speakers, IReadOnlyList<string> kinds, IReadOnlyList<string>? ships)
    {
        var beat = new List<(string, JsonNode)>
        {
            ("title", Text()),
            ("function", Text()),
        };
        beat.AddRange(Step(kinds, ships));
        beat.Add(speakers is not null ? ("lines", Lines(speakers)) : ("line", Text()));

        return beat;
    }

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
