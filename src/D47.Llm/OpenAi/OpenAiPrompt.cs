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
    IReadOnlyList<ConversationContent.ToolUse> Calls,
    bool IsOperator = false);

/// <summary>Turning an assembled prompt into wire turns.</summary>
internal static class OpenAiPrompt
{
    /// <summary>
    /// The conversation, with live game state attached (position 7, below the cache breakpoint), and state an
    /// earlier round of this turn sent attached where that round put it. <paramref name="operatorRoleAvailable"/>
    /// chooses how.
    /// </summary>
    public static IReadOnlyList<WireTurn> Flatten(PromptAssembly prompt, bool operatorRoleAvailable)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var turns = new List<WireTurn>(prompt.History.Count + 1);

        foreach (var message in prompt.History)
        {
            // Provider blocks are never sent over the OpenAI protocols; a message holding only those is skipped.
            if (message.Content.Count > 0
                && message.Content.All(part => part is ConversationContent.Opaque or ConversationContent.ThinkingBlock))
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
                    case ConversationContent.ThinkingBlock:
                    case ConversationContent.TrailingState:
                        break;
                }
            }

            turns.Add(new WireTurn(
                message.Role == ConversationRole.Assistant,
                results,
                text.Length > 0 ? text.ToString() : null,
                calls));

            foreach (var sent in message.Content.OfType<ConversationContent.TrailingState>())
            {
                Attach(turns, sent.Value, operatorRoleAvailable);
            }
        }

        Attach(turns, prompt.TrailingState, operatorRoleAvailable);

        return turns;
    }

    private static void Attach(List<WireTurn> turns, string? state, bool operatorRoleAvailable)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return;
        }

        if (operatorRoleAvailable)
        {
            turns.Add(new WireTurn(IsAssistant: false, [], state, [], IsOperator: true));
            return;
        }

        // The fallback: the reminder goes inside the last message the model reads, so a strict chat template
        // sees alternating roles and a small model answers the Commander or the tool rather than the reminder.
        var reminder = $"<system-reminder>\n{state}\n</system-reminder>";

        if (turns.Count > 0 && turns[^1] is { IsAssistant: false } last)
        {
            if (last.Text is { } text)
            {
                turns[^1] = last with { Text = $"{text}\n\n{reminder}" };
                return;
            }

            if (last.Results.Count > 0)
            {
                var results = last.Results.ToList();
                results[^1] = results[^1] with { Content = $"{results[^1].Content}\n\n{reminder}" };
                turns[^1] = last with { Results = results };
                return;
            }
        }

        turns.Add(new WireTurn(IsAssistant: false, [], reminder, []));
    }
}
