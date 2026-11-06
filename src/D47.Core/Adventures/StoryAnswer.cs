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
    internal const string FidelityName = "story_fidelity";
    internal const string HeadingName = "story_heading";
    internal const string PlainName = "story_plain";
    internal const string LinesName = "story_lines";
    internal const string LineRepairsName = "story_line_repairs";
    internal const string BriefName = "story_brief";

    internal static readonly IReadOnlyList<string> SpineFields = ["name", "premise", "want", "stake", "turn", "ending"];

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

    private static string SwitchesShape(StorySwitches? switches, string boards, string leaves) =>
        (switches?.Boarding is { Count: > 0 } boarding ? $", \"{boards}\": [{Ids(boarding)}]" : string.Empty)
        + (switches?.Leaving is { Count: > 0 } leaving ? $", \"{leaves}\": [{Ids(leaving)}]" : string.Empty);

    private static string Ids(IReadOnlyList<string> ids) => string.Join("|", ids.Select(id => $"\"{id}\""));

    private static string OpeningShape(bool speakers) => speakers ? "[{\"text\": string, \"speaker\": string}]" : "string";

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
