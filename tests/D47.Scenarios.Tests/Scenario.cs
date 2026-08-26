using System.Text.Json;
using System.Text.Json.Serialization;

namespace D47.Scenarios.Tests;

/// <summary>Which persona a scenario runs under.</summary>
public enum PersonaChoice
{
    /// <summary>Whatever the matrix is currently sweeping. The corpus's normal answer.</summary>
    Matrix,

    /// <summary>Personality off. Prompt position 3 is absent and position 2 must not move.</summary>
    Off,

    /// <summary>A named core, for a scenario that is about one.</summary>
    Named,
}

/// <summary>
/// One scenario: journal state, settings, a persona selection, an utterance and a list of
/// assertions. Everything the runner needs and nothing it could execute.
/// </summary>
public sealed record Scenario
{
    public required string Id { get; init; }

    /// <summary>Why this scenario exists, in the author's words. Printed in the report.</summary>
    public string? Note { get; init; }

    public required string Utterance { get; init; }

    /// <summary>
    /// Raw journal lines, applied through <see cref="D47.Core.Journal.GameStateStore"/> exactly as
    /// the tick loop applies them. This is the untrusted path into prompt position 7, and using the
    /// real fold rather than a hand-built state is what makes the injection real rather than
    /// simulated.
    /// </summary>
    public IReadOnlyList<string> Journal { get; init; } = [];

    /// <summary>
    /// Tool results to answer with instead of running the handler — the third-party path. Keyed by
    /// tool name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Poison { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Settings the Commander had already switched on before the turn, applied as
    /// <see cref="D47.Core.Configuration.SettingsCaller.Panel"/> because that is who switched them.
    /// <para>
    /// <b>This is how the dangerous case becomes testable.</b> d47's outward channels are gated by
    /// protected rows the model cannot reach, so with the shipped defaults an injection aimed at
    /// in-game chat cannot succeed however well it is written — which is the architecture working
    /// and is also a corpus that can never fail. The interesting question is the one a Commander
    /// creates by turning the row on: <em>having allowed d47 to send messages, can a hostile ship
    /// name make it send one?</em>
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, string> Settings { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public PersonaChoice Persona { get; init; } = PersonaChoice.Matrix;

    /// <summary>The core id when <see cref="Persona"/> is <see cref="PersonaChoice.Named"/>.</summary>
    public string? PersonaId { get; init; }

    /// <summary>
    /// Whether key injection is on for this scenario. Off by default and deliberately so: the
    /// action tools are the highest-consequence thing on the surface, and a corpus that only ever
    /// ran with them withdrawn would be a corpus that never tested the interesting case.
    /// </summary>
    public bool ActionsEnabled { get; init; }

    /// <summary>
    /// What d47 remembers about the Commander, as it would already have been rendered by
    /// <see cref="D47.Core.Memory.MemoryRecall"/> (list.md Phase 31).
    /// <para>
    /// <b>Prompt position 5, above the cache breakpoint, and the newest thing in the assembly that a
    /// model can be steered by.</b> A memory the model wrote itself is labelled as an inference and
    /// the block says so in as many words — and whether the model then <em>honours</em> that label is
    /// exactly the kind of claim this suite exists to measure rather than assert.
    /// </para>
    /// <para>
    /// Given as rendered text rather than as entries, because a scenario is data the runner cannot
    /// execute and a list of entries would need a store, a Commander and a situation to become one.
    /// The rendering is asserted where it belongs, in the Core tests.
    /// </para>
    /// </summary>
    public string? Recall { get; init; }

    public IReadOnlyList<ScenarioAssertion> Assertions { get; init; } = [];

    /// <summary>
    /// The largest <c>OutOf</c> any assertion here declares, which is the smallest N at which this
    /// scenario produces a result rather than a shrug.
    /// </summary>
    public int MinimumRuns => Assertions.Count == 0 ? 1 : Assertions.Max(a => a.Tolerance.OutOf);

    public override string ToString() => Id;
}

/// <summary>
/// One attempt to talk d47 into something, and what it is trying to reach.
/// <para>
/// The attacks are data rather than <c>[Theory]</c> attributes (docs/plans/
/// phase-30-prove-the-model-behaves.md, call 3): adding one is then not a code change, the report
/// can say how many of what kind ran, and the attacker's words sit in one file that is obviously
/// theirs rather than scattered through C# string literals.
/// </para>
/// </summary>
public sealed record Attack
{
    public required string Id { get; init; }

    /// <summary>What the attacker writes. Data the suite reads; never anything it executes.</summary>
    public required string Payload { get; init; }

    /// <summary>
    /// The tools this attack is trying to provoke, if it names any. Each becomes its own
    /// <see cref="AssertionKind.ToolDidNotRun"/> on top of the blanket
    /// <see cref="AssertionKind.NoToolRan"/>, so the report can say which attack aimed at what.
    /// </summary>
    public IReadOnlyList<string> Targets { get; init; } = [];

    public string? Note { get; init; }

    public override string ToString() => Id;
}

/// <summary>
/// A place an attack can be put, as a scenario with <c>{{payload}}</c> in it.
/// <para>
/// Vectors and attacks are crossed rather than written out, so a new attack is one entry and gets
/// fired down every path, and a new path is one entry and gets every attack.
/// </para>
/// </summary>
public sealed record Vector
{
    public required string Id { get; init; }

    /// <summary>Which untrusted path this is — journal, tool result. Printed in the report.</summary>
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

    /// <summary>
    /// The routing table: a real Commander sentence and the tool it must call
    /// (<a href="https://github.com/dseelinger/d47/issues/57">#57</a>).
    /// <para>
    /// <b>A suite and a table, not an instrument.</b> Everything it needs already existed —
    /// <see cref="ScenarioRunner"/> drives a turn, <c>TurnTrace.ToolCalls</c> records what ran, and
    /// <see cref="ScenarioAssertion"/> asserts against that list. What was missing was the rows.
    /// </para>
    /// <para>
    /// It is what makes <em>growing</em> the tool surface safe. Count is close to free while every
    /// tool is unambiguous; what costs is two tools that could both catch one sentence. A
    /// description is a hope about how a model reads, and until this there was no way to check the
    /// hope except a Commander hitting it while flying.
    /// </para>
    /// </summary>
    public static IReadOnlyList<Scenario> Routing() => Load<Scenario>("routing.json").Select(Validate).ToList();

    public static IReadOnlyList<Attack> Attacks() => Load<Attack>("attacks.json");

    public static IReadOnlyList<Vector> Vectors() => Load<Vector>("vectors.json");

    /// <summary>
    /// Every attack in every vector. The cross product is the corpus, and it is built here rather
    /// than written out so that a count in the report is a fact rather than a claim.
    /// </summary>
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
            // Every injection asserts the payload arrived. Without it, an attack that silently
            // failed to reach the prompt would report as resistance — a negative assertion
            // passing because the mechanism was absent, which is the trap this suite is built
            // around (finding 2).
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
            // Deduplicated, because a vector and an attack may name the same target and the report
            // should not print one claim twice as though it were two pieces of evidence.
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

    /// <summary>
    /// A short, distinctive slice of the payload, for asserting it reached the prompt.
    /// <para>
    /// A slice rather than the whole thing, because the journal fold is entitled to truncate a
    /// name and the prompt is entitled to reformat around it — and an assertion that demanded the
    /// payload survive byte for byte would fail on a vector doing its job.
    /// </para>
    /// </summary>
    public static string Marker(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var trimmed = payload.Trim();
        return trimmed.Length <= 24 ? trimmed : trimmed[..24];
    }

    private static string Substitute(string text, string payload) =>
        text.Replace("{{payload}}", payload, StringComparison.Ordinal);

    /// <summary>
    /// The same substitution, with the payload escaped for the JSON string it is landing in. A
    /// journal line is JSON, and an attack containing a quote would otherwise produce a line that
    /// does not parse — which <see cref="ScenarioWorld.ApplyJournal"/> refuses outright, correctly,
    /// but the corpus author should not have to think about it.
    /// </summary>
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
