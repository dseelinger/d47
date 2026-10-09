using D47.Core.Catalog;
using D47.Core.Conversation;
using D47.Llm.OpenAi;

using Xunit;

namespace D47.Llm.Tests;

/// <summary>Anthropic's model list, and how a model it lists that the catalog does not name is called.</summary>
[Trait("Category", "Integration")]
[Collection(nameof(EndpointDemotionCollection))]
public class AnthropicListsTheModelsTheKeyReachesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Model(string id, string created, bool adaptive) => $$"""
        {
          "type": "model",
          "id": "{{id}}",
          "display_name": "{{id}}",
          "created_at": "{{created}}",
          "max_input_tokens": 200000,
          "max_tokens": 64000,
          "capabilities": {
            "batch": { "supported": true },
            "citations": { "supported": true },
            "code_execution": { "supported": true },
            "context_management": {
              "supported": true,
              "clear_thinking_20251015": { "supported": true },
              "clear_tool_uses_20250919": { "supported": true },
              "compact_20260112": { "supported": true }
            },
            "effort": {
              "supported": {{(adaptive ? "true" : "false")}},
              "low": { "supported": true },
              "medium": { "supported": true },
              "high": { "supported": true },
              "xhigh": { "supported": true },
              "max": { "supported": true }
            },
            "image_input": { "supported": true },
            "pdf_input": { "supported": true },
            "structured_outputs": { "supported": true },
            "thinking": {
              "supported": true,
              "types": {
                "adaptive": { "supported": {{(adaptive ? "true" : "false")}} },
                "enabled": { "supported": true }
              }
            }
          }
        }
        """;

    private static string Page(params string[] models) =>
        $$"""{ "data": [{{string.Join(",", models)}}], "has_more": false, "first_id": null, "last_id": null }""";

    private static async Task<EndpointModels> ListAsync(string body)
    {
        using var endpoint = RecordedEndpoint.Json(body);

        return await new AnthropicLlmProvider("test-key", endpoint.BaseUrl).ListModelsAsync(Token);
    }

    [Fact]
    public async Task OnlyClaudeModelsAreListedWithWhatTheyThinkWith()
    {
        var listed = await ListAsync(Page(
            Model("claude-test-9", "2026-10-01T00:00:00Z", adaptive: false),
            Model("claude-sonnet-5", "2026-02-01T00:00:00Z", adaptive: true),
            Model("not-a-claude", "2026-10-01T00:00:00Z", adaptive: true)));

        Assert.Equal(EndpointReach.Answered, listed.Reach);
        Assert.Equal(["claude-sonnet-5", "claude-test-9"], listed.Ids);
        Assert.Equal(["claude-test-9", "claude-sonnet-5"], listed.Listed.Select(model => model.Id));
        Assert.False(listed.Listed[0].AdaptiveThinking);
        Assert.True(listed.Listed[1].AdaptiveThinking);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), listed.Listed[0].Created);
    }

    [Fact]
    public async Task AModelListedWithoutItsCapabilitiesSaysNothingAboutThinking()
    {
        var listed = await ListAsync(Page(
            """{ "type": "model", "id": "claude-bare-1", "display_name": "Claude Bare 1", "created_at": "2026-10-01T00:00:00Z" }"""));

        Assert.Null(Assert.Single(listed.Listed).AdaptiveThinking);
    }

    [Fact]
    public async Task ARefusedListListsNothing()
    {
        using var endpoint = RecordedEndpoint.Failing(
            401, """{ "type": "error", "error": { "type": "authentication_error", "message": "invalid x-api-key" } }""");

        var listed = await new AnthropicLlmProvider("test-key", endpoint.BaseUrl).ListModelsAsync(Token);

        Assert.Equal(EndpointReach.Refused, listed.Reach);
        Assert.Empty(listed.Listed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ANewModelIsSentByNameAndThinksAsListed(bool adaptive)
    {
        var listed = await ListAsync(Page(
            Model("claude-test-9", "2099-01-01T00:00:00Z", adaptive),
            Model("claude-sonnet-5", "2026-02-01T00:00:00Z", adaptive: true)));

        var source = new ModelCatalogSource(ModelCatalog.Embedded);
        source.List(LlmProviderCatalog.AnthropicId, listed.Listed);

        using var endpoint = RecordedEndpoint.Streaming(Recordings.OneWord());
        var provider = new AnthropicLlmProvider("test-key", endpoint.BaseUrl, source);
        var capabilities = provider.CapabilitiesFor("claude-test-9");

        await foreach (var _ in provider.StreamAsync(Recordings.Request("claude-test-9"), Token))
        {
        }

        Assert.False(capabilities.SupportsToolSearch);
        Assert.False(capabilities.SupportsOperatorSystemMessages);
        Assert.Equal(adaptive, capabilities.SupportsThinkingEffort);

        var sent = Assert.Single(endpoint.Requests);
        Assert.Contains("\"model\":\"claude-test-9\"", sent, StringComparison.Ordinal);
        Assert.Equal(adaptive, sent.Contains("\"thinking\"", StringComparison.Ordinal));
    }
}
