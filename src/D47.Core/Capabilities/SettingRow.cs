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
    /// A controller button, bound by pressing it (Phase 53).
    /// <para>
    /// Its own kind rather than <see cref="Hotkey"/> with a flag, because the gesture that fills
    /// it is a different one: a key is caught by the window that has focus, and a button has to be
    /// walked for — the Commander presses it, d47 works out which one it was, and neither of those
    /// is a keystroke arriving at a control. The stored form is <c>NonRoamableId#index</c> and is
    /// never typed by hand.
    /// </para>
    /// <para>
    /// <b>Still two kinds after #217 merged the two push-to-talk rows into one</b>, and that is the
    /// paragraph above holding rather than surviving by accident: the merge is of the <em>question
    /// put to the Commander</em>, not of the mechanisms. One control arms both listeners at once and
    /// takes whichever arrives first, which needs both mechanisms to stay exactly as distinct as
    /// they are here. See <see cref="SettingRow.AlsoBinds"/>.
    /// </para>
    /// </summary>
    HotasButton,

    /// <summary>
    /// Read-only disclosure. Not a value the Commander sets — a value d47 states, rendered as
    /// a row so it sits where the setting it describes does. The egress disclosures are these.
    /// </summary>
    Info,
}

/// <summary>
/// Whose value a row holds (Phase 44, "Several Commanders, one installation").
/// </summary>
public enum SettingScope
{
    /// <summary>
    /// The installation's. Keys, devices, theme, zoom, hotkeys: the machine's and the person's,
    /// whichever character they are playing. The default, so a row says nothing unless it is
    /// the other kind.
    /// </summary>
    Install,

    /// <summary>
    /// The Commander's who is flying. Layered over the installation's value per Frontier id,
    /// with the id inside the settings document and never in a path — it comes out of the
    /// journal, which is untrusted input.
    /// </summary>
    Commander,
}

/// <summary>
/// Hearing a value before choosing it (Phase 19).
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
/// One named subset of a picker's list, offered beside the search box
/// (<a href="https://github.com/dseelinger/d47/issues/146">#146</a>).
/// </summary>
/// <param name="Label">What the option is called. "All", "Female", "Unlabelled".</param>
/// <param name="Matches">
/// Whether a choice belongs in it, or null for the option that takes everything. A predicate over
/// the stored value rather than over the rendered label, because the label is prose and the value
/// is the thing that has a property.
/// </param>
public sealed record SettingFacetOption(string Label, Func<string, bool>? Matches);

/// <summary>
/// A structured filter a picker offers as well as its search box
/// (<a href="https://github.com/dseelinger/d47/issues/146">#146</a>).
/// <para>
/// <b>It exists because searching a label for a word is not the same as filtering on a field.</b>
/// A voice carries its gender as data and the label merely renders it, so a Commander wanting the
/// women had to type a word that happens to appear in the rendered string — which is how typing
/// <em>male</em> came to list every female voice, and why there was no way to type your way out of
/// it. A facet asks the data.
/// </para>
/// <para>
/// Declared on the row for the reason every other row property is: the surface renders what a
/// descriptor declares, and a picker that had to recognise the voice rows by key would be a second
/// list to keep in step with the registry.
/// </para>
/// </summary>
public sealed record SettingFacet
{
    /// <summary>What the facet is about, shown beside the control. "Voice", "Kind".</summary>
    public required string Label { get; init; }

    /// <summary>
    /// The options, in the order they are offered. The first is the one selected when the picker
    /// opens, so it should be the one that hides nothing.
    /// </summary>
    public required IReadOnlyList<SettingFacetOption> Options { get; init; }
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
/// What a long press does: report how far it has got, answer what to say when it ends.
/// <para>
/// The fraction runs 0 to 1. The answer is a sentence for the row to show, or null where there
/// is nothing to add — a finished download is described by the row's own state, and a line
/// saying so is a line the Commander has to dismiss by reading it.
/// </para>
/// </summary>
public delegate Task<string?> LongPress(IProgress<double> progress, CancellationToken cancellationToken);

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
    /// choice the Commander actually made (Phase 4).
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
    /// Labels that depend on other settings, for the reason <see cref="ChoiceSource"/> exists:
    /// a model's price and whether it is the provider's default belong to whichever provider is
    /// selected right now, and a label built at registration would be describing a list that is
    /// no longer on screen. Takes precedence over <see cref="ChoiceLabel"/>.
    /// <para>
    /// A factory rather than a two-argument label, because a describer is built once per opening
    /// and applied to every row of the list — the model row's <em>cheapest here</em> is a
    /// property of the whole list, and computing it per line would be computing it per line
    /// (<a href="https://github.com/dseelinger/d47/issues/152">#152</a>).
    /// </para>
    /// </summary>
    public Func<D47Settings, Func<string, string>>? ChoiceLabelSource { get; init; }

    /// <summary>
    /// Whether a value outside the offered choices is legitimate. True for the model row: an
    /// endpoint d47 has never heard of still has model names, and the picker's contract is
    /// that an empty list still lets you keep the current value or type one (Phase 4).
    /// </summary>
    public bool AllowsFreeText { get; init; }

    /// <summary>
    /// A structured filter offered beside the picker's search box, or null on a row that has no
    /// property worth filtering on (<a href="https://github.com/dseelinger/d47/issues/146">#146</a>).
    /// <para>
    /// A function of settings for the reason <see cref="ChoiceSource"/> is: the voices belong to
    /// whichever provider is selected right now, and a facet built at registration would be
    /// describing a list that is no longer on screen. <b>Returning null is how a row says the
    /// choices carry nothing to filter on</b> — a provider that tags no voice offers no gender
    /// filter, rather than one whose every option is empty.
    /// </para>
    /// </summary>
    public Func<D47Settings, SettingFacet?>? Facet { get; init; }

    /// <summary>
    /// Why there is nothing to choose from, when there is nothing to choose from — or null when
    /// the row has no better answer than the picker's own generic one.
    /// <para>
    /// Added because the voice row had four different reasons for being empty and one sentence
    /// for all of them: no key stored, a key the provider refused, a provider that could not be
    /// reached, and an account that genuinely has no voices. Only the first two are anything the
    /// Commander can act on, and the picker was telling them to type a voice id they have no way
    /// of knowing (Phase 19; docs/spikes/elevenlabs-voice-sources.md §3).
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
    /// (Phase 19, "Hear a voice before you choose it"). Null on every row where it is
    /// not, which is all of them but three.
    /// </summary>
    public SettingAudition? Audition { get; init; }

    /// <summary>
    /// Whether the row applies at all right now. Settings adapt to the selected provider
    /// instead of showing a hardwired set (Phase 4), and a row that does not apply is
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

    /// <summary>
    /// Shown on the settings page, and <b>not offered to the model</b>, because a better-targeted
    /// row exists for the same value (<a href="https://github.com/dseelinger/d47/issues/21">#21</a>).
    /// <para>
    /// <b>This is not <see cref="Protected"/> and must not be confused with it.</b> Protected is a
    /// safety property — a row the model may never reach by any route. This is an <em>aiming</em>
    /// property: the value is perfectly safe for the model to change, and there is simply a row
    /// that says which one it means. The page still shows both, and both are still writable there.
    /// </para>
    /// <para>
    /// It exists because offering the same value three ways is how a model picks the wrong one. The
    /// VR placement rows are stored per surface and there is a third row that resolves whichever
    /// surface is on screen; the Commander asking to move <em>the panel</em> means the one they are
    /// looking at, and the two explicit rows would only ever be the two wrong answers to that.
    /// </para>
    /// </summary>
    public bool PageOnly { get; init; }

    /// <summary>
    /// Another row this row's control binds as well as its own
    /// (<a href="https://github.com/dseelinger/d47/issues/217">#217</a>). Push-to-talk is the only
    /// one: the key row names the stick-button row, and one control holds both.
    /// <para>
    /// <b>Two rows over two properties, drawn as one.</b> <c>settings.json</c> is append-only, so
    /// <c>listening.pushToTalkKey</c> and <c>listening.pushToTalkButton</c> both stay on the record
    /// and both keep their own binding, help and docs anchor — a build that merged the storage would
    /// silently discard whichever half it dropped on first read. What merges is the question the
    /// Commander is asked, which was always one: <em>what do you press to talk?</em>
    /// </para>
    /// <para>
    /// The companion row carries <see cref="DrawnElsewhere"/>, so exactly one control is built for
    /// the pair and neither row is shown twice.
    /// </para>
    /// </summary>
    public string? AlsoBinds { get; init; }

    /// <summary>
    /// This row is real, writable and reachable, and <b>nothing draws it</b> — another row's control
    /// holds it, through <see cref="AlsoBinds"/>.
    /// <para>
    /// <b>Not <see cref="AppliesWhen"/>, and the difference is load-bearing.</b> A row that does not
    /// apply is refused by <c>SettingsService.Apply</c> as well as being hidden, which is right for a
    /// setting that has no meaning in the current configuration and exactly wrong here: this row is
    /// written every time the Commander binds a stick button. It applies; it is simply not its own
    /// row on the page.
    /// </para>
    /// </summary>
    public bool DrawnElsewhere { get; init; }

    /// <summary>
    /// Every settings key this row's control writes: its own, and <see cref="AlsoBinds"/> when there
    /// is one. What "has this row been changed" and "reset this row" both have to ask, since a merged
    /// row with one half changed is a changed row.
    /// </summary>
    public IReadOnlyList<string> BoundKeys => AlsoBinds is null ? [Key] : [Key, AlsoBinds];

    /// <summary>
    /// Drawn once at the top of the settings page rather than inside its card
    /// (<a href="https://github.com/dseelinger/d47/issues/60">#60</a>).
    /// <para>
    /// <b>For a row that governs the page itself.</b> "Show every setting" decides what the whole
    /// page draws, and a Commander asking <em>how do I see everything</em> looks at the top of the
    /// page rather than inside a card called Interface — where it was, four rows down, which is
    /// exactly where somebody who cannot see the rest of the page will not look.
    /// </para>
    /// <para>
    /// <b>Declared here rather than known by the view</b>, for the reason every other row property
    /// is: a panel holding its own list of which rows are special is a second list to keep in
    /// step. It still belongs to a capability, so its key, its documentation anchor, its spoken
    /// phrases and its coverage all work exactly as any other row's do — only where it is drawn
    /// changes.
    /// </para>
    /// <para>
    /// Never folded, whatever <see cref="Advanced"/> says: a control that hides the page cannot
    /// hide itself, or there is no way back.
    /// </para>
    /// </summary>
    public bool PageTop { get; init; }

    /// <summary>How the value is read and written. Null only for <see cref="Kind"/> Secret.</summary>
    public SettingBinding? Binding { get; init; }

    /// <summary>
    /// The name in the secret store for a <see cref="SettingKind.Secret"/> row. Values are
    /// write-only: the surface can say whether one is present and can replace it, and nothing
    /// outside the code that needs the secret ever reads it back.
    /// </summary>
    public string? SecretName { get; init; }

    /// <summary>
    /// Tries the stored secret against the real service and says what happened (Phase 16).
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
    /// points here (Phase 4, "Link each settings row to its documentation").
    /// <para>
    /// <b>It must name a heading on that capability's own page</b>, either as an explicit
    /// <c>{#anchor}</c> or as GitHub's slug of the heading text; both spellings are in use.
    /// <c>EverySettingsAnchorResolvesToAHeadingOnItsOwnPage</c> is what holds that, and until
    /// 2026-08-28 this comment claimed a gate that had never been written — forty-five rows
    /// pointed at nothing, and pressing "?" on them arrived nowhere (#123).
    /// </para>
    /// <para>
    /// <b>Where several rows share one explanation, they share one anchor.</b> The five audio
    /// categories and the ten headset placement settings are each explained once, as a group,
    /// and pointing each row at a heading of its own would mean writing documentation to
    /// satisfy a link rather than to be read.
    /// </para>
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

    /// <summary>
    /// The same button, where what is behind it takes long enough that the Commander has to see
    /// it happening.
    /// <para>
    /// <b>Added the day the local voice was first downloaded and nobody could tell.</b> A press
    /// that fetches 350 MB and reports nothing is indistinguishable from a dead control: the row
    /// still read <em>not downloaded</em>, the button still invited a second press, and the only
    /// evidence it had worked at all was a line in the log. So a press that answers this reports
    /// a fraction while it runs, shuts its own button, and answers a sentence for the row to
    /// show — or null where the state the row already reads says everything.
    /// </para>
    /// <para>
    /// Core owns no thread and starts nothing (architecture.md, invariants): this is a Task the
    /// App hands over, and the App is what decides where the work runs. Set instead of
    /// <see cref="Press"/> rather than beside it — a row has one button.
    /// </para>
    /// </summary>
    public LongPress? PressAsync { get; init; }

    /// <summary>What the <see cref="Press"/> button says. Required when there is one.</summary>
    public string? PressLabel { get; init; }

    /// <summary>
    /// A <see cref="SettingKind.Choice"/> row whose choices have to be fetched before they can be
    /// selected (<a href="https://github.com/dseelinger/d47/issues/139">#139</a>).
    /// <para>
    /// <b>The choice is the go-ahead</b>, which is the rule the speech model row settled: the size
    /// is stated in the list it was chosen from, so a confirmation on top of it is a question
    /// asked twice. Given the chosen value, this fetches whatever backs it and answers null when
    /// the choice can now be applied, or a sentence saying why it cannot.
    /// </para>
    /// <para>
    /// <b>The setting is written only once that answers null</b>, so a row can never name
    /// something d47 cannot load — and a failed or cancelled fetch leaves both the file and the
    /// setting as they were. The speech model row does this with plumbing of its own that predates
    /// this property; a row that sets this needs none.
    /// </para>
    /// <para>
    /// Core owns no thread, exactly as <see cref="PressAsync"/> above: the Task is the App's.
    /// </para>
    /// </summary>
    public Func<string?, IProgress<double>, CancellationToken, Task<string?>>? FetchChoiceAsync
    {
        get;
        init;
    }

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
    /// model-free keyword router reach it, the LLM path does not (Phase 4). The
    /// protected set is a property of the caller, so it is enforced in one place:
    /// <see cref="Configuration.SettingsService.Apply"/>.
    /// </summary>
    public bool Protected { get; init; }

    /// <summary>
    /// Whose setting this is: the installation's, or the Commander's who is flying (Phase 44).
    /// Declared on the row the way <see cref="Protected"/> is, and for the same
    /// reason: the split is per row and never inferred, because the obvious sweep gets it wrong
    /// in both directions — About Me is the Commander describing themselves and sat in a
    /// per-installation file, and the spend ledger is the person's running cost across every
    /// character and must never be split by one.
    /// <para>
    /// A <see cref="SettingScope.Commander"/> row reads and writes through
    /// <see cref="Configuration.CommanderScope"/>: the value the row sees is the active
    /// Commander's overlay where they have set one, and the installation's value where they
    /// have not. <c>CommanderScopeTests</c> asserts that the rows declaring this are exactly the
    /// rows the overlay reaches, so neither list can drift from the other.
    /// </para>
    /// </summary>
    public SettingScope Scope { get; init; }

    /// <summary>
    /// Whether this row is folded away on the calm settings page
    /// (<a href="https://github.com/dseelinger/d47/issues/60">#60</a>).
    /// <para>
    /// <b>Declared on the row, as every other row property is.</b> A panel holding its own list
    /// of which rows are advanced is a second list to keep in step, which is the exact failure
    /// that put <c>Help</c> and <c>Level</c> onto <c>NavCrumb</c> rather than into the view.
    /// </para>
    /// <para>
    /// <b>The rule for setting it:</b> a row stays on the calm page if a Commander cannot get d47
    /// working, or cannot control how much it talks, without it. That is narrower than "things
    /// people change" on purpose — it is the difference between <em>what do I need</em> and
    /// <em>what might I want</em>, and only the first belongs on a page whose job is to not
    /// frighten anybody.
    /// </para>
    /// <para>
    /// <b>Folding is display and nothing else.</b> A hidden row keeps working at its value or its
    /// default, is still reachable by voice, and is still where a help link lands. Nothing about
    /// this ever writes, clears, normalises or defaults a setting — the way that breaks is a
    /// well-meaning tidy-on-save pass, so it is a test rather than a comment.
    /// </para>
    /// <para>
    /// <b>Three kinds of row are never folded, whatever this says.</b> A secret, because a hidden
    /// row with no default and no value is a row that silently does nothing. Anything carrying an
    /// <see cref="EgressId"/>, because those decide what leaves the machine and a calm page that
    /// stopped mentioning egress would be calm about the wrong thing. And a row the Commander has
    /// actually changed, because the fold's promise is <em>you are not missing anything</em> and a
    /// changed row is by definition something they did. See <c>SettingsFold</c>, which is where
    /// those three are applied.
    /// </para>
    /// </summary>
    public bool Advanced { get; init; }

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

    /// <summary>
    /// How this row's choices read right now. Every surface that shows a choice goes through
    /// here, so the picker's list, the row's value and its tooltip cannot describe the same id
    /// three different ways.
    /// </summary>
    public Func<string, string> DescriberFor(D47Settings settings) =>
        ChoiceLabelSource?.Invoke(settings) ?? ChoiceLabel ?? Verbatim;

    /// <summary>
    /// One choice, written for a person. Settings are taken rather than assumed because
    /// <see cref="ChoiceLabelSource"/> reads them — a caller that could not pass them would get
    /// the bare id back on exactly the rows that most need a label.
    /// </summary>
    public string LabelForChoice(string choice, D47Settings settings) => DescriberFor(settings)(choice);

    /// <summary>The id reads fine as-is, which is true of log levels and of most rows.</summary>
    private static readonly Func<string, string> Verbatim = choice => choice;

    /// <summary>
    /// Whether "nothing chosen" is a state this row can be in. Provider and theme always hold a
    /// real value, so offering to clear them would mean offering the same answer twice —
    /// `(default: anthropic)` sitting directly above `anthropic`. Answered by asking the
    /// binding what it reads when nothing has been set, so it cannot drift from the binding.
    /// </summary>
    public bool IsClearable =>
        Binding is { Write: not null } binding && binding.Read(D47Settings.Defaults) is null;
}
