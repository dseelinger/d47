namespace D47.Core.Diagnostics;

/// <summary>
/// One log target per subsystem (list.md Phase 1). A closed set, so the verbosity capability,
/// the generated settings rows and the model-free keyword router all draw on the same
/// vocabulary instead of accepting free text.
/// </summary>
public static class Subsystems
{
    public const string App = "App";
    public const string Capabilities = "Capabilities";
    public const string Settings = "Settings";
    public const string Journal = "Journal";
    public const string Llm = "Llm";
    public const string Voice = "Voice";
    public const string Vr = "Vr";
    public const string Input = "Input";

    public static readonly IReadOnlyList<string> All =
        [App, Capabilities, Settings, Journal, Llm, Voice, Vr, Input];

    /// <summary>
    /// Subsystem to the namespace its loggers live under. This is the convention that lets a
    /// level change bind to a whole subsystem: log categories come from
    /// <c>ILogger&lt;T&gt;</c>, so the namespace is the routing key. Adding a subsystem means
    /// adding its namespace here, and the namespace is the thing that must not drift.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> SourcePrefixes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [App] = "D47.App",
            [Capabilities] = "D47.Core.Capabilities",
            [Settings] = "D47.Core.Configuration",
            [Journal] = "D47.Core.Journal",
            [Llm] = "D47.Core.Llm",
            [Voice] = "D47.Voice",
            [Vr] = "D47.Vr",
            [Input] = "D47.Input",
        };

    /// <summary>
    /// How a subsystem is written for a person. The identifier stays C#-cased because it is a
    /// settings key and a log source prefix; only the label is fixed up.
    /// </summary>
    public static string DisplayName(string subsystem) => subsystem switch
    {
        Llm => "LLM",
        Vr => "VR",
        _ => subsystem,
    };

    /// <summary>Returns the canonical casing, or null when the name is not a subsystem.</summary>
    public static string? Canonical(string name) =>
        All.FirstOrDefault(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase));
}
