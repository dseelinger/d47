namespace D47.Core.Capabilities;

/// <summary>How a capability presents itself on the panel.</summary>
public sealed record CapabilityDisplay
{
    public string? PanelTitle { get; init; }

    public int Order { get; init; } = 100;

    public bool ShowOnPanel { get; init; } = true;

    /// <summary>Whether the settings card starts collapsed on a panel that has never been touched.</summary>
    public bool StartCollapsed { get; init; }
}

/// <summary>One capability, declared once at startup and never mutated.</summary>
public sealed record CapabilityDescriptor
{
    /// <summary>Stable kebab-case slug.</summary>
    public required string Id { get; init; }

    public required string Group { get; init; }

    public required string Name { get; init; }

    /// <summary>One line.</summary>
    public required string Summary { get; init; }

    public IReadOnlyList<string> Examples { get; init; } = [];

    /// <summary>Vocabulary for the model-free keyword router.</summary>
    public IReadOnlyList<CapabilityKeyword> Keywords { get; init; } = [];

    /// <summary>Phrases that only mean anything when they were spoken.</summary>
    public IReadOnlyList<CapabilityKeyword> SpokenKeywords { get; init; } = [];

    /// <summary>Phrases that only mean anything while there is something to interrupt.</summary>
    public IReadOnlyList<string> InterruptKeywords { get; init; } = [];

    public IReadOnlyList<ToolDefinition> Tools { get; init; } = [];

    public IReadOnlyList<SettingRow> Settings { get; init; } = [];

    public CapabilityDisplay Display { get; init; } = new();
}
