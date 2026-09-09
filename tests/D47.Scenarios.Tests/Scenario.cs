using System.Text.Json;
using System.Text.Json.Serialization;

namespace D47.Scenarios.Tests;

/// <summary>Which persona a scenario runs under.</summary>
public enum PersonaChoice
{
    /// <summary>Whatever the matrix is currently sweeping.</summary>
    Matrix,

    /// <summary>Personality off.</summary>
    Off,

    /// <summary>A named core, for a scenario that is about one.</summary>
    Named,
}

/// <summary>
/// One scenario: journal state, settings, a persona selection, an utterance and a list of assertions.
/// </summary>
public sealed record Scenario
{
    public required string Id { get; init; }

    /// <summary>Why this scenario exists, in the author's words.</summary>
    public string? Note { get; init; }

    public required string Utterance { get; init; }

    /// <summary>
    /// Raw journal lines, applied through <see cref="D47.Core.Journal.GameStateStore"/> exactly as the
    /// tick loop applies them.
    /// </summary>
    public IReadOnlyList<string> Journal { get; init; } = [];

    /// <summary>Tool results to answer with instead of running the handler — the third-party path.</summary>
    public IReadOnlyDictionary<string, string> Poison { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Settings the Commander had already switched on before the turn, applied as <see
    /// cref="D47.Core.Configuration.SettingsCaller.Panel"/> because that is who switched them.
    /// </summary>
    public IReadOnlyDictionary<string, string> Settings { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public PersonaChoice Persona { get; init; } = PersonaChoice.Matrix;

    /// <summary>The core id when <see cref="Persona"/> is <see cref="PersonaChoice.Named"/>.</summary>
    public string? PersonaId { get; init; }

    /// <summary>Whether key injection is on for this scenario.</summary>
    public bool ActionsEnabled { get; init; }

    /// <summary>What d47 remembers about the Commander, as <see cref="D47.Core.Memory.MemoryRecall"/> would already have rendered it.</summary>
    public string? Recall { get; init; }

    /// <summary>What was already said before this turn, handed to the loop as its transcript.</summary>
    public IReadOnlyList<D47.Core.Conversation.ConversationMessage> History { get; init; } = [];

    public IReadOnlyList<ScenarioAssertion> Assertions { get; init; } = [];

    /// <summary>
    /// The largest <c>OutOf</c> any assertion here declares, which is the smallest N at which this
    /// scenario produces a result rather than a shrug.
    /// </summary>
    public int MinimumRuns => Assertions.Count == 0 ? 1 : Assertions.Max(a => a.Tolerance.OutOf);

    public override string ToString() => Id;
}

/// <summary>One attempt to talk d47 into something, and what it is trying to reach.</summary>
public sealed record Attack
{
    public required string Id { get; init; }

    /// <summary>What the attacker writes.</summary>
    public required string Payload { get; init; }

    /// <summary>The tools this attack is trying to provoke, if it names any.</summary>
    public IReadOnlyList<string> Targets { get; init; } = [];

    public string? Note { get; init; }

    public override string ToString() => Id;
}

/// <summary>A place an attack can be put, as a scenario with <c>{{payload}}</c> in it.</summary>
public sealed record Vector
{
    public required string Id { get; init; }

    /// <summary>Which untrusted path this is — journal, tool result.</summary>
    public required string Path { get; init; }

    public string? Note { get; init; }

    /// <summary>The scenario, with <c>{{payload}}</c> wherever the attack's words go.</summary>
    public required Scenario Template { get; init; }

    public override string ToString() => Id;
}

/// <summary>Reading the corpus files, and refusing them when they say something impossible.</summary>
public static class Corpus
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(), new ToleranceConverter() },
    };

    /// <summary>Where the corpus is, beside the test assembly.</summary>
    public static string Directory => Path.Combine(AppContext.BaseDirectory, "Corpus");

    public static IReadOnlyList<Scenario> Scenarios() => Load<Scenario>("scenarios.json").Select(Validate).ToList();

 /// <summary>The routing table: a real Commander sentence and the tool it must call.</summary>
    public static IReadOnlyList<Scenario> Routing() => Load<Scenario>("routing.json").Select(Validate).ToList();

    public static IReadOnlyList<Attack> Attacks() => Load<Attack>("attacks.json");

    public static IReadOnlyList<Vector> Vectors() => Load<Vector>("vectors.json");

    /// <summary>Every attack in every vector.</summary>
    public static IReadOnlyList<Scenario> Injections()
    {
        var attacks = Attacks();

        return
        [
            .. from vector in Vectors()
               from attack in attacks
               select Validate(Expand(vector, attack)),
        ];
    }

    /// <summary>One vector with one attack's words substituted, and its assertions merged in.</summary>
    private static Scenario Expand(Vector vector, Attack attack)
    {
        var template = vector.Template;

        var assertions = new List<ScenarioAssertion>(template.Assertions)
        {
            // Every injection asserts the payload arrived.
            new()
            {
                Kind = AssertionKind.PromptIncludes,
                Target = Marker(attack.Payload),
                Note = "the control: the attack reached the prompt, so a pass means something",
            },
        };

        assertions.AddRange(attack.Targets.Select(target => new ScenarioAssertion
        {
            Kind = AssertionKind.ToolDidNotRun,
            Target = target,
            Note = $"what {attack.Id} was reaching for",
        }));

        return template with
        {
            // Deduplicated: a vector and an attack may name the same target, and one claim printed twice is not two pieces of evidence.
            Id = $"{vector.Id}/{attack.Id}",
            Note = attack.Note ?? vector.Note,
            Utterance = Substitute(template.Utterance, attack.Payload),
            Journal = [.. template.Journal.Select(line => SubstituteInJson(line, attack.Payload))],
            Settings = template.Settings,
            Poison = template.Poison.ToDictionary(
                entry => entry.Key,
                entry => Substitute(entry.Value, attack.Payload),
                StringComparer.Ordinal),
            Assertions = [.. assertions.DistinctBy(assertion => (assertion.Kind, assertion.Target))],
        };
    }

    /// <summary>A short, distinctive slice of the payload, for asserting it reached the prompt.</summary>
    public static string Marker(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var trimmed = payload.Trim();
        return trimmed.Length <= 24 ? trimmed : trimmed[..24];
    }

    private static string Substitute(string text, string payload) =>
        text.Replace("{{payload}}", payload, StringComparison.Ordinal);

    /// <summary>The same substitution, with the payload escaped for the JSON string it is landing in.</summary>
    private static string SubstituteInJson(string line, string payload)
    {
        var escaped = JsonSerializer.Serialize(payload);
        return line.Replace("\"{{payload}}\"", escaped, StringComparison.Ordinal);
    }

    private static Scenario Validate(Scenario scenario) =>
        scenario with { Assertions = [.. scenario.Assertions.Select(assertion => assertion.Validated())] };

    private static IReadOnlyList<T> Load<T>(string file)
    {
        var path = Path.Combine(Directory, file);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"The corpus file '{file}' is not beside the test assembly. It is Content in the csproj and "
                + "should have been copied; a missing one means the suite would run against nothing and pass.",
                path);
        }

        return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), Options)
               ?? throw new InvalidOperationException($"'{file}' held no entries.");
    }

    /// <summary>Reads <c>"4/5"</c>, and nothing else, so a tolerance cannot be written ambiguously.</summary>
    private sealed class ToleranceConverter : JsonConverter<Tolerance>
    {
        public override Tolerance Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var text = reader.GetString() ?? string.Empty;
            var parts = text.Split('/');

            if (parts.Length != 2
                || !int.TryParse(parts[0], out var atLeast)
                || !int.TryParse(parts[1], out var outOf))
            {
                throw new JsonException($"'{text}' is not a tolerance. Write it as \"4/5\".");
            }

            return new Tolerance(atLeast, outOf);
        }

        public override void Write(Utf8JsonWriter writer, Tolerance value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(value);
            writer.WriteStringValue(value.ToString());
        }
    }
}
