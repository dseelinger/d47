namespace D47.Core.Diagnostics;

/// <summary>One log target per subsystem (Phase 1).</summary>
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

    /// <summary>Subsystem to the namespaces its loggers live under.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> SourcePrefixes =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            // The catch-all for the surface.
            [App] = ["D47.App"],

            [Capabilities] = ["D47.Core.Capabilities"],
            [Settings] = ["D47.Core.Configuration"],
            [Journal] = ["D47.Core.Journal"],

            // The provider assembly.
            [Llm] = ["D47.Llm"],

            // The speech loop, which is six namespaces across four projects: what drives it, what it captures
            // and plays, what decides when to listen, and the three providers underneath.
            [Voice] =
            [
                "D47.App.Voice",
                "D47.Core.Audio",
                "D47.Core.Listening",
                "D47.Audio",
                "D47.Stt",
                "D47.Tts",
            ],

            // The runtime, the placement arithmetic, and the surfaces that host it.
            [Vr] = ["D47.Vr", "D47.Core.Vr", "D47.App.Headset"],

            // Key injection and the bindings it reads, on both sides of the seam, plus the HOTAS switches
            // that arrive through the same path.
            [Input] = ["D47.App.Input", "D47.Core.Input", "D47.Core.Hotas"],
        };

    /// <summary>How a subsystem is written for a person.</summary>
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
