namespace D47.Core.Capabilities;

public enum ToolParameterType
{
    String,
    Integer,
    Number,
    Boolean,
}

/// <summary>One tool input.</summary>
public sealed record ToolParameter
{
    public required string Name { get; init; }

    public required ToolParameterType Type { get; init; }

    public required string Description { get; init; }

    public bool Required { get; init; }

    /// <summary>A closed vocabulary.</summary>
    public IReadOnlyList<string> AllowedValues { get; init; } = [];
}

public sealed record ToolResult
{
    public required bool IsError { get; init; }

    public required string Content { get; init; }

    /// <summary>Spoken to the Commander as written; the model turn ends without the model reporting it.</summary>
    public bool Relayed { get; init; }

    public static ToolResult Ok(string content) => new() { IsError = false, Content = content };

    public static ToolResult Relay(string content) =>
        new() { IsError = false, Content = content, Relayed = true };

    public static ToolResult Error(string content) => new() { IsError = true, Content = content };
}

public delegate Task<ToolResult> ToolHandler(ToolArguments arguments, CancellationToken cancellationToken);

/// <summary>
/// A phrase that reaches one tool with one fixed set of arguments — the closed grammar the keyword
/// router was always going to need (Phase 10, "Control flight and navigation by voice").
/// </summary>
public sealed record ToolCommandPhrase(string Phrase, IReadOnlyDictionary<string, string> Arguments)
{
    /// <summary>Whether this phrase is live right now.</summary>
    public Func<bool>? When { get; init; }
}

public sealed record ToolDefinition
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public IReadOnlyList<ToolParameter> Parameters { get; init; } = [];

    /// <summary>Answerable while a turn is already running, rather than queued behind it.</summary>
    public bool Interrupting { get; init; }

    /// <summary>
    /// Phrases the model-free router accepts for this tool, each carrying the arguments it means.
    /// </summary>
    public IReadOnlyList<ToolCommandPhrase> Commands { get; init; } = [];

    /// <summary>
    /// Never callable by the model — the panel, a hotkey and the model-free keyword router reach it,
    /// the LLM path does not.
    /// </summary>
    public bool Protected { get; init; }

    public required ToolHandler Handler { get; init; }
}
