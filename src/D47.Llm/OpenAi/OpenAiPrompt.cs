using D47.Core.Conversation;

namespace D47.Llm.OpenAi;

/// <summary>
/// One turn of the conversation, flattened into the pieces both OpenAI protocols need and neither of
/// them arranges the same way (Phase 29).
/// </summary>
internal sealed record WireTurn(
    bool IsAssistant,
    IReadOnlyList<ConversationContent.ToolResult> Results,
    string? Text,
    IReadOnlyList<ConversationContent.ToolUse> Calls);

/// <summary>Turning an assembled prompt into wire turns.</summary>
internal static class OpenAiPrompt
{
    /// <summary>
    /// The conversation, with live game state attached (position 7, below the cache breakpoint).
    /// </summary>
    /// <paramref name="operatorRoleAvailable"/>chooses how.</paramref>
    public static IReadOnlyList<WireTurn> Flatten(
        PromptAssembly prompt,
        bool operatorRoleAvailable,
        out string? trailingState)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var turns = new List<WireTurn>(prompt.History.Count + 1);

        foreach (var message in prompt.History)
        {
            var text = new System.Text.StringBuilder();
            var calls = new List<ConversationContent.ToolUse>();
            var results = new List<ConversationContent.ToolResult>();

            foreach (var part in message.Content)
            {
                switch (part)
                {
                    case ConversationContent.Text value:
                        if (text.Length > 0)
                        {
                            text.Append('\n');
                        }

                        text.Append(value.Value);
                        break;

                    case ConversationContent.ToolUse call:
                        calls.Add(call);
                        break;

                    case ConversationContent.ToolResult result:
                        results.Add(result);
                        break;
                }
            }

            turns.Add(new WireTurn(
                message.Role == ConversationRole.Assistant,
                results,
                text.Length > 0 ? text.ToString() : null,
                calls));
        }

        trailingState = null;

        if (string.IsNullOrWhiteSpace(prompt.LiveGameState))
        {
            return turns;
        }

        if (operatorRoleAvailable)
        {
            trailingState = prompt.LiveGameState;
            return turns;
        }

        // The fallback.
        var reminder = $"<system-reminder>\n{prompt.LiveGameState}\n</system-reminder>";

        turns.Add(new WireTurn(IsAssistant: false, [], reminder, []));

        return turns;
    }
}
