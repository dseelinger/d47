using Microsoft.Extensions.Logging;

namespace D47.Core.Configuration;

/// <summary>
/// The settings store's whole shape. Anything not declared here is an unknown key and is
/// rejected on load (list.md Phase 1).
/// </summary>
public sealed record D47Settings
{
    public int SchemaVersion { get; init; } = 1;

    public LoggingSettings Logging { get; init; } = new();

    public LlmSettings Llm { get; init; } = new();

    public UiSettings Ui { get; init; } = new();

    public HotkeySettings Hotkeys { get; init; } = new();

    public UpdateSettings Updates { get; init; } = new();
}

public sealed record LlmSettings
{
    /// <summary>
    /// Which provider to use. "none" is a real, supported choice — every input path stays
    /// answerable through the model-free keyword router (list.md Phase 3).
    /// </summary>
    public string Provider { get; init; } = "anthropic";

    /// <summary>Null uses the provider's own default rather than pinning a model here.</summary>
    public string? Model { get; init; }

    /// <summary>
    /// Null uses the provider's published endpoint. A value here points at something else
    /// speaking the same protocol — a gateway or a proxy — which is why changing it clears
    /// <see cref="Model"/>: model ids are a property of the endpoint's namespace, and a name
    /// carried across from another endpoint is a stale selection that fails at the first turn
    /// (list.md Phase 4).
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// False is "plain answers, no persona". The anti-invention guardrails are unaffected —
    /// they sit above the persona in the assembled prompt and there is no setter for them.
    /// </summary>
    public bool PersonalityEnabled { get; init; } = true;

    /// <summary>The Commander's standing prompt about themselves, kept between sessions.</summary>
    public string? AboutMe { get; init; }
}

public sealed record LoggingSettings
{
    /// <summary>Applies to any subsystem with no explicit entry below.</summary>
    public LogLevel Default { get; init; } = LogLevel.Information;

    /// <summary>
    /// Per-subsystem overrides, keyed by <see cref="Diagnostics.Subsystems"/> name. Unknown
    /// subsystem names are rejected on load along with any other unknown key.
    /// </summary>
    public IReadOnlyDictionary<string, LogLevel> Subsystems { get; init; } =
        new Dictionary<string, LogLevel>();
}

public sealed record UiSettings
{
    /// <summary>
    /// A theme id from the shipped set. Colour lives in one place and no view hardcodes a
    /// literal, so this is the only thing that has to change to repaint the app (list.md
    /// Phase 4, "Themes").
    /// </summary>
    public string Theme { get; init; } = "elite";
}

/// <summary>
/// Bound gestures, stored as the display form the binding UI produces ("Ctrl+Shift+S"). One
/// property per action rather than a dictionary: an unknown action in a hand-edited file has
/// to be rejected like any other unknown key, and a dictionary would accept it silently.
/// <para>
/// These are window-scoped. A gesture that works while Elite has the foreground needs a
/// system-wide registration, which arrives with the phase that needs it — push-to-talk in
/// Phase 6 — rather than being built here for nothing to use.
/// </para>
/// </summary>
public sealed record HotkeySettings
{
    public string? OpenSettings { get; init; } = "F10";

    public string? FocusAsk { get; init; } = "Ctrl+L";
}

public sealed record UpdateSettings
{
    /// <summary>
    /// The startup check contacts GitHub, so it is egress and is disclosed as such. Turning it
    /// off is part of what makes local-only operation a reachable configuration rather than a
    /// theoretical one (list.md Phase 4, "Say what each provider receives").
    /// </summary>
    public bool CheckOnStartup { get; init; } = true;
}
