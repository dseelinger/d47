namespace D47.Core.Capabilities;

public enum SettingKind
{
    Text,
    Secret,
    Toggle,
    Choice,
    Number,
    Hotkey,
}

/// <summary>
/// A settings row, declared by the capability that owns it. The UI renders these rather
/// than holding its own list (architecture.md §5 D5).
/// </summary>
public sealed record SettingRow
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    /// <summary>The short form. The capability's documentation page is the long form.</summary>
    public required string Help { get; init; }

    public required SettingKind Kind { get; init; }

    /// <summary>
    /// Shown as a placeholder, never as a value, so a default is visually distinct from a
    /// choice the Commander actually made (list.md Phase 4).
    /// </summary>
    public string? DefaultDisplay { get; init; }

    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>
    /// Never settable through a tool the model can call — the panel, a hotkey and the
    /// model-free keyword router reach it, the LLM path does not (list.md Phase 4).
    /// Declared now so the flag exists before the first protected row does.
    /// </summary>
    public bool Protected { get; init; }
}
