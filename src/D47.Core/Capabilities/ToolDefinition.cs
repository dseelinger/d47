namespace D47.Core.Capabilities;

public enum ToolParameterType
{
    String,
    Integer,
    Number,
    Boolean,
}

/// <summary>
/// One tool input. Deliberately flat scalars: the schema model and the argument model change
/// together, and nothing in the checklist yet needs a nested object.
/// </summary>
public sealed record ToolParameter
{
    public required string Name { get; init; }

    public required ToolParameterType Type { get; init; }

    public required string Description { get; init; }

    public bool Required { get; init; }

    /// <summary>
    /// A closed vocabulary. Emitted as JSON Schema <c>enum</c> and validated before the
    /// handler runs, so a hallucinated value never reaches capability code.
    /// </summary>
    public IReadOnlyList<string> AllowedValues { get; init; } = [];
}

public sealed record ToolResult
{
    public required bool IsError { get; init; }

    public required string Content { get; init; }

    public static ToolResult Ok(string content) => new() { IsError = false, Content = content };

    public static ToolResult Error(string content) => new() { IsError = true, Content = content };
}

public delegate Task<ToolResult> ToolHandler(ToolArguments arguments, CancellationToken cancellationToken);

/// <summary>
/// A phrase that reaches one tool with one fixed set of arguments — the closed grammar the
/// keyword router was always going to need (list.md Phase 10, "Control flight and navigation by
/// voice").
/// <para>
/// Nothing is extracted from what the Commander said. The phrase is declared, the arguments are
/// declared beside it, and the router either matches the whole utterance or does not. That is
/// what separates this from a router guessing at arguments: "gear down" does not mean d47 parsed
/// "gear", it means somebody wrote down that this phrase performs this action.
/// </para>
/// <para>
/// Deliberately not part of the tool's schema. The model never sees these, so adding a phrase
/// cannot change a byte of the cached prefix (architecture.md §6).
/// </para>
/// </summary>
public sealed record ToolCommandPhrase(string Phrase, IReadOnlyDictionary<string, string> Arguments);

public sealed record ToolDefinition
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public IReadOnlyList<ToolParameter> Parameters { get; init; } = [];

    /// <summary>
    /// Answerable while a turn is already running, rather than queued behind it.
    /// <para>
    /// Almost nothing should be. A second question asked mid-turn is a second turn and belongs
    /// in line. This is for the handful of commands whose entire purpose is to interrupt what
    /// is already happening — <c>stop_speaking</c> is the first and, for now, the only one. An
    /// instant-silence command that waits for the turn it is trying to silence is not one
    /// (list.md Phase 5, "never gated behind a turn completing").
    /// </para>
    /// <para>
    /// Declared on the tool rather than checked by name at the call site, so the surface asks
    /// the registry what interrupts instead of holding its own opinion about it.
    /// </para>
    /// </summary>
    public bool Interrupting { get; init; }

    /// <summary>
    /// Phrases the model-free router accepts for this tool, each carrying the arguments it
    /// means. Usually empty: a tool with required parameters is otherwise unreachable without a
    /// model, and for most tools that is correct.
    /// </summary>
    public IReadOnlyList<ToolCommandPhrase> Commands { get; init; } = [];

    /// <summary>
    /// Never callable by the model — the panel, a hotkey and the model-free keyword router reach
    /// it, the LLM path does not.
    /// <para>
    /// The same rule <see cref="SettingRow.Protected"/> already carries, and the same reason:
    /// journal text and in-game messages are untrusted (architecture.md §7), so anything the model
    /// can call, a hostile in-game message can attempt to invoke. It arrived on tools when the
    /// checklist did (list.md Phase 17), which is the first capability whose <em>write</em> path
    /// has to be model-free while its read path stays open.
    /// </para>
    /// <para>
    /// Enforced in two places because advertising and calling are two different things.
    /// <see cref="Conversation.ToolProfiles"/> leaves it out of the advertisement, so no cached
    /// prefix pays for a tool that would refuse; and <see cref="CapabilityRegistry.InvokeAsync"/>
    /// refuses it outright when the caller is the model, so a name recalled from anywhere else
    /// still cannot reach the handler. Advertisement alone would be a lock on the front door and
    /// none on the back.
    /// </para>
    /// </summary>
    public bool Protected { get; init; }

    public required ToolHandler Handler { get; init; }
}
