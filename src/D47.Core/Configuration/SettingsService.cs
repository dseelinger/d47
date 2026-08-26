using System.Globalization;
using D47.Core.Capabilities;
using Microsoft.Extensions.Logging;

namespace D47.Core.Configuration;

/// <summary>
/// Who is asking. Protection is a property of the caller, not of the modality
/// (architecture.md §7): the same row is reachable from the panel, from a hotkey and from the
/// model-free keyword router, and unreachable from anything the model can invoke — because
/// the model consumes untrusted text and a guard it can flip is privilege escalation.
/// </summary>
public enum SettingsCaller
{
    /// <summary>The settings surface, driven by the Commander's own hands.</summary>
    Panel,

    /// <summary>A bound gesture. Same trust as the panel: nothing between the key and the row.</summary>
    Hotkey,

    /// <summary>The model-free command path. No provider was contacted, no free text interpreted.</summary>
    KeywordRouter,

    /// <summary>A tool call. The only caller that can be steered by text d47 did not author.</summary>
    Model,

    /// <summary>
    /// A binding the Commander made, acting (list.md Phase 35). Same trust as the panel and for a
    /// stronger reason than the hotkey has: the value was not merely typed by them, it was written
    /// down by them and is being replayed unchanged. Nothing is interpreted between the file and
    /// the row.
    /// <para>
    /// Its own name rather than borrowing <see cref="Panel"/>, because the log line saying which
    /// caller changed the core is read when a Commander is asking why their companion changed —
    /// and "the panel" would be a lie told at exactly the moment it mattered.
    /// </para>
    /// </summary>
    ShipBinding,
}

public enum SettingApplyStatus
{
    Applied,

    /// <summary>The value was already what was asked for. Not an error, and not a write.</summary>
    Unchanged,

    UnknownKey,

    /// <summary>The value is not one this row accepts.</summary>
    Rejected,

    /// <summary>The caller is not allowed to change this row. Only the model ever sees this.</summary>
    Refused,

    /// <summary>The change was valid but could not be persisted.</summary>
    Failed,
}

public sealed record SettingApplyResult(SettingApplyStatus Status, string Message)
{
    public bool Ok => Status is SettingApplyStatus.Applied or SettingApplyStatus.Unchanged;
}

public sealed record SettingsChanged(string Key, D47Settings Settings);

/// <summary>One attempt to set a row, and what came of it — including the attempts that
/// changed nothing.</summary>
public sealed record SettingApplied(string Key, SettingApplyStatus Status);

/// <summary>One capability's rows, in the order the capability declared them.</summary>
public sealed record SettingsSection(CapabilityDescriptor Capability, IReadOnlyList<SettingRow> Rows);

/// <summary>
/// The one place a setting changes. Every caller — panel, hotkey, keyword router, tool —
/// arrives here, which is what lets the protected set be enforced once rather than once per
/// surface, and what makes "apply every setting without a restart" a property of the service
/// rather than a promise each screen has to keep: a change is validated, written, persisted
/// and announced in that order, and subscribers act on the announcement (list.md Phase 4).
/// <para>
/// There is no save button and no dirty state. A rejected value never reaches
/// <see cref="Current"/>, so what is on disk and what is in memory cannot disagree.
/// </para>
/// </summary>
public sealed class SettingsService
{
    private readonly SettingsStore _store;
    private readonly SecretStore _secrets;
    private readonly ILogger<SettingsService> _logger;

    private IReadOnlyList<SettingsSection>? _sections;
    private Dictionary<string, SettingRow>? _byKey;

    /// <summary>
    /// The document as it is on disk — both layers (list.md Phase 44). <see cref="Current"/> is
    /// this seen through the active Commander's overlay, and every write comes back through
    /// <see cref="CommanderScope.Persist"/> to land in the right layer.
    /// </summary>
    private D47Settings _stored;

    /// <summary>Who is flying, as the journal last said, or null before anyone has been identified.</summary>
    private string? _commanderFid;

    private string? _commanderName;

    public SettingsService(
        SettingsStore store,
        SecretStore secrets,
        D47Settings current,
        ILogger<SettingsService> logger)
    {
        _store = store;
        _secrets = secrets;
        _logger = logger;
        _stored = current;
        Current = current;
    }

    /// <summary>
    /// The settings as they are right now, for whoever is flying. Replaced wholesale, never
    /// mutated.
    /// <para>
    /// A projection, not the file: the installation's record with the active Commander's own
    /// values over the rows that are theirs (<see cref="CommanderScope.Project"/>). Every reader
    /// — the panel, the prompt, the tool surface — reads this and never the document, which is
    /// what lets a Commander row be declared rather than special-cased at each place it is read.
    /// </para>
    /// </summary>
    public D47Settings Current { get; private set; }

    /// <summary>
    /// Re-reads the settings for the Commander the journal now says is flying (list.md Phase 44).
    /// <para>
    /// Called for an adoption and for a switch alike, and during the backlog replay as well as
    /// live: this is a pure reading of who is active — the projection is a function of the id, and
    /// nothing here is discarded — so it follows every reassignment the way a reader that asks for
    /// the id per call does, and the priming flag on the signal is for the subscribers that
    /// <em>do</em> discard something.
    /// </para>
    /// <para>
    /// Announces each <see cref="SettingScope.Commander"/> row whose effective value moved, under
    /// that row's own key, so the subscribers that re-read About Me or the core-binding selector
    /// do so exactly as they would after an edit. Nothing is persisted, because nothing changed on
    /// disk; the announcement means "what you would read has changed", which is what it meant.
    /// </para>
    /// </summary>
    public void UseCommander(string? fid, string? name = null)
    {
        if (string.Equals(fid, _commanderFid, StringComparison.Ordinal))
        {
            _commanderName = name ?? _commanderName;
            return;
        }

        var before = Current;

        _commanderFid = fid;
        _commanderName = name;
        Current = CommanderScope.Project(_stored, fid);

        _logger.LogInformation(
            "Settings now read for Commander {Name} ({Fid})",
            name ?? "(unknown)",
            fid ?? "(nobody yet)");

        foreach (var row in CommanderRows())
        {
            if (!string.Equals(row.Binding?.Read(before), row.Binding?.Read(Current), StringComparison.Ordinal))
            {
                Changed?.Invoke(new SettingsChanged(row.Key, Current));
            }
        }
    }

    /// <summary>
    /// The rows declared per Commander, or none before <see cref="Bind"/> — a service with no row
    /// table has nobody to announce to.
    /// </summary>
    private IEnumerable<SettingRow> CommanderRows() =>
        _byKey?.Values.Where(row => row.Scope == SettingScope.Commander) ?? [];

    /// <summary>
    /// Writes a change made against <see cref="Current"/> to disk, in the layer it belongs to, and
    /// re-projects. True when something was written.
    /// </summary>
    private bool Persist(D47Settings next)
    {
        var stored = CommanderScope.Persist(_stored, Current, next, _commanderFid, _commanderName);

        if (ReferenceEquals(stored, _stored))
        {
            return false;
        }

        _store.Save(stored);
        _stored = stored;
        Current = CommanderScope.Project(stored, _commanderFid);

        return true;
    }

    /// <summary>
    /// Raised after a change is persisted. Subscribers re-read what they care about — the
    /// verbosity control, the turn loop's model and endpoint, the theme. Raised on the calling
    /// thread; a UI subscriber marshals for itself.
    /// </summary>
    public event Action<SettingsChanged>? Changed;

    /// <summary>
    /// Raised after every attempt to set a row, whatever came of it.
    /// <para>
    /// Separate from <see cref="Changed"/> on purpose. That one means "a change was persisted,
    /// go and re-read what you care about", and subscribers act on it; widening it to carry
    /// failures would have them re-reading state after a save that did not happen. This one
    /// carries no state at all — only what was attempted and how it went.
    /// </para>
    /// <para>
    /// Exists for the coverage recorder, which is off unless asked for; nothing in the shipped
    /// path subscribes.
    /// </para>
    /// </summary>
    public event Action<SettingApplied>? Applied;

    public IReadOnlyList<SettingsSection> Sections =>
        _sections ?? throw new InvalidOperationException(
            "The settings service has no rows until Bind() is called with the capability registry.");

    /// <summary>
    /// Supplies the row table from the registry.
    /// <para>
    /// Rows are declared by capability descriptors and some descriptors read settings, so one
    /// of those two edges has to be late-bound. This is the safer one: the row table is inert
    /// until a surface renders or a caller applies a change, whereas a descriptor holding a
    /// half-built service could read one at construction time. Called once, from the
    /// composition root, immediately after the registry is built.
    /// </para>
    /// </summary>
    public void Bind(CapabilityRegistry registry)
    {
        if (_sections is not null)
        {
            throw new InvalidOperationException("The settings service is already bound to a registry.");
        }

        _sections =
        [
            .. registry.All
                .Where(c => c.Descriptor.Settings.Count > 0)
                .OrderBy(c => c.Descriptor.Display.Order)
                .Select(c => new SettingsSection(c.Descriptor, c.Descriptor.Settings))
        ];

        _byKey = _sections
            .SelectMany(s => s.Rows)
            .ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var row in _byKey.Values)
        {
            // A row with nothing behind it renders as a control that silently does nothing,
            // which is worse than a missing row. Fail at startup instead.
            var wired = row.Kind switch
            {
                SettingKind.Secret => row.SecretName is not null,
                SettingKind.Info => row.Binding?.Read is not null,
                _ => row.Binding?.Write is not null,
            };

            if (!wired)
            {
                throw new CapabilityRegistrationException(
                    $"Settings row '{row.Key}' is a {row.Kind} row with nothing bound behind it.");
            }

            // Same rule, for the button an Info row may carry: one with no words on it is a
            // control the Commander cannot know the effect of until they press it.
            if (row.Press is not null && string.IsNullOrWhiteSpace(row.PressLabel))
            {
                throw new CapabilityRegistrationException(
                    $"Settings row '{row.Key}' offers a button with nothing written on it.");
            }
        }

        _logger.LogInformation(
            "Settings surface bound: {Rows} rows across {Sections} sections, {Protected} protected",
            _byKey.Count,
            _sections.Count,
            _byKey.Values.Count(r => r.Protected));
    }

    public SettingRow? Find(string key) =>
        (_byKey ?? throw new InvalidOperationException("Bind() has not been called."))
        .GetValueOrDefault(key);

    /// <summary>
    /// The row's current value, or null when no choice has been made and the default stands.
    /// Secret rows always read null — that is the write-only property, and it holds for every
    /// caller including the panel that just set one.
    /// </summary>
    public string? Read(string key) =>
        Find(key) is { Kind: not SettingKind.Secret, Binding: { } binding } ? binding.Read(Current) : null;

    /// <summary>
    /// Whether this row's value differs from what a fresh install would show
    /// (<a href="https://github.com/dseelinger/d47/issues/61">#61</a>).
    /// <para>
    /// <b>Needs no new state, which is what makes it cheap.</b>
    /// <see cref="D47Settings.Defaults"/> is a settings document with nothing chosen in it, and
    /// a row's own binding read against that is what the row would say on a fresh install. So
    /// "changed" is a comparison between two reads rather than a flag somebody has to remember
    /// to set — and a row put back by hand stops being changed without anything being told.
    /// </para>
    /// <para>
    /// <b>Not simply "the value is non-null".</b> That was the first shape of this and it was
    /// wrong for most of the surface: a row like the theme always reads a value, because its
    /// unset state <em>is</em> a value. Only the rows whose binding returns null when nothing is
    /// chosen — the model, the endpoint, About Me — would have answered correctly, and every
    /// toggle and every choice with a default would have claimed to be changed on a fresh
    /// install.
    /// </para>
    /// <para>
    /// <b>A per-Commander row asks a different question</b>, and it is the one the glyph should
    /// answer: not "does this differ from the shipped default" but "do I have my own answer
    /// here". A Commander looking at a value the installation set has nothing of theirs to undo.
    /// </para>
    /// <para>
    /// Always false for a secret. A key is not a setting with a default to fall back to, and
    /// clearing one is destructive in a way nothing else here is.
    /// </para>
    /// </summary>
    public bool IsChanged(string key)
    {
        if (Find(key) is not { Kind: not SettingKind.Secret, Binding: { } binding } row)
        {
            return false;
        }

        if (row.Scope == SettingScope.Commander)
        {
            return CommanderScope.WithOneFieldForgotten(_stored, _commanderFid).Any(candidate =>
                !string.Equals(
                    binding.Read(CommanderScope.Project(candidate, _commanderFid)),
                    binding.Read(Current),
                    StringComparison.Ordinal));
        }

        return !string.Equals(binding.Read(Current), binding.Read(D47Settings.Defaults), StringComparison.Ordinal);
    }

    /// <summary>
    /// Puts a row back to its default (#61).
    /// <para>
    /// <b>The mechanism already existed and had never been used.</b>
    /// <see cref="SettingBinding.Write"/> takes a <c>string?</c>, and a null written value means
    /// "no choice made" — so reset is a write of null, with no default table to author and
    /// nothing to keep in step with the shipped defaults.
    /// </para>
    /// <para>
    /// <b>It is not reachable from the tool surface, and that is the point rather than an
    /// omission.</b> Protected is a property of the caller: a <c>reset_settings</c> tool would
    /// hand the model one call that reaches every protected row at once, which is the exact thing
    /// the invariant exists to prevent. There is no reset tool at any scope, and this refuses a
    /// model caller on a protected row exactly as <see cref="Apply"/> does, because it is
    /// <see cref="Apply"/> that does it.
    /// </para>
    /// <para>
    /// <b>A secret is never reset.</b> Forgetting a key is destructive and unrecoverable — the
    /// Commander has to go and find it again — so it is a separate, differently worded, confirmed
    /// action rather than something a card-level reset sweeps up. Somebody asking for a working
    /// Speech tab is not asking to be logged out of ElevenLabs.
    /// </para>
    /// </summary>
    public SettingApplyResult Reset(string key, SettingsCaller caller)
    {
        if (Find(key) is not { } row)
        {
            return new SettingApplyResult(SettingApplyStatus.UnknownKey, $"There is no setting called '{key}'.");
        }

        if (row.Kind == SettingKind.Secret)
        {
            return new SettingApplyResult(
                SettingApplyStatus.Refused,
                $"'{row.Label}' is a stored key, not a setting with a default. Forgetting it is its own action.");
        }

        // A row that is the Commander's own resets by forgetting their answer rather than by
        // writing a blank one — see CommanderScope.WithOneFieldForgotten for why an ordinary
        // write cannot express that.
        if (row.Scope == SettingScope.Commander && ForgetCommanderAnswer(row) is { } forgotten)
        {
            return forgotten;
        }

        return Apply(key, null, caller);
    }

    /// <summary>
    /// Every row on one capability's card, put back to its default (#61). Returns how many moved.
    /// <para>
    /// <b>The gesture that matters when things are haywire.</b> A Commander who has been fiddling
    /// with twenty-two Speech rows does not know which one did it, and "reset Speech" is what they
    /// actually want to say.
    /// </para>
    /// <para>
    /// Secrets are not swept up, and rows the Commander never touched are not written — so this
    /// is exactly as destructive as the changes it is undoing and no more.
    /// </para>
    /// </summary>
    public int ResetCard(string capabilityId, SettingsCaller caller)
    {
        var rows = Sections
            .Where(section => string.Equals(section.Capability.Id, capabilityId, StringComparison.Ordinal))
            .SelectMany(section => section.Rows)
            .Where(row => IsChanged(row.Key))
            .Select(row => row.Key)
            .ToList();

        return rows.Count(key => Reset(key, caller).Status == SettingApplyStatus.Applied);
    }

    /// <summary>
    /// Removes this Commander's own answer for a row, or null when there is nothing of theirs to
    /// remove and the ordinary write should handle it.
    /// <para>
    /// Which of their fields the row is asking about is found by trying each and seeing which one
    /// moves this row's value — the same rule <c>CommanderScopeTests</c> uses to decide which rows
    /// the overlay reaches, rather than a second list of keys that could disagree with it.
    /// </para>
    /// </summary>
    private SettingApplyResult? ForgetCommanderAnswer(SettingRow row)
    {
        if (row.Binding is not { } binding)
        {
            return null;
        }

        var mine = binding.Read(Current);

        foreach (var candidate in CommanderScope.WithOneFieldForgotten(_stored, _commanderFid))
        {
            var without = CommanderScope.Project(candidate, _commanderFid);

            if (string.Equals(binding.Read(without), mine, StringComparison.Ordinal))
            {
                continue;
            }

            _store.Save(candidate);
            _stored = candidate;
            Current = CommanderScope.Project(candidate, _commanderFid);

            _logger.LogInformation("Reset {Key} to the installation's value", row.Key);
            Changed?.Invoke(new SettingsChanged(row.Key, Current));
            Applied?.Invoke(new SettingApplied(row.Key, SettingApplyStatus.Applied));

            return new SettingApplyResult(SettingApplyStatus.Applied, $"{row.Label} is back to its default.");
        }

        return null;
    }

    /// <summary>Whether a secret has a value stored. Never what it is.</summary>
    public bool HasSecret(string? secretName) =>
        secretName is not null && _secrets.Has(secretName);

    /// <summary>
    /// Writes settings that are not a row.
    /// <para>
    /// Every setting a Commander can change is a row, and rows are how the protected rule, the
    /// validation and the picker all work — so this is deliberately not a general escape hatch.
    /// It exists for derived state that lives in the settings file because it must survive a
    /// restart, but that nobody types: the voice paired to each persona is the case (list.md
    /// Phase 11, #33), chosen in the background from a list the Commander never sees.
    /// </para>
    /// <para>
    /// It still saves and still announces, under <paramref name="reason"/>, so a subscriber
    /// rebuilding from a change cannot miss one of these.
    /// </para>
    /// </summary>
    public void Replace(string reason, Func<D47Settings, D47Settings> change)
    {
        var next = change(Current);

        if (next == Current)
        {
            return;
        }

        try
        {
            if (!Persist(next))
            {
                return;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Losing a derived value is survivable — it is derived. Losing the app over it is not.
            _logger.LogError(ex, "Could not persist {Reason}", reason);
            return;
        }

        _logger.LogInformation("Stored {Reason}", reason);
        Changed?.Invoke(new SettingsChanged(reason, Current));
    }

    public SettingApplyResult Apply(string key, string? value, SettingsCaller caller)
    {
        var result = ApplyCore(key, value, caller);

        // Announced under the row's own key rather than the caller's spelling of it. Keys are
        // matched case-insensitively, so "Listening.Ptt" and "listening.ptt" are one row, and a
        // subscriber matching against the registry would otherwise miss whichever one it was
        // not expecting.
        Applied?.Invoke(new SettingApplied(Find(key)?.Key ?? key, result.Status));

        return result;
    }

    private SettingApplyResult ApplyCore(string key, string? value, SettingsCaller caller)
    {
        if (Find(key) is not { } row)
        {
            return new SettingApplyResult(SettingApplyStatus.UnknownKey, $"There is no setting called '{key}'.");
        }

        // The whole of the protected rule, in one place. Secrets are model-unreachable
        // whether or not anyone remembered to mark the row protected as well.
        if (caller == SettingsCaller.Model && (row.Protected || row.Kind == SettingKind.Secret))
        {
            _logger.LogWarning("Refused a model-initiated change to protected setting {Key}", key);
            return new SettingApplyResult(
                SettingApplyStatus.Refused,
                $"'{row.Label}' is protected. It can be changed from the settings panel, but not by me.");
        }

        if (row.Kind == SettingKind.Info)
        {
            return new SettingApplyResult(
                SettingApplyStatus.Refused, $"'{row.Label}' is something D47 reports, not something you set.");
        }

        // A key is data, not a mode. Which provider is selected decides whether a row is *shown*
        // and which service d47 actually calls; it does not decide whether a credential may be
        // stored. The first-run window offers the ElevenLabs key while Edge is still the selected
        // voice on purpose (MainWindow.ShowKeySetupAsync) — a Commander pastes the key and then
        // goes looking for the voice, and SpeechWiring is built for precisely that order. Gating
        // the write on applicability made that order impossible, and said so in words that read
        // as though the key itself had been rejected.
        //
        // Secrets stay model-unreachable regardless: that is refused above, before this.
        if (row.Kind != SettingKind.Secret && !row.Applies(Current))
        {
            return new SettingApplyResult(
                SettingApplyStatus.Rejected,
                $"'{row.Label}' does not apply with the current selection.");
        }

        var requested = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        if (row.Kind == SettingKind.Secret)
        {
            return ApplySecret(row, requested, caller);
        }

        if (!TryNormalise(row, requested, out var normalised))
        {
            return new SettingApplyResult(
                SettingApplyStatus.Rejected,
                row.ChoicesFor(Current) is { Count: > 0 } choices
                    ? $"'{value}' is not a valid {row.Label}. Expected one of: {string.Join(", ", choices)}."
                    : $"'{value}' is not a valid {row.Label}.");
        }

        // Said here rather than discovered later. A system-wide key with no modifier cannot be
        // registered — it would take that key from every other application, the game included —
        // and refusing it on the row that was just pressed puts the reason in front of the
        // Commander who pressed it, rather than somewhere they are not looking.
        if (row.Kind == SettingKind.Hotkey && row.SystemWide
            && normalised is { Length: > 0 } gesture && !gesture.Contains('+'))
        {
            return new SettingApplyResult(
                SettingApplyStatus.Rejected,
                $"{row.Label} works everywhere, so it needs a modifier — try Ctrl, Alt or Shift with "
                + "it. On its own, that key would stop working in every other application, Elite included.");
        }

        if (string.Equals(row.Binding!.Read(Current), normalised, StringComparison.Ordinal))
        {
            return new SettingApplyResult(SettingApplyStatus.Unchanged, $"{row.Label} is already {Describe(normalised)}.");
        }

        var next = row.Binding.Write!(Current, normalised);

        try
        {
            // A Commander row lands in the Commander's overlay and an installation row in the
            // file's body; the row does not know which it is and does not need to (list.md
            // Phase 44). A row whose write changes nothing on disk — the core-for-that-ship row
            // writes to its own store and hands the settings back untouched — still announces,
            // because what it changed is what a subscriber reads through the settings.
            Persist(next);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Could not persist a change to {Key}", key);
            return new SettingApplyResult(
                SettingApplyStatus.Failed, $"{row.Label} could not be saved: {ex.Message}");
        }

        _logger.LogInformation("{Caller} set {Key} to {Value}", caller, key, Describe(normalised));
        // row.Key, not key, for the same reason Applied uses it — and matching what the two
        // other raise sites already do.
        Changed?.Invoke(new SettingsChanged(row.Key, Current));

        return new SettingApplyResult(SettingApplyStatus.Applied, $"{row.Label} is now {Describe(normalised)}.");
    }

    private SettingApplyResult ApplySecret(SettingRow row, string? value, SettingsCaller caller)
    {
        var name = row.SecretName!;

        if (value is null)
        {
            var removed = _secrets.Remove(name);
            if (removed)
            {
                // A key change alters what the provider can do, so it announces itself like any
                // other setting even though the value never entered D47Settings.
                Changed?.Invoke(new SettingsChanged(row.Key, Current));
            }

            return new SettingApplyResult(
                removed ? SettingApplyStatus.Applied : SettingApplyStatus.Unchanged,
                removed ? $"{row.Label} cleared." : $"There was no {row.Label} to clear.");
        }

        _secrets.Set(name, value);
        _logger.LogInformation("{Caller} stored a new value for {Key}", caller, row.Key);
        Changed?.Invoke(new SettingsChanged(row.Key, Current));

        return new SettingApplyResult(SettingApplyStatus.Applied, $"{row.Label} stored.");
    }

    /// <summary>
    /// Produces the canonical string to store. Canonical matters: "TRUE", "on" and "True" are
    /// the same setting, and a row whose stored form varies by caller reads as changed when it
    /// is not — which would show up as an unnecessary write and an unnecessary announcement.
    /// </summary>
    private bool TryNormalise(SettingRow row, string? value, out string? normalised)
    {
        // Clearing is always legal: it restores the default, which is what the placeholder has
        // been advertising all along.
        normalised = null;

        if (value is null)
        {
            return true;
        }

        switch (row.Kind)
        {
            case SettingKind.Toggle:
                normalised = value.ToLowerInvariant() switch
                {
                    "true" or "on" or "yes" or "enabled" or "1" => "true",
                    "false" or "off" or "no" or "disabled" or "0" => "false",
                    _ => null,
                };

                return normalised is not null;

            case SettingKind.Number:
                // Float rather than Integer, and the row's own format on the way out. A row
                // that wants whole numbers declares a step of 1 and gets exactly what it got
                // before; a row whose value is a fraction can finally hold one.
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    return false;
                }

                normalised = number.ToString(row.NumberFormat, CultureInfo.InvariantCulture);
                return true;

            case SettingKind.Choice:
                var choices = row.ChoicesFor(Current);
                normalised = choices.FirstOrDefault(c => string.Equals(c, value, StringComparison.OrdinalIgnoreCase));

                if (normalised is not null)
                {
                    return true;
                }

                // Fail-soft by contract: an endpoint d47 has never seen still has model names,
                // so a row that says so accepts a value it cannot offer (list.md Phase 4).
                normalised = row.AllowsFreeText ? value : null;
                return normalised is not null;

            default:
                normalised = value;
                return true;
        }
    }

    private static string Describe(string? value) => value ?? "(default)";
}
