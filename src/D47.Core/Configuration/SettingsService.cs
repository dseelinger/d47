using System.Globalization;
using D47.Core.Capabilities;
using Microsoft.Extensions.Logging;

namespace D47.Core.Configuration;

/// <summary>Who is asking.</summary>
public enum SettingsCaller
{
    /// <summary>The settings surface, driven by the Commander's own hands.</summary>
    Panel,

    /// <summary>A bound gesture.</summary>
    Hotkey,

    /// <summary>The model-free command path.</summary>
    KeywordRouter,

    /// <summary>A tool call.</summary>
    Model,

    /// <summary>A binding the Commander made, acting (Phase 35).</summary>
    ShipBinding,
}

public enum SettingApplyStatus
{
    Applied,

    /// <summary>The value was already what was asked for.</summary>
    Unchanged,

    UnknownKey,

    /// <summary>The value is not one this row accepts.</summary>
    Rejected,

    /// <summary>The caller is not allowed to change this row.</summary>
    Refused,

    /// <summary>The change was valid but could not be persisted.</summary>
    Failed,
}

public sealed record SettingApplyResult(SettingApplyStatus Status, string Message)
{
    public bool Ok => Status is SettingApplyStatus.Applied or SettingApplyStatus.Unchanged;
}

public sealed record SettingsChanged(string Key, D47Settings Settings);

/// <summary>One attempt to set a row, and what came of it — including the attempts that changed nothing.</summary>
public sealed record SettingApplied(string Key, SettingApplyStatus Status);

/// <summary>One capability's rows, in the order the capability declared them.</summary>
public sealed record SettingsSection(CapabilityDescriptor Capability, IReadOnlyList<SettingRow> Rows);

/// <summary>The one place a setting changes.</summary>
public sealed class SettingsService
{
    private readonly SettingsStore _store;
    private readonly SecretStore _secrets;
    private readonly ILogger<SettingsService> _logger;

    private IReadOnlyList<SettingsSection>? _sections;
    private Dictionary<string, SettingRow>? _byKey;

    /// <summary>The document as it is on disk — both layers (Phase 44).</summary>
    private D47Settings _stored;

    /// <summary>Who is flying, as the journal last said, or null before anyone has been identified.</summary>
    private string? _commanderFid;

    private string? _commanderName;

    /// <summary>
    /// True when what this service holds is defaults standing in for a file that refused to load.
    /// </summary>
    private readonly bool _loadFailed;

    /// <summary><param name="current"> What the store loaded, or defaults if it could not.</summary>
    /// <param name="current">What the store loaded, or defaults if it could not.</param>
    /// <param name="loadFailed">
    /// Whether <paramref name="current"/> is defaults because the file refused to load (#368).
    /// </param>
    public SettingsService(
        SettingsStore store,
        SecretStore secrets,
        D47Settings current,
        ILogger<SettingsService> logger,
        bool loadFailed = false)
    {
        _store = store;
        _secrets = secrets;
        _logger = logger;
        _stored = current;
        _loadFailed = loadFailed;
        Current = current;
    }

    /// <summary>The settings as they are right now, for whoever is flying.</summary>
    public D47Settings Current { get; private set; }

    /// <summary>Re-reads the settings for the Commander the journal now says is flying (Phase 44).</summary>
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
    /// The rows declared per Commander, or none before <see cref="Bind"/> — a service with no row table
    /// has nobody to announce to.
    /// </summary>
    private IEnumerable<SettingRow> CommanderRows() =>
        _byKey?.Values.Where(row => row.Scope == SettingScope.Commander) ?? [];

    /// <summary>
    /// Writes a change made against <see cref="Current"/> to disk, in the layer it belongs to, and
    /// re-projects.
    /// </summary>
    private bool Persist(D47Settings next)
    {
        var stored = CommanderScope.Persist(_stored, Current, next, _commanderFid, _commanderName);

        if (ReferenceEquals(stored, _stored))
        {
            return false;
        }

        SaveUnlessTheFileRefusedToLoad(stored);

        _stored = stored;
        Current = CommanderScope.Project(stored, _commanderFid);

        return true;
    }

    /// <summary>Writes the document, unless the file it would land on refused to load (#368).</summary>
    private void SaveUnlessTheFileRefusedToLoad(D47Settings stored)
    {
        if (_loadFailed)
        {
            _logger.LogWarning(
                "Not writing {Path}: it did not load, so what is in memory is defaults rather than "
                + "the Commander's settings",
                _store.SettingsFile);

            return;
        }

        _store.Save(stored);
    }

    /// <summary>
    /// What a Commander is told when their change took effect but cannot be written down (#368).
    /// </summary>
    private string NotRemembered(string what) =>
        $"{what}, but it will not be remembered: {_store.SettingsFile} could not be read at "
        + "startup, and writing over it would replace your settings with defaults. Fix the file "
        + "and restart.";

    /// <summary>Raised after a change is persisted.</summary>
    public event Action<SettingsChanged>? Changed;

    /// <summary>Raised after every attempt to set a row, whatever came of it.</summary>
    public event Action<SettingApplied>? Applied;

    public IReadOnlyList<SettingsSection> Sections =>
        _sections ?? throw new InvalidOperationException(
            "The settings service has no rows until Bind() is called with the capability registry.");

    /// <summary>Supplies the row table from the registry.</summary>
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
            // A row with nothing behind it renders as a control that silently does nothing, which is worse
            // than a missing row.
            var wired = row.Kind switch
            {
                SettingKind.Secret => row.SecretName is not null,
                SettingKind.Info => row.Binding?.Read is not null || row.Press is not null || row.PressAsync is not null,
                _ => row.Binding?.Write is not null,
            };

            if (!wired)
            {
                throw new CapabilityRegistrationException(
                    $"Settings row '{row.Key}' is a {row.Kind} row with nothing bound behind it.");
            }

            // Same rule, for the button an Info row may carry: one with no words on it is a control the
            // Commander cannot know the effect of until they press it.
            if ((row.Press is not null || row.PressAsync is not null) && string.IsNullOrWhiteSpace(row.PressLabel))
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

    /// <summary>The row's current value, or null when no choice has been made and the default stands.</summary>
    public string? Read(string key) =>
        Find(key) is { Kind: not SettingKind.Secret, Binding: { } binding } ? binding.Read(Current) : null;

    /// <summary>Whether this row's value differs from what a fresh install would show (#61).</summary>
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

    /// <summary>Puts a row back to its default (#61).</summary>
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

        // A row that is the Commander's own resets by forgetting their answer rather than by writing a blank
        // one — see CommanderScope.WithOneFieldForgotten for why an ordinary write cannot express that.
        if (row.Scope == SettingScope.Commander && ForgetCommanderAnswer(row) is { } forgotten)
        {
            return forgotten;
        }

        return Apply(key, null, caller);
    }

    /// <summary>Every row on one capability's card, put back to its default (#61).</summary>
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
    /// Removes this Commander's own answer for a row, or null when there is nothing of theirs to remove
    /// and the ordinary write should handle it.
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

            SaveUnlessTheFileRefusedToLoad(candidate);
            _stored = candidate;
            Current = CommanderScope.Project(candidate, _commanderFid);

            _logger.LogInformation("Reset {Key} to the installation's value", row.Key);
            Changed?.Invoke(new SettingsChanged(row.Key, Current));

            // The same fence, and the same telling, as an ordinary change (#368): live for this run, refused
            // as a status, so nothing counts it as remembered.
            var status = _loadFailed ? SettingApplyStatus.Failed : SettingApplyStatus.Applied;

            Applied?.Invoke(new SettingApplied(row.Key, status));

            return new SettingApplyResult(
                status,
                _loadFailed
                    ? NotRemembered($"{row.Label} is back to its default")
                    : $"{row.Label} is back to its default.");
        }

        return null;
    }

    /// <summary>Whether a secret has a value stored.</summary>
    public bool HasSecret(string? secretName) =>
        secretName is not null && _secrets.Has(secretName);

    /// <summary>Writes settings that are not a row.</summary>
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
            // Losing a derived value is survivable — it is derived.
            _logger.LogError(ex, "Could not persist {Reason}", reason);
            return;
        }

        _logger.LogInformation("Stored {Reason}", reason);
        Changed?.Invoke(new SettingsChanged(reason, Current));
    }

    public SettingApplyResult Apply(string key, string? value, SettingsCaller caller)
    {
        var result = ApplyCore(key, value, caller);

        // Announced under the row's own key rather than the caller's spelling of it.
        Applied?.Invoke(new SettingApplied(Find(key)?.Key ?? key, result.Status));

        return result;
    }

    private SettingApplyResult ApplyCore(string key, string? value, SettingsCaller caller)
    {
        if (Find(key) is not { } row)
        {
            return new SettingApplyResult(SettingApplyStatus.UnknownKey, $"There is no setting called '{key}'.");
        }

        // The whole of the protected rule, in one place.
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

        // A key is data, not a mode.
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

        // Said here rather than discovered later.
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
            // A Commander row lands in the Commander's overlay and an installation row in the file's body;
            // the row does not know which it is and does not need to (Phase 44).
            Persist(next);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Could not persist a change to {Key}", key);
            return new SettingApplyResult(
                SettingApplyStatus.Failed, $"{row.Label} could not be saved: {ex.Message}");
        }

        _logger.LogInformation("{Caller} set {Key} to {Value}", caller, key, Describe(normalised));
        // row.Key, not key, for the same reason Applied uses it — and matching what the two other raise sites
        // already do.
        Changed?.Invoke(new SettingsChanged(row.Key, Current));

        if (_loadFailed)
        {
            return new SettingApplyResult(
                SettingApplyStatus.Failed,
                NotRemembered($"{row.Label} is now {Describe(normalised)}"));
        }

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
                // A key change alters what the provider can do, so it announces itself like any other setting
                // even though the value never entered D47Settings.
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

    /// <summary>Produces the canonical string to store.</summary>
    private bool TryNormalise(SettingRow row, string? value, out string? normalised)
    {
        // Clearing is always legal: it restores the default, which is what the placeholder has been
        // advertising all along.
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
                // Float rather than Integer, and the row's own format on the way out.
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

                // Fail-soft by contract: an endpoint d47 has never seen still has model names, so a row that
                // says so accepts a value it cannot offer (Phase 4).
                normalised = row.AllowsFreeText ? value : null;
                return normalised is not null;

            default:
                normalised = value;
                return true;
        }
    }

    private static string Describe(string? value) => value ?? "(default)";
}
