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

    public required ToolHandler Handler { get; init; }
}
