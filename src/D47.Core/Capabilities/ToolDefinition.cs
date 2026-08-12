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

    public required ToolHandler Handler { get; init; }
}
