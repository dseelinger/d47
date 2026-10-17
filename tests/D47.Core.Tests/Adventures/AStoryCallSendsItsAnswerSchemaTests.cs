using System.Text.Json;
using D47.Core.Adventures;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// Each story call carries a JSON schema built from the same field and kind lists as its instruction's JSON line.
/// </summary>
public sealed class AStoryCallSendsItsAnswerSchemaTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static readonly AdventureStory Chapter = new("s", "Title", "Public", "Hidden", Chapter: 1, DaysRunning: 3, DaysSinceBeacon: null);

    public static TheoryData<string> Variants => ["short", "cast", "beacon", "away", "finale"];

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task TheBeatsKindEnumIsTheKindsTheInstructionOffers(string variant)
    {
        var beats = await BeatsCall(variant);
        var kinds = Kinds(beats.Schema);

        Assert.All(kinds, kind => Assert.Contains(kind, AdventureValidation.Kinds));
        Assert.Equal(kinds.Count, kinds.Distinct().Count());
        Assert.Contains("conflict", kinds);
        Assert.Contains("faction", kinds);
        Assert.Equal(variant == "beacon", kinds.Contains("beacon"));
        Assert.Equal(variant == "away", kinds.Contains("reach"));
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task TheInstructionNamesEachKindInItsEnumAndInItsJsonLine(string variant)
    {
        var beats = await BeatsCall(variant);
        var jsonLine = beats.Text[beats.Text.LastIndexOf("Answer with one JSON object", StringComparison.Ordinal)..];

        foreach (var kind in Kinds(beats.Schema))
        {
            Assert.Contains($"- \"{kind}\":", beats.Text);
            Assert.Contains($"\"{kind}\"", jsonLine);
        }
    }

    [Fact]
    public async Task ACastGetsLinesAndAnArrayOpening()
    {
        var root = Root((await BeatsCall("cast")).Schema);
        var beat = BeatProperties(root);

        Assert.Equal("array", root.GetProperty("properties").GetProperty("opening").GetProperty("type").GetString());
        Assert.True(beat.TryGetProperty("lines", out _));
        Assert.False(beat.TryGetProperty("line", out _));
    }

    [Fact]
    public async Task AStoryWithoutACastGetsALineAndAStringOpening()
    {
        var root = Root((await BeatsCall("short")).Schema);
        var beat = BeatProperties(root);

        Assert.Equal("string", root.GetProperty("properties").GetProperty("opening").GetProperty("type").GetString());
        Assert.True(beat.TryGetProperty("line", out _));
        Assert.False(beat.TryGetProperty("lines", out _));
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task TheDestinationIsRequiredOnlyForFinaleChapterOne(string variant)
    {
        var root = Root((await BeatsCall(variant)).Schema);
        var has = root.GetProperty("properties").TryGetProperty("destination", out _);
        var required = root.GetProperty("required").EnumerateArray().Select(name => name.GetString()).Contains("destination");

        Assert.Equal(variant == "finale", has);
        Assert.Equal(has, required);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task EveryObjectIsStrictWithEveryPropertyRequired(string variant)
    {
        Strict(Root((await BeatsCall(variant)).Schema));
        Strict(Root((await Calls(variant))[0].Schema));
    }

    [Fact]
    public async Task TheSpineSchemaHasTheSixStringFieldsAllRequired()
    {
        var call = (await Calls("short"))[0];
        var root = Root(call.Schema);

        Assert.Equal("story_spine", call.Schema.Name);
        Assert.Equal(["name", "premise", "want", "stake", "turn", "ending"], root.GetProperty("required").EnumerateArray().Select(name => name.GetString()));
        Assert.All(root.GetProperty("properties").EnumerateObject(), property => Assert.Equal("string", property.Value.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task TheBeatsSchemaTypesMatchTheInstruction()
    {
        var call = await BeatsCall("short");
        var beat = BeatProperties(Root(call.Schema));

        Assert.Equal("story_beats", call.Schema.Name);
        Assert.Equal("string", beat.GetProperty("title").GetProperty("type").GetString());
        Assert.Equal(["integer", "null"], Types(beat, "rank"));
        Assert.Equal(["integer", "null"], Types(beat, "count"));
        Assert.Equal(["boolean", "null"], Types(beat, "organic"));
        Assert.Equal(["string", "null"], Types(beat, "stage"));
    }

    private static IEnumerable<string?> Types(JsonElement beat, string name) =>
        beat.GetProperty(name).GetProperty("type").EnumerateArray().Select(type => type.GetString());

    private static JsonElement BeatProperties(JsonElement root) =>
        root.GetProperty("properties").GetProperty("beats").GetProperty("items").GetProperty("properties");

    private static void Strict(JsonElement schema)
    {
        if (schema.TryGetProperty("properties", out var properties))
        {
            Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
            Assert.Equal(
                properties.EnumerateObject().Select(property => property.Name),
                schema.GetProperty("required").EnumerateArray().Select(name => name.GetString()));

            foreach (var property in properties.EnumerateObject())
            {
                Strict(property.Value);
            }
        }

        if (schema.TryGetProperty("items", out var items))
        {
            Strict(items);
        }

        foreach (var limit in new[] { "minLength", "maxLength", "minItems", "maxItems", "minimum", "maximum" })
        {
            Assert.False(schema.TryGetProperty(limit, out _), limit);
        }
    }

    private static JsonElement Root(LlmResponseSchema schema) => JsonDocument.Parse(schema.SchemaJson).RootElement;

    private static IReadOnlyList<string> Kinds(LlmResponseSchema schema) =>
        BeatProperties(Root(schema)).GetProperty("kind").GetProperty("enum").EnumerateArray().Select(kind => kind.GetString()!).ToList();

    private static async Task<(string Text, LlmResponseSchema Schema)> BeatsCall(string variant) =>
        (await Calls(variant)).Single(call => call.Schema.Name == "story_beats");

    private static async Task<List<(string Text, LlmResponseSchema Schema)>> Calls(string variant)
    {
        var provider = new Capturing();
        var ask = variant switch
        {
            "cast" => new AdventureAsk(Story: Chapter with { Cast = [new AdventureSpeaker("tess", "Tess", "A pilot")] }),
            "beacon" => new AdventureAsk(Story: Chapter with { Beacon = new AdventureBeacon(1, "Beacon System") }),
            "away" => new AdventureAsk(Story: Chapter with { BeaconAway = new AdventureBeaconAway("Far System", 900, "too far") }),
            "finale" => new AdventureAsk(Story: Chapter with { FinaleChapter = 1 }),
            _ => new AdventureAsk(Length: AdventureLength.Short),
        };

        await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), new RecordingLogger(), TimeProvider.System)
            .GenerateAsync(ask, Now, CancellationToken.None);

        return provider.Calls;
    }

    private sealed class Capturing : ILlmProvider
    {
        public List<(string Text, LlmResponseSchema Schema)> Calls { get; } = [];

        public string Id => "anthropic";

        public string DisplayName => "Capturing";

        public string DefaultModel => "claude-opus-5";

        public bool RunsOnThisMachine => true;

        public LlmProviderCapabilities CapabilitiesFor(string model) => new()
        {
            SupportsPromptCaching = false,
            SupportsThinkingEffort = true,
            SupportsOperatorSystemMessages = true,
            MinimumCacheablePrefixTokens = 0,
        };

        public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
            LlmRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var schema = Assert.IsType<LlmResponseSchema>(request.ResponseSchema);
            Calls.Add((string.Concat(request.Prompt.History.Select(message => message.Text)), schema));
            await Task.CompletedTask;

            if (schema.Name == "story_spine")
            {
                yield return new LlmStreamEvent.TextDelta(AdventureGeneratorTests.Spine);
                yield return new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed);
            }
            else
            {
                yield return new LlmStreamEvent.Failed("Stop.", Transient: false);
            }
        }
    }
}
