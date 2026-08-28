namespace D47.Core.Diagnostics;

/// <summary>
/// One log target per subsystem (Phase 1). A closed set, so the verbosity capability,
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
    /// Subsystem to the namespaces its loggers live under. This is the convention that lets a
    /// level change bind to a whole subsystem: log categories come from
    /// <c>ILogger&lt;T&gt;</c>, so the namespace is the routing key. Adding a subsystem means
    /// adding its namespaces here, and the namespaces are the thing that must not drift.
    /// <para>
    /// <b>Several namespaces per subsystem, because a subsystem is not a project.</b> This was a
    /// single namespace each, and three of the eight named namespaces that do not exist anywhere
    /// — a prefix matching no logger is never applied, so the Voice, Input and LLM level rows
    /// silently controlled nothing at all (bugs.md 1). The cause was not a typo so much as the
    /// shape: the speech loop alone spans the pipeline that drives it, the audio it plays and
    /// captures, the gate that decides when to listen, and three provider projects, and none of
    /// those facts fitted in one string.
    /// </para>
    /// <para>
    /// <b>Overlap is fine and is relied on.</b> <c>D47.App</c> covers everything the app logs,
    /// including <c>D47.App.Voice</c>, which Voice also claims. Serilog applies the
    /// <em>longest</em> matching prefix, so the more specific subsystem wins — which is what
    /// makes a broad catch-all and a narrow override able to coexist. <c>LogRoutingTests</c>
    /// asserts both halves: that every prefix here matches a real namespace, and that the
    /// specific one wins where they overlap.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> SourcePrefixes =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            // The catch-all for the surface. Deliberately broad: anything under D47.App that no
            // narrower subsystem claims belongs to the app.
            [App] = ["D47.App"],

            [Capabilities] = ["D47.Core.Capabilities"],
            [Settings] = ["D47.Core.Configuration"],
            [Journal] = ["D47.Core.Journal"],

            // The provider assembly. The turn loop in D47.Core.Conversation is deliberately not
            // here: it is the path a turn takes whichever provider answers, and it belongs to
            // the app's own level rather than to the one that names a vendor.
            [Llm] = ["D47.Llm"],

            // The speech loop, which is six namespaces across four projects: what drives it,
            // what it captures and plays, what decides when to listen, and the three providers
            // underneath.
            [Voice] =
            [
                "D47.App.Voice",
                "D47.Core.Audio",
                "D47.Core.Listening",
                "D47.Audio",
                "D47.Stt",
                "D47.Tts",
            ],

            // The runtime, the placement arithmetic, and the surfaces that host it. Only the
            // first of these was here, so turning VR down left two thirds of it talking.
            [Vr] = ["D47.Vr", "D47.Core.Vr", "D47.App.Headset"],

            // Key injection and the bindings it reads, on both sides of the seam, plus the HOTAS
            // switches that arrive through the same path.
            [Input] = ["D47.App.Input", "D47.Core.Input", "D47.Core.Hotas"],
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
