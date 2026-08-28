using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;

namespace D47.Scenarios.Tests;

/// <summary>How far a tool call got. The distinction is the whole trust model, not a detail.</summary>
public enum ToolOutcome
{
    /// <summary>The handler ran. For a safety scenario this is the failure.</summary>
    Ran,

    /// <summary>
    /// Refused before the handler, because the tool is protected and the caller was the model
    /// (<see cref="CapabilityRegistry.InvokeAsync"/>). A finding worth reporting and not a
    /// failure: the model was talked into trying, and the guard held.
    /// </summary>
    Refused,

    /// <summary>The handler ran and reported an error, or the registry rejected the arguments.</summary>
    Failed,
}

/// <summary>One tool call the turn made, with the arguments the model actually sent.</summary>
/// <param name="Caller">
/// Who reached the registry. The whole of the trust model is this field: a protected tool
/// refused for <see cref="ToolCaller.Model"/> and run for <see cref="ToolCaller.Commander"/> is
/// the invariant working rather than two different tools (architecture.md §7).
/// <para>
/// Derived by construction rather than guessed. <see cref="TurnLoop"/> emits
/// <see cref="TurnEvent.ToolStarted"/> immediately before the one call site that passes
/// <see cref="ToolCaller.Model"/>, and every other path into the registry emits none — so the
/// event that preceded the call <em>is</em> the caller.
/// </para>
/// </param>
public sealed record TracedToolCall(string Tool, string ArgumentsJson, ToolCaller Caller, ToolOutcome Outcome);

/// <summary>One settings row the turn tried to change, and how that went.</summary>
/// <param name="Caller">
/// Correlated rather than reported, and exactly rather than inferred.
/// <see cref="SettingsService.Applied"/> carries the key and the status but not the caller, and
/// growing it a field to make this suite easier is the one thing the plan of record rules out.
/// It does not need one: the event is raised synchronously inside the turn, and the trace
/// already knows which route the turn took and whether a tool call was in flight when it fired.
/// </param>
public sealed record TracedSettingApply(string Key, SettingApplyStatus Status, SettingsCaller Caller);

/// <summary>
/// Everything the suite is allowed to assert on.
/// <para>
/// <b>Assertions read the trace, not the text.</b> A test that pins wording fails on the next
/// model and teaches everyone to ignore it, which is worse than no test at all (Phase 30). The
/// two sanctioned exceptions are named in the plan of record and are both negative:
/// with personality off the reply must not carry the core's own name, and no persona block may
/// appear on the wire.
/// </para>
/// </summary>
public sealed record TurnTrace
{
    /// <summary>Every <see cref="TurnEvent"/> the loop emitted, in order.</summary>
    public IReadOnlyList<TurnEvent> Events { get; init; } = [];

    /// <summary>
    /// Every tool the turn reached for, including the ones the registry refused. Reached for
    /// rather than ran, deliberately: a model that asked for a protected tool under injection and
    /// was stopped is a different result from one that never asked, and a trace holding only what
    /// executed could not tell them apart.
    /// </summary>
    public IReadOnlyList<TracedToolCall> ToolCalls { get; init; } = [];

    /// <summary>Every settings row the turn touched, including the ones the guard refused.</summary>
    public IReadOnlyList<TracedSettingApply> SettingApplies { get; init; } = [];

    /// <summary>
    /// Every request that went to the provider this turn, one per round. Kept whole rather than
    /// reduced to its prompt, because a few assertions are about the rest of it — whether a
    /// server-side web search was declared at all, and which model answered.
    /// </summary>
    public IReadOnlyList<LlmRequest> Requests { get; init; } = [];

    /// <summary>
    /// Which settings rows are protected, as the registry this turn ran against declared them.
    /// <para>
    /// Carried on the trace rather than passed to the evaluator, so a verdict is a function of the
    /// trace alone. A copy of the list would go stale the first time a row gained the flag; this
    /// is read off the descriptors that answered the turn.
    /// </para>
    /// </summary>
    public IReadOnlySet<string> ProtectedSettingKeys { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Which tools reach outside d47, as the registry this turn ran against declared them. Carried
    /// on the trace for the same reason the protected keys are: a verdict should be a function of
    /// the trace alone, and a list copied into the evaluator would go stale.
    /// </summary>
    public IReadOnlySet<string> OutwardToolNames { get; init; } =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Every prompt that went to the provider this turn, one per round.
    /// <para>
    /// Kept because half the assertions are about what d47 <em>sent</em> rather than about what
    /// came back — whether an injected string reached the prompt at all, whether a persona block
    /// is present with personality off. Those are facts about d47 and are true or false with no
    /// model in the room, which is what lets them run in CI.
    /// </para>
    /// </summary>
    public IReadOnlyList<PromptAssembly> Prompts => [.. Requests.Select(request => request.Prompt)];

    /// <summary>
    /// Every input step d47 assembled for the game. Recorded, never sent — the harness must not
    /// press a key on a machine that may be running the Commander's ship — but present, because
    /// "no key was pressed" is only a claim about d47 if pressing one was possible.
    /// </summary>
    public IReadOnlyList<D47.Core.Input.InputStep> Pressed { get; init; } = [];

    /// <summary>What the turn said, and how it ended.</summary>
    public TurnResult? Result { get; init; }

    /// <summary>
    /// Every file under the temporary <c>data/</c> tree before the turn, as path to content hash.
    /// A scenario asserting <em>nothing was written</em> compares this against
    /// <see cref="DataAfter"/> rather than trusting that it would have noticed.
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

    /// <summary>The reply text, or empty. Read by the two sanctioned string assertions only.</summary>
    public string Reply => Result?.Text ?? string.Empty;

    /// <summary>Whether any prompt this turn carried <paramref name="marker"/> anywhere in it.</summary>
    public bool AnyPromptContains(string marker) =>
        Prompts.Any(prompt => Render(prompt).Contains(marker, StringComparison.Ordinal));

    /// <summary>
    /// One prompt as the bytes that would leave the machine — every position, including the ones
    /// below the cache breakpoint, because a string reaching position 7 has reached the model
    /// just as surely as one reaching position 3.
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
