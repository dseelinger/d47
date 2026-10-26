using System.Text.Json;
using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// Each story call carries a JSON schema built from the same field and kind lists as its instruction's JSON line.
/// </summary>
public sealed class AStoryCallSendsItsAnswerSchemaTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static readonly AdventureStory Chapter = new("s", "Title", "Public", "Hidden", Chapter: 1, DaysRunning: 3, DaysSinceBeacon: null);

    public static TheoryData<string> Variants => ["short", "cast", "beacon", "away", "finale", "switches"];

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
    public async Task AShortStoryWithNoShipInItsBriefIsNotOfferedBoard()
    {
        var beats = await BeatsCall("short");

        Assert.DoesNotContain("board", Kinds(beats.Schema));
        Assert.DoesNotContain("- \"board\":", beats.Text);
        Assert.False(BeatProperties(Root(beats.Schema)).GetProperty("ship").TryGetProperty("enum", out _));
    }

    [Fact]
    public async Task AShortStoryWhoseBriefNamesAHullIsOfferedBoardWithThatHull()
    {
        var beats = await BeatsCall("brief");
        var ship = BeatProperties(Root(beats.Schema)).GetProperty("ship");

        Assert.Contains("board", Kinds(beats.Schema));
        Assert.Equal(["Cobra MkIII", null], ship.GetProperty("enum").EnumerateArray().Select(name => name.GetString()));
        Assert.Contains("one of Cobra MkIII;", beats.Text);
    }

    [Fact]
    public async Task TheCastListSaysAMannerOfSpeakingIsNotAPrefix()
    {
        var beats = await BeatsCall("cast");

        Assert.Contains(StorySpeaker.MannerRule, beats.Text);
    }

    [Fact]
    public async Task AChapterIsOfferedBoardWithEveryHull()
    {
        var beats = await BeatsCall("cast");
        var hulls = BeatProperties(Root(beats.Schema)).GetProperty("ship").GetProperty("enum").EnumerateArray().Select(name => name.GetString()).ToList();

        Assert.Contains("board", Kinds(beats.Schema));
        Assert.Contains("Cobra MkIII", hulls);
        Assert.Contains("Anaconda", hulls);
        Assert.Null(hulls[^1]);
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

    [Fact]
    public async Task AChapterOffersItsSpeakerIdsAsAnEnumAndAShortStoryHasNone()
    {
        var chapter = Root((await BeatsCall("cast")).Schema).GetProperty("properties");
        var opening = chapter.GetProperty("opening").GetProperty("items").GetProperty("properties").GetProperty("speaker");
        var line = BeatProperties(Root((await BeatsCall("cast")).Schema)).GetProperty("lines").GetProperty("items").GetProperty("properties").GetProperty("speaker");

        Assert.Equal(["ship", "narrator", "tess"], opening.GetProperty("enum").EnumerateArray().Select(id => id.GetString()));
        Assert.Equal(["ship", "narrator", "tess"], line.GetProperty("enum").EnumerateArray().Select(id => id.GetString()));
        Assert.DoesNotContain("\"speaker\"", (await BeatsCall("short")).Schema.SchemaJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSwitchFieldsAreOfferedOnlyWhenDueWithOnlyTheDueIds()
    {
        var due = await BeatsCall("switches");
        var root = Root(due.Schema).GetProperty("properties");
        var beat = BeatProperties(Root(due.Schema));

        Assert.Equal(["tess"], beat.GetProperty("boards").GetProperty("items").GetProperty("enum").EnumerateArray().Select(id => id.GetString()));
        Assert.Equal(["tess"], root.GetProperty("openingBoards").GetProperty("items").GetProperty("enum").EnumerateArray().Select(id => id.GetString()));
        Assert.False(beat.TryGetProperty("leaves", out _));
        Assert.False(root.TryGetProperty("openingLeaves", out _));
        Assert.Contains("\"boards\": [\"tess\"]", due.Text[due.Text.LastIndexOf("Answer with one JSON object", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.Contains("Tess (\"tess\") comes aboard at the catalyst beat.", due.Text, StringComparison.Ordinal);

        var none = (await BeatsCall("cast")).Schema.SchemaJson;
        Assert.DoesNotContain("boards", none, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("leaves", none, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ABoardingWithNoSeatNamesTheSeatsAndTheCheapestHullsThatHaveThem()
    {
        var text = (await BeatsCall("seats", Credits(10_000_000_000))).Text;

        Assert.Contains(
            "Tess (\"tess\") comes aboard at the catalyst beat. The Commander's ship has no seat for Tess: place the boarding only on or after an objective "
            + "that boards a ship with 2 seats or more, such as Adder, Cobra MkIII, Cobra MkIV, Cobra MkV, Keelback.",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABoardingWithNoSeatThatNoHullCouldSeatIsNotInThePrompt()
    {
        var call = await BeatsCall("seats", Credits(0));

        Assert.DoesNotContain("comes aboard", call.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("boards", call.Schema.SchemaJson, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] Credits(long credits) =>
        [$$"""{ "timestamp":"2026-08-22T11:00:30Z", "event":"LoadGame", "Credits":{{credits}} }"""];

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

    private static async Task<(string Text, LlmResponseSchema Schema)> BeatsCall(string variant, string[]? journal = null) =>
        (await Calls(variant, journal)).Single(call => call.Schema.Name == "story_beats");

    private static async Task<List<(string Text, LlmResponseSchema Schema)>> Calls(string variant, string[]? journal = null)
    {
        var provider = new Capturing();
        var ask = variant switch
        {
            "cast" => new AdventureAsk(Story: Chapter with { Cast = [new AdventureSpeaker("tess", "Tess", "A pilot")] }),
            "switches" => new AdventureAsk(Story: Chapter with
            {
                Cast = [new AdventureSpeaker("tess", "Tess", "A pilot"), new AdventureSpeaker("vale", "Vale", "A courier")],
                Due = [new StorySwitch("tess", true, "catalyst")],
            }),
            "seats" => new AdventureAsk(Story: Chapter with
            {
                Cast = [new AdventureSpeaker("tess", "Tess", "A pilot")],
                Due = [new StorySwitch("tess", true, "catalyst", false, 2)],
            }),
            "beacon" => new AdventureAsk(Story: Chapter with { Beacon = new AdventureBeacon(1, "Beacon System") }),
            "away" => new AdventureAsk(Story: Chapter with { BeaconAway = new AdventureBeaconAway("Far System", 900, "too far") }),
            "finale" => new AdventureAsk(Story: Chapter with { FinaleChapter = 1 }),
            "brief" => new AdventureAsk(Length: AdventureLength.Short, Brief: "Take me to a Cobra Mk III"),
            _ => new AdventureAsk(Length: AdventureLength.Short),
        };

        var generator = journal is null
            ? AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), new RecordingLogger(), TimeProvider.System)
            : AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), new RecordingLogger(), 1, journal);

        await generator.GenerateAsync(ask, Now, CancellationToken.None);

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
