using System.Text.Json;
using D47.Core.Catalog;
using D47.Core.Conversation;
using D47.Llm.OpenAi;
using Xunit;

namespace D47.Llm.Tests;

/// <summary>A picture on a tool result reaches each provider in the shape its protocol takes.</summary>
[Collection(nameof(EndpointDemotionCollection))]
public class APictureRidesOnItsToolResultTests
{
    private const string ResultText = "A picture of the Commander's screen.";

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02, 0x03];

    private static readonly string Base64 = Convert.ToBase64String(Jpeg);

    private static readonly ModelCatalogSource Catalog = new(ModelCatalog.Parse("""
        {
          "schema": 1,
          "published": "2026-10-07",
          "providers": {
            "anthropic": {
              "default": "claude-sees-9",
              "models": [
                { "id": "claude-sees-9", "offered": true, "price": { "input": 3, "output": 15 }, "traits": { "images": true } },
                { "id": "claude-blind-9", "offered": true, "price": { "input": 3, "output": 15 }, "traits": { "toolSearch": true } }
              ]
            },
            "openai": {
              "default": "gpt-sees-9",
              "models": [
                { "id": "gpt-sees-9", "offered": true, "price": { "input": 2, "output": 10 }, "traits": { "images": true } },
                { "id": "gpt-blind-9", "offered": true, "price": { "input": 2, "output": 10 } }
              ]
            }
          },
          "speech": {
            "elevenlabs": { "default": "eleven-a", "models": [ { "id": "eleven-a", "offered": true } ] },
            "openai": { "default": "tts-a", "models": [ { "id": "tts-a", "offered": true } ] },
            "cartesia": { "default": "sonic-a", "models": [ { "id": "sonic-a", "offered": true } ] }
          }
        }
        """));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public APictureRidesOnItsToolResultTests() => EndpointDemotions.Clear();

    [Fact]
    public void AnthropicSendsTheTextThenTheImageInsideTheToolResult()
    {
        var provider = new AnthropicLlmProvider("test-key", catalog: Catalog);

        using var messages = JsonDocument.Parse(
            JsonSerializer.Serialize(provider.BuildParameters(Request("claude-sees-9")).Messages));

        var result = messages.RootElement[2].GetProperty("content")[0];
        var content = result.GetProperty("content");

        Assert.Equal("tool_result", result.GetProperty("type").GetString());
        Assert.Equal(2, content.GetArrayLength());
        Assert.Equal("text", content[0].GetProperty("type").GetString());
        Assert.Equal(ResultText, content[0].GetProperty("text").GetString());
        Assert.Equal("image", content[1].GetProperty("type").GetString());

        var source = content[1].GetProperty("source");

        Assert.Equal("base64", source.GetProperty("type").GetString());
        Assert.Equal("image/jpeg", source.GetProperty("media_type").GetString());
        Assert.Equal(Base64, source.GetProperty("data").GetString());
    }

    [Fact]
    public void AnthropicReadsWhetherAModelSeesFromTheCatalog()
    {
        var provider = new AnthropicLlmProvider("test-key", catalog: Catalog);

        Assert.True(provider.CapabilitiesFor("claude-sees-9").SupportsImages);
        Assert.False(provider.CapabilitiesFor("claude-blind-9").SupportsImages);
        Assert.False(provider.CapabilitiesFor("claude-not-in-the-catalog").SupportsImages);
    }

    [Fact]
    public void ResponsesSendsAnInputTextThenAnInputImageAsTheOutput()
    {
        using var provider = new ResponsesLlmProvider("sk-test", endpoint: null, catalog: Catalog);

        using var body = JsonDocument.Parse(provider.BuildBody(Request("gpt-sees-9")).ToArray());

        var output = body.RootElement.GetProperty("input").EnumerateArray()
            .Single(item => item.TryGetProperty("type", out var type) && type.GetString() == "function_call_output")
            .GetProperty("output");

        Assert.Equal(2, output.GetArrayLength());
        Assert.Equal("input_text", output[0].GetProperty("type").GetString());
        Assert.Equal(ResultText, output[0].GetProperty("text").GetString());
        Assert.Equal("input_image", output[1].GetProperty("type").GetString());
        Assert.Equal($"data:image/jpeg;base64,{Base64}", output[1].GetProperty("image_url").GetString());

        Assert.True(provider.CapabilitiesFor("gpt-sees-9").SupportsImages);
        Assert.False(provider.CapabilitiesFor("gpt-blind-9").SupportsImages);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ResponsesDoesNotTakeARefusedPictureForARefusalOfTools()
    {
        using var endpoint = RecordedEndpoint.Failing(
            400,
            OpenAiRecordings.Refusal("Images in tool outputs are not supported for this model.", null));

        using var provider = new ResponsesLlmProvider("sk-test", endpoint.BaseUrl, catalog: Catalog);

        var events = await OpenAiRecordings.DrainAsync(provider, Request("gpt-sees-9"), Token);

        Assert.Single(events.OfType<LlmStreamEvent.Failed>());
        Assert.Single(endpoint.Requests);
        Assert.True(provider.CapabilitiesFor("gpt-sees-9").SupportsToolCalls);
    }

    [Fact]
    public void ChatCompletionsFollowsTheToolMessageWithAUserMessageHoldingThePicture()
    {
        using var provider = new ChatCompletionsLlmProvider(apiKey: null, "http://127.0.0.1:11434/v1");

        Assert.True(provider.CapabilitiesFor("llava").SupportsImages);

        using var body = JsonDocument.Parse(provider.BuildBody(Request("llava")).ToArray());
        var messages = body.RootElement.GetProperty("messages").EnumerateArray().ToList();
        var tool = messages.FindIndex(message => message.GetProperty("role").GetString() == "tool");

        Assert.Equal(ResultText, messages[tool].GetProperty("content").GetString());

        var picture = messages[tool + 1];
        var part = Assert.Single(picture.GetProperty("content").EnumerateArray());

        Assert.Equal("user", picture.GetProperty("role").GetString());
        Assert.Equal("image_url", part.GetProperty("type").GetString());
        Assert.Equal($"data:image/jpeg;base64,{Base64}", part.GetProperty("image_url").GetProperty("url").GetString());
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ARefusedPictureIsDroppedTheRoundAskedOnceMoreAndTheModelToldWhy()
    {
        using var endpoint = RecordedEndpoint.RefusingThenStreaming(
            400,
            OpenAiRecordings.Refusal("image input is not supported - hint: you may need to provide the mmproj", null),
            OpenAiRecordings.Chat.TextDelta("I cannot see it."),
            OpenAiRecordings.Chat.Finish("stop"),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var events = await OpenAiRecordings.DrainAsync(provider, Request("qwen3:30b"), Token);

        Assert.Empty(events.OfType<LlmStreamEvent.Failed>());
        Assert.Equal(2, endpoint.Requests.Count);
        Assert.Contains("image_url", endpoint.Requests[0], StringComparison.Ordinal);
        Assert.DoesNotContain("image_url", endpoint.Requests[1], StringComparison.Ordinal);
        Assert.DoesNotContain(Base64, endpoint.Requests[1], StringComparison.Ordinal);

        using var retried = JsonDocument.Parse(endpoint.Requests[1]);
        var tool = retried.RootElement.GetProperty("messages").EnumerateArray()
            .Single(message => message.GetProperty("role").GetString() == "tool");

        Assert.Equal(
            $"{ResultText}\n\nThe model at this endpoint cannot read pictures.",
            tool.GetProperty("content").GetString());

        Assert.False(provider.CapabilitiesFor("qwen3:30b").SupportsImages);
        Assert.True(provider.CapabilitiesFor("llava").SupportsImages);
        Assert.True(provider.CapabilitiesFor("qwen3:30b").SupportsToolCalls);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ARefusalThatEchoesThePictureDoesNotRepeatIt()
    {
        using var endpoint = RecordedEndpoint.Failing(
            400,
            $"Bad request: could not decode data:image/jpeg;base64,{Base64} as an image");

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var events = await OpenAiRecordings.DrainAsync(provider, Request("qwen3:30b"), Token);
        var failed = Assert.Single(events.OfType<LlmStreamEvent.Failed>());

        Assert.DoesNotContain(Base64, failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARefusalOfAnotherFieldIsNotTakenForAPicture()
    {
        Assert.False(ChatCompletionsLlmProvider.RefusedPicture("Unrecognized request argument supplied: reasoning_effort"));
        Assert.True(ChatCompletionsLlmProvider.RefusedPicture("Model qwen3 is not a multimodal model"));
    }

    private static LlmRequest Request(string model) => OpenAiRecordings.Ask(model) with
    {
        Prompt = new PromptAssembly
        {
            History =
            [
                new ConversationMessage(ConversationRole.User, "what's on my scanner"),
                new ConversationMessage(
                    ConversationRole.Assistant,
                    [new ConversationContent.ToolUse("call_1", "look_at_screen", "{}")]),
                new ConversationMessage(
                    ConversationRole.User,
                    [
                        new ConversationContent.ToolResult("call_1", ResultText, IsError: false)
                        {
                            Image = new ImageAttachment(Jpeg, "image/jpeg", 1280, 720),
                        },
                    ]),
            ],
        },
    };
}
