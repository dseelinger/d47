using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;

namespace D47.Scenarios.Tests;

/// <summary>How far a tool call got.</summary>
public enum ToolOutcome
{
    /// <summary>The handler ran.</summary>
    Ran,

    /// <summary>
    /// Refused before the handler, because the tool is protected and the caller was the model (<see
    /// cref="CapabilityRegistry.InvokeAsync"/>).
    /// </summary>
    Refused,

    /// <summary>The handler ran and reported an error, or the registry rejected the arguments.</summary>
    Failed,
}

/// <summary>One tool call the turn made, with the arguments the model actually sent.</summary>
/// <param name="Caller">Who reached the registry.</param>
public sealed record TracedToolCall(string Tool, string ArgumentsJson, ToolCaller Caller, ToolOutcome Outcome);

/// <summary>One settings row the turn tried to change, and how that went.</summary>
public sealed record TracedSettingApply(string Key, SettingApplyStatus Status, SettingsCaller Caller);

/// <summary>Everything the suite is allowed to assert on.</summary>
public sealed record TurnTrace
{
    /// <summary>Every <see cref="TurnEvent"/> the loop emitted, in order.</summary>
    public IReadOnlyList<TurnEvent> Events { get; init; } = [];

    /// <summary>Every tool the turn reached for, including the ones the registry refused.</summary>
    public IReadOnlyList<TracedToolCall> ToolCalls { get; init; } = [];

    /// <summary>Every settings row the turn touched, including the ones the guard refused.</summary>
    public IReadOnlyList<TracedSettingApply> SettingApplies { get; init; } = [];

    /// <summary>Every request that went to the provider this turn, one per round.</summary>
    public IReadOnlyList<LlmRequest> Requests { get; init; } = [];

    /// <summary>Which settings rows are protected, as the registry this turn ran against declared them.</summary>
    public IReadOnlySet<string> ProtectedSettingKeys { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Which tools reach outside d47, as the registry this turn ran against declared them.</summary>
    public IReadOnlySet<string> OutwardToolNames { get; init; } =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Every prompt that went to the provider this turn, one per round.</summary>
    public IReadOnlyList<PromptAssembly> Prompts => [.. Requests.Select(request => request.Prompt)];

    /// <summary>Every input step d47 assembled for the game.</summary>
    public IReadOnlyList<D47.Core.Input.InputStep> Pressed { get; init; } = [];

    /// <summary>What the turn said, and how it ended.</summary>
    public TurnResult? Result { get; init; }

    /// <summary>
    /// Every file under the temporary <c>data/</c> tree before the turn, as path to content hash.
    /// </summary>
    public IReadOnlyDictionary<string, string> DataBefore { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> DataAfter { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Paths whose bytes differ between the two snapshots, added or changed.</summary>
    public IReadOnlyList<string> DataWrites =>
        [.. DataAfter
            .Where(after => !DataBefore.TryGetValue(after.Key, out var before) || before != after.Value)
            .Select(after => after.Key)
            .Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The reply text, or empty.</summary>
    public string Reply => Result?.Text ?? string.Empty;

    /// <summary>Whether any prompt this turn carried <paramref name="marker"/> anywhere in it.</summary>
    public bool AnyPromptContains(string marker) =>
        Prompts.Any(prompt => Render(prompt).Contains(marker, StringComparison.Ordinal));

    /// <summary>
    /// One prompt as the bytes that would leave the machine — every position, including the ones below
    /// the cache breakpoint, because a string reaching position 7 has reached the model just as surely
    /// as one reaching position 3.
    /// </summary>
    public static string Render(PromptAssembly prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var text = new System.Text.StringBuilder(prompt.RenderCachedSystemBlock());

        foreach (var tool in prompt.Tools)
        {
            text.Append('\n').Append(tool.Name)
                .Append('\n').Append(tool.Description)
                .Append('\n').Append(tool.InputSchemaJson);
        }

        foreach (var message in prompt.History)
        {
            text.Append('\n').Append(Flatten(message));
        }

        if (prompt.LiveGameState is { } state)
        {
            text.Append('\n').Append(state);
        }

        return text.ToString();
    }

    private static string Flatten(ConversationMessage message)
    {
        var text = new System.Text.StringBuilder();

        foreach (var content in message.Content)
        {
            switch (content)
            {
                case ConversationContent.Text plain:
                    text.Append(plain.Value).Append('\n');
                    break;
                case ConversationContent.ToolUse use:
                    text.Append(use.Name).Append(' ').Append(use.InputJson).Append('\n');
                    break;
                case ConversationContent.ToolResult result:
                    text.Append(result.Content).Append('\n');
                    break;
            }
        }

        return text.ToString();
    }
}
