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
            // Opaque blocks are never sent over the OpenAI protocols; a message holding only opaque blocks is skipped.
            if (message.Content.Count > 0 && message.Content.All(part => part is ConversationContent.Opaque))
            {
                continue;
            }

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

                    case ConversationContent.Opaque:
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

        if (string.IsNullOrWhiteSpace(prompt.TrailingState))
        {
            return turns;
        }

        if (operatorRoleAvailable)
        {
            trailingState = prompt.TrailingState;
            return turns;
        }

        // The fallback: the reminder goes inside the last message the model reads, so a strict chat template
        // sees alternating roles and a small model answers the Commander or the tool rather than the reminder.
        var reminder = $"<system-reminder>\n{prompt.TrailingState}\n</system-reminder>";

        if (turns.Count > 0 && turns[^1] is { IsAssistant: false } last)
        {
            if (last.Text is { } text)
            {
                turns[^1] = last with { Text = $"{text}\n\n{reminder}" };
                return turns;
            }

            if (last.Results.Count > 0)
            {
                var results = last.Results.ToList();
                results[^1] = results[^1] with { Content = $"{results[^1].Content}\n\n{reminder}" };
                turns[^1] = last with { Results = results };
                return turns;
            }
        }

        turns.Add(new WireTurn(IsAssistant: false, [], reminder, []));

        return turns;
    }
}
