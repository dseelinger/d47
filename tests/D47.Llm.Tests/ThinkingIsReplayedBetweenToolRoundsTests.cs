using System.Text.Json;
using System.Text.Json.Nodes;
using D47.Core.Capabilities;
using D47.Core.Conversation;
using D47.Llm.OpenAi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Llm.Tests;

/// <summary>
/// A turn of several tool rounds sends each round's thinking back on the rounds after it, and each round's
/// request begins with the whole of the one before, so the block is not refused or dropped.
/// </summary>
[Collection(nameof(EndpointDemotionCollection))]
public class ThinkingIsReplayedBetweenToolRoundsTests
{
    private const string Operator = "claude-opus-5-5";

    private const string Folding = "claude-sonnet-5";

    private const string FirstThought = "The Commander wants a distance.";

    private const string FirstSignature = "c2lnbmF0dXJlLW9uZQ+/==";

    public ThinkingIsReplayedBetweenToolRoundsTests() => EndpointDemotions.Clear();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string[] CallingRound(string model, bool redacted = false) =>
    [
        Recordings.MessageStart(model),
        .. redacted
            ? (string[])[Recordings.RedactedThinkingBlockStart("ZW5jcnlwdGVk", index: 0)]
            :
            [
                Recordings.ThinkingBlockStart(index: 0),
                Recordings.ThinkingDelta(FirstThought, index: 0),
                Recordings.SignatureDelta(FirstSignature, index: 0),
            ],
        Recordings.BlockStop(index: 0),
        Recordings.ToolUseBlockStart("toolu_1", "look_up_distance", index: 1),
        Recordings.InputJsonDelta("""{"system":"Sol"}""", index: 1),
        Recordings.BlockStop(index: 1),
        Recordings.MessageDelta("tool_use"),
        Recordings.MessageStop(),
    ];

    private static string[] AnsweringRound(string model) =>
    [
        Recordings.MessageStart(model),
        Recordings.ThinkingBlockStart(index: 0),
        Recordings.ThinkingDelta("Sol is here.", index: 0),
        Recordings.SignatureDelta("c2lnbmF0dXJlLXR3bw==", index: 0),
        Recordings.BlockStop(index: 0),
        Recordings.TextBlockStart(index: 1),
        Recordings.TextDelta("Sol is right here.", index: 1),
        Recordings.BlockStop(index: 1),
        Recordings.MessageDelta("end_turn"),
        Recordings.MessageStop(),
    ];

    /// <summary>A two-round turn whose tool changes the recall and, when asked, the game state.</summary>
    private static async Task<(JsonElement First, JsonElement Second, TurnLoop Loop)> TwoRoundsAsync(
        string model,
        bool stateChanges,
        int maxToolRounds = 8,
        bool redacted = false)
    {
        var (requests, loop) = await RequestsAsync(model, stateChanges, maxToolRounds, redacted, followUp: null);

        Assert.Equal(2, requests.Count);

        return (requests[0], requests[1], loop);
    }

    /// <summary>
    /// The two-round turn, then, when <paramref name="followUp"/> is given, a one-round turn asking it.
    /// </summary>
    private static async Task<(IReadOnlyList<JsonElement> Requests, TurnLoop Loop)> RequestsAsync(
        string model,
        bool stateChanges,
        int maxToolRounds,
        bool redacted,
        string? followUp)
    {
        using var endpoint = followUp is null
            ? RecordedEndpoint.StreamingEach(CallingRound(model, redacted), AnsweringRound(model))
            : RecordedEndpoint.StreamingEach(CallingRound(model, redacted), AnsweringRound(model), AnsweringRound(model));

        var state = "Docked at Jameson Memorial.";
        TurnLoop? loop = null;

        var registry = CapabilityRegistry.Build(
        [
            new CapabilityDescriptor
            {
                Id = "galaxy",
                Group = "Knowledge",
                Name = "Galaxy",
                Summary = "Looks things up in the galaxy.",
                Tools =
                [
                    new ToolDefinition
                    {
                        Name = "look_up_distance",
                        Description = "How far one system is from another.",
                        Parameters =
                        [
                            new ToolParameter
                            {
                                Name = "system",
                                Type = ToolParameterType.String,
                                Description = "The system to measure to.",
                                Required = true,
                            },
                        ],
                        Handler = (_, _) =>
                        {
                            loop!.Recall = "The Commander has just been told something new.";

                            if (stateChanges)
                            {
                                state = "Undocked from Jameson Memorial.";
                            }

                            return Task.FromResult(ToolResult.Ok("Sol is 0 light years away."));
                        },
                    },
                ],
            },
        ]);

        loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            new AnthropicLlmProvider("test-key", endpoint.BaseUrl),
            model)
        {
            Retry = RetryPolicy.Default with { Attempts = 1 },
            MaxToolRounds = maxToolRounds,
            Persona = "You are the ship's computer.",
            Recall = "The Commander flies a Python.",
            LiveGameState = () => state,
        };

        await foreach (var _ in loop.RunAsync("how far is Sol", cancellationToken: Token))
        {
        }

        if (followUp is not null)
        {
            await foreach (var _ in loop.RunAsync(followUp, cancellationToken: Token))
            {
            }
        }

        return ([.. endpoint.Requests.Select(Parse)], loop);
    }

    private static JsonElement Parse(string body) => JsonDocument.Parse(body).RootElement.Clone();

    private static string[] Raw(JsonElement array) => [.. array.EnumerateArray().Select(element => element.GetRawText())];

    /// <summary>Each element's JSON with every <c>cache_control</c> removed.</summary>
    private static string[] Unmarked(JsonElement array) =>
        [.. array.EnumerateArray().Select(element => Unmarked(JsonNode.Parse(element.GetRawText())!).ToJsonString())];

    private static JsonNode Unmarked(JsonNode node)
    {
        switch (node)
        {
            case JsonObject members:
                members.Remove("cache_control");

                foreach (var (_, member) in members)
                {
                    if (member is not null)
                    {
                        Unmarked(member);
                    }
                }

                break;

            case JsonArray items:
                foreach (var item in items)
                {
                    if (item is not null)
                    {
                        Unmarked(item);
                    }
                }

                break;
        }

        return node;
    }

    private static bool Marked(JsonElement block) =>
        block.TryGetProperty("cache_control", out var control) && control.ValueKind == JsonValueKind.Object;

    /// <summary>Every content block of every message that has a block list, in order.</summary>
    private static JsonElement[] Blocks(JsonElement messages) =>
    [
        .. messages
            .EnumerateArray()
            .Where(message => message.GetProperty("content").ValueKind == JsonValueKind.Array)
            .SelectMany(message => message.GetProperty("content").EnumerateArray()),
    ];

    [Trait("Category", "Integration")]
    [Theory]
    [InlineData(Operator)]
    [InlineData(Folding)]
    public async Task EachRoundBeginsWithTheWholeOfTheLast(string model)
    {
        var (first, second, _) = await TwoRoundsAsync(model, stateChanges: true);

        Assert.Equal(Raw(first.GetProperty("system")), Raw(second.GetProperty("system")));
        Assert.Equal(Raw(first.GetProperty("tools")), Raw(second.GetProperty("tools")));

        var before = Unmarked(first.GetProperty("messages"));
        var after = Unmarked(second.GetProperty("messages"));

        Assert.True(after.Length > before.Length);
        Assert.Equal(before, after[..before.Length]);

        // Round 1's question is the end of its history, so round 2 reads everything round 1 sent from cache.
        var question = Blocks(first.GetProperty("messages")).Single(Marked);
        Assert.Equal("text", question.GetProperty("type").GetString());
        Assert.EndsWith("how far is Sol", question.GetProperty("text").GetString(), StringComparison.Ordinal);

        var added = second.GetProperty("messages").EnumerateArray().Skip(before.Length).ToList();
        Assert.Equal("assistant", added[0].GetProperty("role").GetString());
        Assert.Equal("user", added[1].GetProperty("role").GetString());
        Assert.Equal("tool_result", added[1].GetProperty("content")[0].GetProperty("type").GetString());
    }

    /// <summary>
    /// The next turn's request repeats the last turn's first request up to its question, so it can read the
    /// entry that request wrote there.
    /// </summary>
    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheNextTurnBeginsWithTheLastTurnsQuestion()
    {
        var (requests, _) = await RequestsAsync(
            "claude-sonnet-5-5", stateChanges: false, maxToolRounds: 8, redacted: false, followUp: "and Achenar");

        Assert.Equal(3, requests.Count);

        // The first request is the question and the game state after it.
        var firstRequest = requests[0].GetProperty("messages");
        var question = firstRequest[0];

        Assert.Equal("user", question.GetProperty("role").GetString());
        Assert.True(Marked(Blocks(firstRequest)[^1]), "the first request's question is not marked");
        Assert.Equal(Unmarked(firstRequest)[0], Unmarked(requests[2].GetProperty("messages"))[0]);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ARoundsThinkingGoesBackUnchangedInItsPlace()
    {
        var (first, second, _) = await TwoRoundsAsync(Operator, stateChanges: false);

        var assistant = second.GetProperty("messages")[first.GetProperty("messages").GetArrayLength()];
        var content = assistant.GetProperty("content");

        Assert.Equal("thinking", content[0].GetProperty("type").GetString());
        Assert.Equal(FirstThought, content[0].GetProperty("thinking").GetString());
        Assert.Equal(FirstSignature, content[0].GetProperty("signature").GetString());
        Assert.Equal("tool_use", content[1].GetProperty("type").GetString());
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ARedactedBlockGoesBackAsItCame()
    {
        var (first, second, _) = await TwoRoundsAsync(Operator, stateChanges: false, redacted: true);

        var content = second.GetProperty("messages")[first.GetProperty("messages").GetArrayLength()].GetProperty("content");

        Assert.True(JsonElement.DeepEquals(
            JsonDocument.Parse("""{"type":"redacted_thinking","data":"ZW5jcnlwdGVk"}""").RootElement,
            content[0]));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ChangedStateFollowsTheToolResultsAndTheEarlierCopyStays()
    {
        var (first, second, _) = await TwoRoundsAsync(Operator, stateChanges: true);

        var messages = second.GetProperty("messages");
        var sentFirst = first.GetProperty("messages").GetArrayLength();

        Assert.Equal("Docked at Jameson Memorial.", messages[sentFirst - 1].GetProperty("content").GetString());
        Assert.Equal(sentFirst + 3, messages.GetArrayLength());
        var last = messages[messages.GetArrayLength() - 1];
        Assert.Equal("system", last.GetProperty("role").GetString());
        Assert.Equal("Undocked from Jameson Memorial.", last.GetProperty("content").GetString());
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task UnchangedStateIsNotSentAgain()
    {
        var (first, second, _) = await TwoRoundsAsync(Operator, stateChanges: false);

        Assert.Equal(first.GetProperty("messages").GetArrayLength() + 2, second.GetProperty("messages").GetArrayLength());
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheLastRoundKeepsItsToolsAndMayNotCallThem()
    {
        var (first, second, _) = await TwoRoundsAsync(Operator, stateChanges: false, maxToolRounds: 1);

        Assert.False(first.TryGetProperty("tool_choice", out _));
        Assert.Equal(Raw(first.GetProperty("tools")), Raw(second.GetProperty("tools")));
        Assert.Equal("none", second.GetProperty("tool_choice").GetProperty("type").GetString());
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheTranscriptKeepsNoThinking()
    {
        var (_, _, loop) = await TwoRoundsAsync(Operator, stateChanges: true);

        var parts = loop.History.SelectMany(message => message.Content).ToList();

        Assert.Contains(parts, part => part is ConversationContent.ToolUse);
        Assert.DoesNotContain(parts, part => part is ConversationContent.ThinkingBlock or ConversationContent.TrailingState);
    }

    [Theory]
    [InlineData(Operator)]
    [InlineData(Folding)]
    public void StateAnEarlierRoundSentIsRenderedAsItWasSent(string model)
    {
        var provider = new AnthropicLlmProvider("test-key");

        Assert.Equal(
            Anthropic(provider, model, History(sent: false, thinking: false), "Docked."),
            Anthropic(provider, model, History(sent: true, thinking: false), trailing: null));
    }

    [Fact]
    public void TheOpenAiProtocolsSendNoThinkingAndStateWhereItWasSent()
    {
        using var chat = new ChatCompletionsLlmProvider(apiKey: null, endpoint: null);
        using var responses = new ResponsesLlmProvider("sk-test", endpoint: null);

        Func<LlmRequest, ReadOnlyMemory<byte>>[] builders = [chat.BuildBody, responses.BuildBody];

        foreach (var build in builders)
        {
            Assert.Equal(
                OpenAi(build, History(sent: false, thinking: false), "Docked."),
                OpenAi(build, History(sent: true, thinking: true), trailing: null));
        }
    }

    /// <summary>An exchange and a question, with the state already on the question when it was sent.</summary>
    private static List<ConversationMessage> History(bool sent, bool thinking)
    {
        ConversationContent[] reasoning = thinking
            ? [new ConversationContent.ThinkingBlock("anthropic", """{"type":"thinking","thinking":"x","signature":"y"}""")]
            : [];
        ConversationContent[] state = sent ? [new ConversationContent.TrailingState("Docked.")] : [];

        return
        [
            new ConversationMessage(ConversationRole.User, "where am I"),
            new ConversationMessage(ConversationRole.Assistant, [.. reasoning, new ConversationContent.Text("Shinrarta Dezhra.")]),
            new ConversationMessage(ConversationRole.User, [new ConversationContent.Text("how far is Sol"), .. state]),
        ];
    }

    private static string Anthropic(AnthropicLlmProvider provider, string model, List<ConversationMessage> history, string? trailing) =>
        JsonSerializer.Serialize(provider.BuildParameters(new LlmRequest
        {
            Model = model,
            Effort = ThinkingEffort.Medium,
            Sampling = LlmSampling.Conversation,
            Prompt = new PromptAssembly { History = history, LiveGameState = trailing },
        }).Messages);

    private static string OpenAi(Func<LlmRequest, ReadOnlyMemory<byte>> build, List<ConversationMessage> history, string? trailing) =>
        System.Text.Encoding.UTF8.GetString(build(OpenAiRecordings.Ask() with
        {
            Prompt = new PromptAssembly { History = history, LiveGameState = trailing },
        }).Span);
}
