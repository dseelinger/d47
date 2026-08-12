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

    public SettingsService(
        SettingsStore store,
        SecretStore secrets,
        D47Settings current,
        ILogger<SettingsService> logger)
    {
        _store = store;
        _secrets = secrets;
        _logger = logger;
        Current = current;
    }

    /// <summary>The settings as they are right now. Replaced wholesale, never mutated.</summary>
    public D47Settings Current { get; private set; }

    /// <summary>
    /// Raised after a change is persisted. Subscribers re-read what they care about — the
    /// verbosity control, the turn loop's model and endpoint, the theme. Raised on the calling
    /// thread; a UI subscriber marshals for itself.
    /// </summary>
    public event Action<SettingsChanged>? Changed;

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

    /// <summary>Whether a secret has a value stored. Never what it is.</summary>
    public bool HasSecret(string? secretName) =>
        secretName is not null && _secrets.Has(secretName);

    public SettingApplyResult Apply(string key, string? value, SettingsCaller caller)
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
                SettingApplyStatus.Refused, $"'{row.Label}' is something d47 reports, not something you set.");
        }

        if (!row.Applies(Current))
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

        if (string.Equals(row.Binding!.Read(Current), normalised, StringComparison.Ordinal))
        {
            return new SettingApplyResult(SettingApplyStatus.Unchanged, $"{row.Label} is already {Describe(normalised)}.");
        }

        var next = row.Binding.Write!(Current, normalised);

        try
        {
            _store.Save(next);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Could not persist a change to {Key}", key);
            return new SettingApplyResult(
                SettingApplyStatus.Failed, $"{row.Label} could not be saved: {ex.Message}");
        }

        Current = next;
        _logger.LogInformation("{Caller} set {Key} to {Value}", caller, key, Describe(normalised));
        Changed?.Invoke(new SettingsChanged(key, next));

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
                if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                {
                    return false;
                }

                normalised = number.ToString(CultureInfo.InvariantCulture);
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
