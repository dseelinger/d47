using D47.Core.Conversation;

namespace D47.Llm.OpenAi;

/// <summary>
/// One turn of the conversation, flattened into the pieces both OpenAI protocols need and
/// neither of them arranges the same way (list.md Phase 29).
/// <para>
/// Results come before text because a message carrying tool results has to open with them, and
/// calls come last because they are what the assistant said it wanted next.
/// </para>
/// </summary>
internal sealed record WireTurn(
    bool IsAssistant,
    IReadOnlyList<ConversationContent.ToolResult> Results,
    string? Text,
    IReadOnlyList<ConversationContent.ToolUse> Calls);

/// <summary>
/// Turning an assembled prompt into wire turns. Shared by both decoders, which is where the
/// sharing between them stops being cosmetic: the <em>decisions</em> are here — how live game
/// state is attached, what a turn with both text and tool calls looks like — and only the
/// serialisation differs below.
/// </summary>
internal static class OpenAiPrompt
{
    /// <summary>
    /// The conversation, with live game state attached (position 7, below the cache breakpoint).
    /// <para>
    /// <paramref name="operatorRoleAvailable"/> chooses how. A privileged trailing message
    /// carries operator authority that journal content cannot spoof; a
    /// <c>&lt;system-reminder&gt;</c> folded into the conversation caches identically but is
    /// only a convention. Either way it sits after everything cached, so attaching it costs no
    /// prefix.
    /// </para>
    /// </summary>
    /// <param name="trailingState">
    /// The state to send as its own privileged message, or null when it was folded in instead.
    /// </param>
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

        // The fallback. A turn of its own rather than folded into the previous message's text,
        // which is the shape Anthropic needs and this one does not: there, a second user message
        // in a row is a protocol error and would land between a tool_use and the result
        // answering it. Here the tool results are already their own messages, so a plain user
        // turn after them is both legal and the least surprising thing on the wire.
        var reminder = $"<system-reminder>\n{prompt.LiveGameState}\n</system-reminder>";

        turns.Add(new WireTurn(IsAssistant: false, [], reminder, []));

        return turns;
    }
}
