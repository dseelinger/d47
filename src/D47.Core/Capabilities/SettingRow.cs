using D47.Core.Configuration;

namespace D47.Core.Capabilities;

public enum SettingKind
{
    Text,
    Secret,
    Toggle,
    Choice,
    Number,
    Hotkey,

    /// <summary>
    /// Read-only disclosure. Not a value the Commander sets — a value d47 states, rendered as
    /// a row so it sits where the setting it describes does. The egress disclosures are these.
    /// </summary>
    Info,
}

/// <summary>
/// How a row reads and writes its value. String-valued throughout: the settings surface, the
/// picker, the tool surface and the keyword router all speak text, and one conversion point
/// per row is fewer than one per caller. A null written value means "no choice made" and the
/// row falls back to its default, which is why <see cref="SettingRow.DefaultDisplay"/> is a
/// placeholder rather than a value.
/// </summary>
public sealed record SettingBinding
{
    public required Func<D47Settings, string?> Read { get; init; }

    /// <summary>Null for a row that can be shown but not set — <see cref="SettingKind.Info"/>.</summary>
    public Func<D47Settings, string?, D47Settings>? Write { get; init; }
}

/// <summary>
/// A phrase the model-free keyword router accepts for this row, and the value it writes.
/// <para>
/// This is the reason the router exists in the shape it does: safety-critical rows are
/// unreachable from the tool surface entirely, so a fixed phrase mapped to a fixed value is
/// how they stay reachable by voice without the model in the path (architecture.md §7). A
/// closed phrase-to-value pair, never free-text argument extraction — a router that guesses
/// at values is a router that changes the wrong setting with total confidence.
/// </para>
/// </summary>
public sealed record SettingCommandPhrase(string Phrase, string? Value);

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

    /// <summary>
    /// A placeholder that depends on other settings — the endpoint and model defaults belong
    /// to the selected provider. Takes precedence over <see cref="DefaultDisplay"/>.
    /// </summary>
    public Func<D47Settings, string?>? DefaultDisplaySource { get; init; }

    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>
    /// Choices that depend on other settings — the model list belongs to the selected
    /// provider's endpoint, not to the app. Takes precedence over <see cref="Choices"/>.
    /// </summary>
    public Func<D47Settings, IReadOnlyList<string>>? ChoiceSource { get; init; }

    /// <summary>
    /// Whether a value outside the offered choices is legitimate. True for the model row: an
    /// endpoint d47 has never heard of still has model names, and the picker's contract is
    /// that an empty list still lets you keep the current value or type one (list.md Phase 4).
    /// </summary>
    public bool AllowsFreeText { get; init; }

    /// <summary>
    /// Whether the row applies at all right now. Settings adapt to the selected provider
    /// instead of showing a hardwired set (list.md Phase 4), and a row that does not apply is
    /// absent rather than disabled — a greyed-out control still asserts the setting exists.
    /// </summary>
    public Func<D47Settings, bool>? AppliesWhen { get; init; }

    /// <summary>How the value is read and written. Null only for <see cref="Kind"/> Secret.</summary>
    public SettingBinding? Binding { get; init; }

    /// <summary>
    /// The name in the secret store for a <see cref="SettingKind.Secret"/> row. Values are
    /// write-only: the surface can say whether one is present and can replace it, and nothing
    /// outside the code that needs the secret ever reads it back.
    /// </summary>
    public string? SecretName { get; init; }

    /// <summary>
    /// Anchor within the owning capability's documentation page. The per-row setup-guide link
    /// points here; the docs gate asserts the anchor's heading exists (list.md Phase 4,
    /// "Link each settings row to its documentation").
    /// </summary>
    public string? DocsAnchor { get; init; }

    /// <summary>Phrases the model-free router accepts for this row. Usually empty.</summary>
    public IReadOnlyList<SettingCommandPhrase> Commands { get; init; } = [];

    /// <summary>
    /// Whether the value is a paragraph rather than a line. Only About Me so far, and only
    /// the control's height depends on it.
    /// </summary>
    public bool Multiline { get; init; }

    /// <summary>
    /// Never settable through a tool the model can call — the panel, a hotkey and the
    /// model-free keyword router reach it, the LLM path does not (list.md Phase 4). The
    /// protected set is a property of the caller, so it is enforced in one place:
    /// <see cref="Configuration.SettingsService.Apply"/>.
    /// </summary>
    public bool Protected { get; init; }

    public IReadOnlyList<string> ChoicesFor(D47Settings settings) =>
        ChoiceSource?.Invoke(settings) ?? Choices;

    public bool Applies(D47Settings settings) => AppliesWhen?.Invoke(settings) ?? true;

    public string? DefaultDisplayFor(D47Settings settings) =>
        DefaultDisplaySource?.Invoke(settings) ?? DefaultDisplay;
}
