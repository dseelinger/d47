using System.Text.Json;
using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using D47.Core.Tests.Stories;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>Each story call carries a JSON schema built from the same field lists as its instruction's JSON line.</summary>
public sealed class AStoryCallSendsItsAnswerSchemaTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static readonly AdventureStory Chapter = PlannedChapterWorld.Story with { Cast = [] };

    public static TheoryData<string> Variants => ["short", "cast", "switches"];

    [Fact]
    public async Task TheCastListSaysAMannerOfSpeakingIsNotAPrefix()
    {
        var lines = await LinesCall("cast");

        Assert.Contains(StorySpeaker.MannerRule, lines.Text);
    }

    [Fact]
    public async Task ACastGetsLinesAndAnArrayOpening()
    {
        var root = Root((await LinesCall("cast")).Schema);
        var objective = ObjectiveProperties(root);

        Assert.Equal("array", root.GetProperty("properties").GetProperty("opening").GetProperty("type").GetString());
        Assert.Equal("array", objective.GetProperty("lines").GetProperty("type").GetString());
        Assert.Equal("object", objective.GetProperty("lines").GetProperty("items").GetProperty("type").GetString());
    }

    [Fact]
    public async Task AStoryWithoutACastGetsStringLinesAndAStringOpening()
    {
        var root = Root((await LinesCall("short")).Schema);
        var objective = ObjectiveProperties(root);

        Assert.Equal("string", root.GetProperty("properties").GetProperty("opening").GetProperty("type").GetString());
        Assert.Equal("string", objective.GetProperty("lines").GetProperty("items").GetProperty("type").GetString());
    }

    [Fact]
    public async Task AChapterOffersItsSpeakerIdsAsAnEnumAndAShortStoryHasNone()
    {
        var root = Root((await LinesCall("cast")).Schema).GetProperty("properties");
        var opening = root.GetProperty("opening").GetProperty("items").GetProperty("properties").GetProperty("speaker");
        var line = ObjectiveProperties(Root((await LinesCall("cast")).Schema)).GetProperty("lines").GetProperty("items").GetProperty("properties").GetProperty("speaker");

        Assert.Equal(["ship", "narrator", "tess"], opening.GetProperty("enum").EnumerateArray().Select(id => id.GetString()));
        Assert.Equal(["ship", "narrator", "tess"], line.GetProperty("enum").EnumerateArray().Select(id => id.GetString()));
        Assert.DoesNotContain("\"speaker\"", (await LinesCall("short")).Schema.SchemaJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSwitchFieldsAreOfferedOnlyWhenDueWithOnlyTheDueIds()
    {
        var due = await LinesCall("switches");
        var root = Root(due.Schema).GetProperty("properties");
        var objective = ObjectiveProperties(Root(due.Schema));

        Assert.Equal(["tess"], objective.GetProperty("boards").GetProperty("items").GetProperty("enum").EnumerateArray().Select(id => id.GetString()));
        Assert.Equal(["tess"], root.GetProperty("openingBoards").GetProperty("items").GetProperty("enum").EnumerateArray().Select(id => id.GetString()));
        Assert.False(objective.TryGetProperty("leaves", out _));
        Assert.False(root.TryGetProperty("openingLeaves", out _));

        var none = (await LinesCall("cast")).Schema.SchemaJson;
        Assert.DoesNotContain("boards", none, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("leaves", none, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ABoardingWithNoSeatNamesTheSeatsAndTheCheapestHullsThatHaveThem()
    {
        var text = (await LinesCall("seats", 10_000_000_000)).Text;

        Assert.Contains("Tess (\"tess\") comes aboard at the catalyst beat. The Commander's ship has no seat for Tess: place the boarding only on or after an objective", text, StringComparison.Ordinal);
        Assert.Contains("boards a ship with 2 seats or more", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABoardingWithNoSeatThatNoHullCouldSeatIsNotInThePrompt()
    {
        var call = await LinesCall("seats", seats: 99);

        Assert.DoesNotContain("comes aboard", call.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("boards", call.Schema.SchemaJson, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task EveryObjectIsStrictWithEveryPropertyRequired(string variant)
    {
        var call = await LinesCall(variant);

        Assert.Equal("story_lines", call.Schema.Name);
        Strict(Root(call.Schema));
    }

    private static JsonElement ObjectiveProperties(JsonElement root) =>
        root.GetProperty("properties").GetProperty("objectives").GetProperty("items").GetProperty("properties");

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

    private static async Task<(string Text, LlmResponseSchema Schema)> LinesCall(string variant, long? credits = null, int seats = 2)
    {
        var provider = new Capturing();
        var tess = new AdventureSpeaker("tess", "Tess", "A pilot");
        var ask = variant switch
        {
            "cast" => PlannedChapterWorld.Ask(Chapter with { Cast = [tess] }),
            "switches" => PlannedChapterWorld.Ask(Chapter with
            {
                Cast = [tess, new AdventureSpeaker("vale", "Vale", "A courier")],
                Due = [new StorySwitch("tess", true, "catalyst")],
            }),
            "seats" => PlannedChapterWorld.Ask(Chapter with
            {
                Cast = [tess],
                Due = [new StorySwitch("tess", true, "catalyst", false, seats)],
            }),
            _ => new AdventureAsk(AdventureReach.Session, AdventureLength.Evening),
        };

        var generator = PlannedChapterWorld.Generator(provider, PlannedChapterWorld.Roomy(), credits is { } balance ? () => WithCredits(balance) : null);

        await generator.GenerateAsync(ask, Now, CancellationToken.None);

        return provider.Calls.First(call => call.Schema.Name == "story_lines");
    }

    private static CommanderGameState WithCredits(long credits)
    {
        var store = new GameStateStore();

        foreach (var line in EmbeddedFixture.Lines("planner.Journal.2026-11-01T100000.01.log").Append($$"""{ "timestamp":"2026-11-01T10:30:00Z", "event":"LoadGame", "Credits":{{credits}} }"""))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var journalEvent));
            store.Apply(journalEvent!);
        }

        return store.Active!;
    }

    private sealed class Capturing : StoryTestProvider
    {
        public List<(string Text, LlmResponseSchema Schema)> Calls { get; } = [];

        public override string DisplayName => "Capturing";

        public override async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
            LlmRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var schema = Assert.IsType<LlmResponseSchema>(request.ResponseSchema);
            Calls.Add((string.Concat(request.Prompt.History.Select(message => message.Text)), schema));
            await Task.CompletedTask;

            yield return new LlmStreamEvent.Failed("Stop.", Transient: false);
        }
    }
}
