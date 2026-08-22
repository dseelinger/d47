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
/// Hearing a value before choosing it (list.md Phase 19).
/// <para>
/// Declared on the row rather than special-cased in the settings surface, for the reason every
/// other row property is: the surface renders what a descriptor declares, and a voice row that
/// the panel had to recognise by key would be a second list to keep in step with the registry.
/// </para>
/// <para>
/// Everything here is a function of settings, because all three answers move: what the line
/// costs depends on the selected provider, and whether it can be played at all depends on
/// whether one is selected.
/// </para>
/// </summary>
public sealed record SettingAudition
{
    /// <summary>
    /// Plays one value, and commits nothing. Cancelled when a second audition starts, which is
    /// what makes walking a list of candidates one voice at a time rather than several at once.
    /// </summary>
    public required Func<string, CancellationToken, Task> Play { get; init; }

    /// <summary>
    /// What a press costs, as a sentence. The price belongs here — a control that spends money
    /// should say so before it is pressed, and a provider that costs nothing should say
    /// <em>that</em> rather than leaving it to be guessed at.
    /// <para>
    /// It reads as a line above the list rather than as a caption, because from
    /// change-requests.md 18 the control itself is a play glyph on each row and a glyph has no
    /// room for a price. The disclosure survived the button it used to be written on.
    /// </para>
    /// </summary>
    public required Func<D47Settings, string> Cost { get; init; }

    /// <summary>Why it cannot be pressed, or null when it can.</summary>
    public Func<D47Settings, string?>? Unavailable { get; init; }
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
    /// How a choice is written for a person. Values stay ids — `anthropic`, `elite-palette` —
    /// because that is what goes in the settings file and what a tool call names; only the
    /// label changes. Null means the id reads fine as-is, which is true of log levels.
    /// </summary>
    public Func<string, string>? ChoiceLabel { get; init; }

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
    /// Why there is nothing to choose from, when there is nothing to choose from — or null when
    /// the row has no better answer than the picker's own generic one.
    /// <para>
    /// Added because the voice row had four different reasons for being empty and one sentence
    /// for all of them: no key stored, a key the provider refused, a provider that could not be
    /// reached, and an account that genuinely has no voices. Only the first two are anything the
    /// Commander can act on, and the picker was telling them to type a voice id they have no way
    /// of knowing (list.md Phase 19; docs/spikes/elevenlabs-voice-sources.md §3).
    /// </para>
    /// <para>
    /// A function of settings rather than a string, for the same reason
    /// <see cref="ChoiceSource"/> is: the answer belongs to whichever provider is selected right
    /// now, and one captured at registration would keep explaining the wrong one.
    /// </para>
    /// </summary>
    public Func<D47Settings, string?>? WhyNoChoices { get; init; }

    /// <summary>
    /// How a value can be heard before it is chosen, when hearing it is the only way to judge it
    /// (list.md Phase 19, "Hear a voice before you choose it"). Null on every row where it is
    /// not, which is all of them but three.
    /// </summary>
    public SettingAudition? Audition { get; init; }

    /// <summary>
    /// Whether the row applies at all right now. Settings adapt to the selected provider
    /// instead of showing a hardwired set (list.md Phase 4), and a row that does not apply is
    /// absent rather than disabled — a greyed-out control still asserts the setting exists.
    /// </summary>
    public Func<D47Settings, bool>? AppliesWhen { get; init; }

    /// <summary>
    /// Whether this hotkey is claimed from the whole system rather than from d47's own window.
    /// <para>
    /// A system-wide gesture with no modifier takes that key away from every other application,
    /// including the game — bind <kbd>]</kbd> and you could no longer type <kbd>]</kbd> anywhere.
    /// The binder has always refused those; declaring it here is what lets the refusal happen on
    /// the row the Commander just pressed, instead of arriving later as a message on a panel
    /// behind the settings surface, about a value that was stored anyway.
    /// </para>
    /// <para>
    /// False for push-to-talk, which is polled rather than registered — a bare key is the normal
    /// arrangement there, and that difference is the whole reason this is a property of the row.
    /// </para>
    /// </summary>
    public bool SystemWide { get; init; }

    /// <summary>How the value is read and written. Null only for <see cref="Kind"/> Secret.</summary>
    public SettingBinding? Binding { get; init; }

    /// <summary>
    /// The name in the secret store for a <see cref="SettingKind.Secret"/> row. Values are
    /// write-only: the surface can say whether one is present and can replace it, and nothing
    /// outside the code that needs the secret ever reads it back.
    /// </summary>
    public string? SecretName { get; init; }

    /// <summary>
    /// Tries the stored secret against the real service and says what happened (list.md Phase 16).
    /// Null on a row that has nothing to try it against.
    /// <para>
    /// <b>It makes the real call.</b> A key that is wrong, revoked, or pasted with a trailing
    /// newline is otherwise indistinguishable from one that works until the first turn fails —
    /// and by then the Commander is somewhere else, watching d47 not answer rather than watching
    /// a key not work.
    /// </para>
    /// <para>
    /// Takes no argument: the verifier reads the stored value itself, so the key never travels
    /// through the surface that asked. That is the same rule as <see cref="SecretName"/> — a row
    /// can ask whether a secret works without ever being told what it is.
    /// </para>
    /// </summary>
    public Func<CancellationToken, Task<SecretCheck>>? Verify { get; init; }

    /// <summary>
    /// The id in <see cref="Configuration.EgressDisclosure"/> for what this key sends and where.
    /// Carried rather than written out beside the row, because a hand-written sentence about what
    /// leaves the machine is exactly the sentence that goes stale — and this is the one place a
    /// Commander is deciding whether to trust the thing.
    /// </summary>
    public string? EgressId { get; init; }

    /// <summary>
    /// What <em>this row's own value</em> causes to leave, where that is not what the current
    /// selection causes. Overrides <see cref="EgressId"/> where both are set.
    /// <para>
    /// A provider's key row is about that provider. Resolving <see cref="EgressId"/> against the
    /// settings answers "what is leaving right now", which on a key row is a different question
    /// from the one being asked — and answered it wrongly: with Edge selected, the ElevenLabs key
    /// row described Edge Read Aloud and named Bing's address as where the key would go.
    /// </para>
    /// <para>
    /// A function of settings rather than a fixed entry, so that this stays the same kind of
    /// thing every other disclosure is — computed, never a stored sentence. A key row that does
    /// not vary with settings simply ignores the argument, which is the point of it.
    /// </para>
    /// </summary>
    public Func<D47Settings, EgressEntry>? EgressFor { get; init; }

    /// <summary>
    /// Anchor within the owning capability's documentation page. The per-row setup-guide link
    /// points here; the docs gate asserts the anchor's heading exists (list.md Phase 4,
    /// "Link each settings row to its documentation").
    /// </summary>
    public string? DocsAnchor { get; init; }

    /// <summary>
    /// How much one step of a <see cref="SettingKind.Number"/> row is worth, and — because it
    /// is the only thing that could — how many decimal places the value has.
    /// <para>
    /// It defaults to 1, which is what a count wants. A row whose value is a fraction has to
    /// say so: without this, "number" meant "whole number", and a speaking rate documented as
    /// "1.2 is a fifth faster" rejected 1.2 because nothing had ever told the surface that
    /// tenths existed.
    /// </para>
    /// </summary>
    public double Step { get; init; } = 1;

    /// <summary>
    /// The ends of a number row's range, where it has them. Null means unbounded.
    /// <para>
    /// Declared rather than only enforced in the setter, because two things need to know: the
    /// stepper the panel builds, which should not offer a click that will be clamped away, and
    /// the number-row gate, which steps every row by one and has to step <em>down</em> from a row
    /// that is already sitting at its ceiling. A level of 1 is not a row that cannot move.
    /// </para>
    /// </summary>
    public double? Minimum { get; init; }

    public double? Maximum { get; init; }

    /// <summary>
    /// How this row's number is written, derived from its step rather than declared twice. A
    /// format and a step that disagree is a value that changes every time it is read back.
    /// </summary>
    public string NumberFormat => Step switch
    {
        >= 1 => "0",
        >= 0.1 => "0.#",
        >= 0.01 => "0.##",

        // Thousandths, for the one row that needs them: a price per thousand characters runs
        // from $0.05 to $0.20, so two decimal places is four distinguishable values across the
        // whole published range. The ladder is extended rather than the row declaring its own
        // format, because a format and a step that disagree is a value that changes every time
        // it is read back.
        _ => "0.###",
    };

    /// <summary>Phrases the model-free router accepts for this row. Usually empty.</summary>
    public IReadOnlyList<SettingCommandPhrase> Commands { get; init; } = [];

    /// <summary>
    /// The one thing an <see cref="SettingKind.Info"/> row may do besides state a value: a
    /// button that clears the state the row describes.
    /// <para>
    /// Info rather than a kind of its own, because what this renders is still a disclosure —
    /// the button is how the Commander answers it. It also puts the action on the right side
    /// of the trust boundary for free: <see cref="Configuration.SettingsService.Apply"/>
    /// refuses Info rows outright, so nothing reachable from the tool surface can press this.
    /// </para>
    /// </summary>
    public Action? Press { get; init; }

    /// <summary>What the <see cref="Press"/> button says. Required when there is one.</summary>
    public string? PressLabel { get; init; }

    /// <summary>
    /// Whether an <see cref="SettingKind.Info"/> row's value belongs on a tooltip rather than
    /// on the page.
    /// <para>
    /// The egress disclosures are paragraphs, and a paragraph rendered inline is a paragraph
    /// between two rows that are one line each — the one about the voice provider is five lines
    /// in the middle of the speech card. It is a thing to consult, not a thing to read every
    /// time: the row still states what it is about, and hovering the label or its help line
    /// gives the whole disclosure (remediation.md, "What the voice provider receives").
    /// </para>
    /// <para>
    /// Declared on the row for the reason every other row property is: the surface renders what
    /// a descriptor declares, and a row the panel had to recognise by key would be a second
    /// list to keep in step with the registry.
    /// </para>
    /// </summary>
    public bool ValueAsHint { get; init; }

    /// <summary>
    /// Whether the value is a paragraph rather than a line. Only the character sheet and About
    /// Me so far, and only the control's height depends on it.
    /// </summary>
    public bool Multiline { get; init; }

    /// <summary>
    /// An optional subheading these rows sit under. Rows sharing a group render together with
    /// the explanation stated once, instead of repeating it per row — which is what nine
    /// near-identical subsystem rows looked like before.
    /// </summary>
    public string? Group { get; init; }

    /// <summary>Shown under the group heading, once, for the whole group.</summary>
    public string? GroupHelp { get; init; }

    /// <summary>
    /// Never settable through a tool the model can call — the panel, a hotkey and the
    /// model-free keyword router reach it, the LLM path does not (list.md Phase 4). The
    /// protected set is a property of the caller, so it is enforced in one place:
    /// <see cref="Configuration.SettingsService.Apply"/>.
    /// </summary>
    public bool Protected { get; init; }

    public IReadOnlyList<string> ChoicesFor(D47Settings settings) =>
        ChoiceSource?.Invoke(settings) ?? Choices;

    /// <summary>
    /// Whether this row's vocabulary is open — a list nobody can write down in advance, like the
    /// voices an account happens to have or the models an endpoint happens to serve. Those get the
    /// searchable picker; a closed list gets a drop-down.
    /// <para>
    /// <b>A computed list is not the same as an open one</b> (remediation.md 11, item 9). The
    /// persona row computes its choices so a core the Commander wrote appears the moment they
    /// write it, and it is still eleven-or-so named things a person picks from — asking
    /// "does it compute?" turned it into a search window the moment it gained a source. What
    /// makes a vocabulary open is that nothing here knows its shape, which is exactly the case
    /// where there is no <see cref="Choices"/> to fall back on.
    /// </para>
    /// </summary>
    public bool IsOpenVocabulary => ChoiceSource is not null && Choices.Count == 0;

    /// <summary>
    /// Why the list is empty, or null — either because it is not, or because this row has
    /// nothing more specific to say than the picker already does.
    /// </summary>
    public string? WhyNoChoicesFor(D47Settings settings) =>
        ChoicesFor(settings).Count > 0 ? null : WhyNoChoices?.Invoke(settings);

    public bool Applies(D47Settings settings) => AppliesWhen?.Invoke(settings) ?? true;

    public string? DefaultDisplayFor(D47Settings settings) =>
        DefaultDisplaySource?.Invoke(settings) ?? DefaultDisplay;

    /// <summary>
    /// The default as a bare phrase, with any brackets it was declared inside removed.
    /// <para>
    /// Rows disagree about this and always will: some read naturally as an aside — "(the
    /// provider's default)" — and some are a value, like a model name. Every surface then frames
    /// it its own way, so the ones that arrived bracketed came out doubled: "Use the default
    /// ((the provider's default))" on the picker, "(the provider's default) (default)" on the
    /// row. Stripping here means each surface can bracket unconditionally and be right.
    /// </para>
    /// </summary>
    public string? BareDefaultFor(D47Settings settings)
    {
        var shown = DefaultDisplayFor(settings);

        return shown is { Length: > 1 } && shown[0] == '(' && shown[^1] == ')'
            ? shown[1..^1]
            : shown;
    }

    public string LabelForChoice(string choice) => ChoiceLabel?.Invoke(choice) ?? choice;

    /// <summary>
    /// Whether "nothing chosen" is a state this row can be in. Provider and theme always hold a
    /// real value, so offering to clear them would mean offering the same answer twice —
    /// `(default: anthropic)` sitting directly above `anthropic`. Answered by asking the
    /// binding what it reads when nothing has been set, so it cannot drift from the binding.
    /// </summary>
    public bool IsClearable =>
        Binding is { Write: not null } binding && binding.Read(D47Settings.Defaults) is null;
}
