using System.Text.Json;
using D47.Core.Conversation;
using D47.Llm.OpenAi;
using Xunit;

namespace D47.Llm.Tests;

/// <summary>
/// On Chat Completions the game state is appended to the last message rather than sent as a user message of
/// its own, which a small model answers in place of the Commander's question (#420).
/// </summary>
[Collection(nameof(EndpointDemotionCollection))]
public class GameStateRidesInTheLastMessageTests
{
    private const string State = "Fuel 12.4 of 32 tonnes.";

    public GameStateRidesInTheLastMessageTests() => EndpointDemotions.Clear();

    [Fact]
    public void TheCommandersQuestionCarriesTheReminder()
    {
        var messages = Messages(
        [
            new ConversationMessage(ConversationRole.User, "Where am I?"),
            new ConversationMessage(ConversationRole.Assistant, "Shinrarta Dezhra."),
            new ConversationMessage(ConversationRole.User, "How much fuel do I have?"),
        ], State);

        AssertNoTwoUserMessagesInARow(messages);

        var last = messages[^1];
        Assert.Equal("user", last.GetProperty("role").GetString());

        var content = last.GetProperty("content").GetString()!;
        Assert.StartsWith("How much fuel do I have?", content, StringComparison.Ordinal);
        Assert.EndsWith($"<system-reminder>\n{State}\n</system-reminder>", content, StringComparison.Ordinal);
    }

    [Fact]
    public void AToolAnswerCarriesTheReminderAndNothingFollowsIt()
    {
        var messages = Messages(
        [
            new ConversationMessage(ConversationRole.User, "How much fuel do I have?"),
            new ConversationMessage(
                ConversationRole.Assistant,
                [new ConversationContent.ToolUse("call_1", "get_status", "{}")]),
            new ConversationMessage(
                ConversationRole.User,
                [new ConversationContent.ToolResult("call_1", State, IsError: false)]),
        ], "Docked at Jameson Memorial.");

        var last = messages[^1];
        Assert.Equal("tool", last.GetProperty("role").GetString());

        var content = last.GetProperty("content").GetString()!;
        Assert.StartsWith(State, content, StringComparison.Ordinal);
        Assert.EndsWith(
            "<system-reminder>\nDocked at Jameson Memorial.\n</system-reminder>",
            content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheStoredToolResultIsNotChanged()
    {
        var result = new ConversationContent.ToolResult("call_1", State, IsError: false);

        Messages(
        [
            new ConversationMessage(ConversationRole.User, "How much fuel do I have?"),
            new ConversationMessage(
                ConversationRole.Assistant,
                [new ConversationContent.ToolUse("call_1", "get_status", "{}")]),
            new ConversationMessage(ConversationRole.User, [result]),
        ], "Docked at Jameson Memorial.");

        Assert.Equal(State, result.Content);
    }

    [Fact]
    public void WithNoStateNothingIsAppended()
    {
        var messages = Messages([new ConversationMessage(ConversationRole.User, "How much fuel do I have?")], null);

        Assert.Equal("How much fuel do I have?", messages[^1].GetProperty("content").GetString());
    }

    private static JsonElement[] Messages(IReadOnlyList<ConversationMessage> history, string? state)
    {
        using var provider = new ChatCompletionsLlmProvider(apiKey: null, "http://127.0.0.1:1234/v1");

        var body = provider.BuildBody(OpenAiRecordings.Ask() with
        {
            Prompt = new PromptAssembly { History = history, LiveGameState = state },
        });

        using var document = JsonDocument.Parse(body);

        return [.. document.RootElement.GetProperty("messages").EnumerateArray().Select(message => message.Clone())];
    }

    private static void AssertNoTwoUserMessagesInARow(JsonElement[] messages)
    {
        for (var i = 1; i < messages.Length; i++)
        {
            Assert.False(
                messages[i].GetProperty("role").GetString() == "user"
                    && messages[i - 1].GetProperty("role").GetString() == "user",
                $"messages {i - 1} and {i} are both user messages");
        }
    }
}
