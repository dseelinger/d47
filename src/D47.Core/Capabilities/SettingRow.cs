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

    /// <summary>A controller button, bound by pressing it (Phase 53).</summary>
    HotasButton,

    /// <summary>Read-only disclosure.</summary>
    Info,
}

/// <summary>Whose value a row holds (Phase 44, "Several Commanders, one installation").</summary>
public enum SettingScope
{
    /// <summary>The installation's.</summary>
    Install,

    /// <summary>The Commander's who is flying.</summary>
    Commander,
}

/// <summary>Hearing a value before choosing it (Phase 19).</summary>
public sealed record SettingAudition
{
    /// <summary>Plays one value, and commits nothing.</summary>
    public required Func<string, CancellationToken, Task> Play { get; init; }

    /// <summary>What a press costs, as a sentence.</summary>
    public required Func<D47Settings, string> Cost { get; init; }

    /// <summary>Why it cannot be pressed, or null when it can.</summary>
    public Func<D47Settings, string?>? Unavailable { get; init; }
}

/// <summary>One named subset of a picker's list, offered beside the search box (#146).</summary>
/// <param name="Label">What the option is called. "All", "Female", "Unlabelled".</param>
/// <param name="Matches">
/// Whether a choice belongs in it, or null for the option that takes everything.
/// </param>
public sealed record SettingFacetOption(string Label, Func<string, bool>? Matches);

/// <summary>A structured filter a picker offers as well as its search box (#146).</summary>
public sealed record SettingFacet
{
    /// <summary>What the facet is about, shown beside the control. "Voice", "Kind".</summary>
    public required string Label { get; init; }

    /// <summary>The options, in the order they are offered.</summary>
    public required IReadOnlyList<SettingFacetOption> Options { get; init; }
}

/// <summary>How a row reads and writes its value.</summary>
public sealed record SettingBinding
{
    public required Func<D47Settings, string?> Read { get; init; }

    /// <summary>Null for a row that can be shown but not set — <see cref="SettingKind.Info"/>.</summary>
    public Func<D47Settings, string?, D47Settings>? Write { get; init; }
}

/// <summary>A phrase the model-free keyword router accepts for this row, and the value it writes.</summary>
public sealed record SettingCommandPhrase(string Phrase, string? Value);

/// <summary>What a long press does: report how far it has got, answer what to say when it ends.</summary>
public delegate Task<string?> LongPress(IProgress<double> progress, CancellationToken cancellationToken);

/// <summary>A settings row, declared by the capability that owns it.</summary>
public sealed record SettingRow
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    /// <summary>The short form.</summary>
    public required string Help { get; init; }

    /// <summary>
    /// A hazard the Commander is accepting by using this row, or null on the rows that carry none —
    /// which is all of them but one (#237).
    /// </summary>
    public string? Warning { get; init; }

    public required SettingKind Kind { get; init; }

    /// <summary>
    /// Shown as a placeholder, never as a value, so a default is visually distinct from a choice the
    /// Commander actually made (Phase 4).
    /// </summary>
    public string? DefaultDisplay { get; init; }

    /// <summary>
    /// A placeholder that depends on other settings — the endpoint and model defaults belong to the
    /// selected provider.
    /// </summary>
    public Func<D47Settings, string?>? DefaultDisplaySource { get; init; }

    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>How a choice is written for a person.</summary>
    public Func<string, string>? ChoiceLabel { get; init; }

    /// <summary>
    /// Choices that depend on other settings — the model list belongs to the selected provider's
    /// endpoint, not to the app.
    /// </summary>
    public Func<D47Settings, IReadOnlyList<string>>? ChoiceSource { get; init; }

    /// <summary>
    /// Labels that depend on other settings, for the reason <see cref="ChoiceSource"/> exists: a
    /// model's price and whether it is the provider's default belong to whichever provider is selected
    /// right now, and a label built at registration would be describing a list that is no longer on
    /// screen.
    /// </summary>
    public Func<D47Settings, Func<string, string>>? ChoiceLabelSource { get; init; }

    /// <summary>Whether a value outside the offered choices is legitimate.</summary>
    public bool AllowsFreeText { get; init; }

    /// <summary>
    /// A structured filter offered beside the picker's search box, or null on a row that has no
    /// property worth filtering on (#146).
    /// </summary>
    public Func<D47Settings, SettingFacet?>? Facet { get; init; }

    /// <summary>
    /// Why there is nothing to choose from, when there is nothing to choose from — or null when the row
    /// has no better answer than the picker's own generic one.
    /// </summary>
    public Func<D47Settings, string?>? WhyNoChoices { get; init; }

    /// <summary>
    /// How a value can be heard before it is chosen, when hearing it is the only way to judge it (Phase
    /// 19, "Hear a voice before you choose it").
    /// </summary>
    public SettingAudition? Audition { get; init; }

    /// <summary>Whether the row applies at all right now.</summary>
    public Func<D47Settings, bool>? AppliesWhen { get; init; }

    /// <summary>Whether this hotkey is claimed from the whole system rather than from d47's own window.</summary>
    public bool SystemWide { get; init; }

    /// <summary>
    /// Shown on the settings page, and not offered to the model, because a better-targeted row exists
    /// for the same value (#21).
    /// </summary>
    public bool PageOnly { get; init; }

    /// <summary>Another row this row's control binds as well as its own (#217).</summary>
    public string? AlsoBinds { get; init; }

    /// <summary>
    /// This row is real, writable and reachable, and nothing draws it — another row's control holds it,
    /// through <see cref="AlsoBinds"/>.
    /// </summary>
    public bool DrawnElsewhere { get; init; }

    /// <summary>
    /// Every settings key this row's control writes: its own, and <see cref="AlsoBinds"/> when there is
    /// one.
    /// </summary>
    public IReadOnlyList<string> BoundKeys => AlsoBinds is null ? [Key] : [Key, AlsoBinds];

    /// <summary>Drawn once at the top of the settings page rather than inside its card (#60).</summary>
    public bool PageTop { get; init; }

    /// <summary>How the value is read and written.</summary>
    public SettingBinding? Binding { get; init; }

    /// <summary>The name in the secret store for a <see cref="SettingKind.Secret"/> row.</summary>
    public string? SecretName { get; init; }

    /// <summary>Tries the stored secret against the real service and says what happened (Phase 16).</summary>
    public Func<CancellationToken, Task<SecretCheck>>? Verify { get; init; }

    /// <summary>
    /// The id in <see cref="Configuration.EgressDisclosure"/> for what this key sends and where.
    /// </summary>
    public string? EgressId { get; init; }

    /// <summary>
    /// What this row's own value causes to leave, where that is not what the current selection causes.
    /// </summary>
    public Func<D47Settings, EgressEntry>? EgressFor { get; init; }

    /// <summary>Anchor within the owning capability's documentation page.</summary>
    public string? DocsAnchor { get; init; }

    /// <summary>
    /// How much one step of a <see cref="SettingKind.Number"/> row is worth, and — because it is the
    /// only thing that could — how many decimal places the value has.
    /// </summary>
    public double Step { get; init; } = 1;

    /// <summary>The ends of a number row's range, where it has them.</summary>
    public double? Minimum { get; init; }

    public double? Maximum { get; init; }

    /// <summary>How this row's number is written, derived from its step rather than declared twice.</summary>
    public string NumberFormat => Step switch
    {
        >= 1 => "0",
        >= 0.1 => "0.#",
        >= 0.01 => "0.##",

        // Thousandths, for the one row that needs them: a price per thousand characters runs from $0.05 to
        // $0.20, so two decimal places is four distinguishable values across the whole published range.
        _ => "0.###",
    };

    /// <summary>Phrases the model-free router accepts for this row.</summary>
    public IReadOnlyList<SettingCommandPhrase> Commands { get; init; } = [];

    /// <summary>
    /// The one thing an <see cref="SettingKind.Info"/> row may do besides state a value: a button that
    /// clears the state the row describes.
    /// </summary>
    public Action? Press { get; init; }

    /// <summary>
    /// The same button, where what is behind it takes long enough that the Commander has to see it
    /// happening.
    /// </summary>
    public LongPress? PressAsync { get; init; }

    /// <summary>What the <see cref="Press"/> button says.</summary>
    public string? PressLabel { get; init; }

    /// <summary>
    /// A <see cref="SettingKind.Choice"/> row whose choices have to be fetched before they can be
    /// selected (#139).
    /// </summary>
    public Func<string?, IProgress<double>, CancellationToken, Task<string?>>? FetchChoiceAsync
    {
        get;
        init;
    }

    /// <summary>
    /// Whether an <see cref="SettingKind.Info"/> row's value belongs on a tooltip rather than on the
    /// page.
    /// </summary>
    public bool ValueAsHint { get; init; }

    /// <summary>Whether the value is a paragraph rather than a line.</summary>
    public bool Multiline { get; init; }

    /// <summary>An optional subheading these rows sit under.</summary>
    public string? Group { get; init; }

    /// <summary>Shown under the group heading, once, for the whole group.</summary>
    public string? GroupHelp { get; init; }

    /// <summary>
    /// Never settable through a tool the model can call — the panel, a hotkey and the model-free
    /// keyword router reach it, the LLM path does not (Phase 4).
    /// </summary>
    public bool Protected { get; init; }

    /// <summary>Whose setting this is: the installation's, or the Commander's who is flying (Phase 44).</summary>
    public SettingScope Scope { get; init; }

    /// <summary>Whether this row is folded away on the calm settings page (#60).</summary>
    public bool Advanced { get; init; }

    public IReadOnlyList<string> ChoicesFor(D47Settings settings) =>
        ChoiceSource?.Invoke(settings) ?? Choices;

    /// <summary>
    /// Whether this row's vocabulary is open — a list nobody can write down in advance, like the voices
    /// an account happens to have or the models an endpoint happens to serve.
    /// </summary>
    public bool IsOpenVocabulary => ChoiceSource is not null && Choices.Count == 0;

    /// <summary>
    /// Why the list is empty, or null — either because it is not, or because this row has nothing more
    /// specific to say than the picker already does.
    /// </summary>
    public string? WhyNoChoicesFor(D47Settings settings) =>
        ChoicesFor(settings).Count > 0 ? null : WhyNoChoices?.Invoke(settings);

    public bool Applies(D47Settings settings) => AppliesWhen?.Invoke(settings) ?? true;

    public string? DefaultDisplayFor(D47Settings settings) =>
        DefaultDisplaySource?.Invoke(settings) ?? DefaultDisplay;

    /// <summary>The default as a bare phrase, with any brackets it was declared inside removed.</summary>
    public string? BareDefaultFor(D47Settings settings)
    {
        var shown = DefaultDisplayFor(settings);

        return shown is { Length: > 1 } && shown[0] == '(' && shown[^1] == ')'
            ? shown[1..^1]
            : shown;
    }

    /// <summary>How this row's choices read right now.</summary>
    public Func<string, string> DescriberFor(D47Settings settings) =>
        ChoiceLabelSource?.Invoke(settings) ?? ChoiceLabel ?? Verbatim;

    /// <summary>One choice, written for a person.</summary>
    public string LabelForChoice(string choice, D47Settings settings) => DescriberFor(settings)(choice);

    /// <summary>The id reads fine as-is, which is true of log levels and of most rows.</summary>
    private static readonly Func<string, string> Verbatim = choice => choice;

    /// <summary>Whether "nothing chosen" is a state this row can be in.</summary>
    public bool IsClearable =>
        Binding is { Write: not null } binding && binding.Read(D47Settings.Defaults) is null;
}
