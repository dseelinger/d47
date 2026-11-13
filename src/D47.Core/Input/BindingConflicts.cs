using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Hotas;

namespace D47.Core.Input;

/// <summary>What kind of d47 binding a conflict is on.</summary>
public enum BindingKind
{
    Key,
    Button,
    SwitchPosition,
}

/// <summary>A d47 binding that acts while Elite has the foreground, and the Elite actions bound to the same press.</summary>
/// <param name="Control">The d47 control: a settings row's label, or a switch's name.</param>
/// <param name="RowKey">The settings key of the key or button slot, or null for a switch position.</param>
/// <param name="Switch">The switch mapping's name, or null for a settings row.</param>
/// <param name="Position">The switch position as the Switch page prints it, or null for a settings row.</param>
/// <param name="Gesture">The key or button as the panel prints it.</param>
/// <param name="EliteActions">Elite's action names, distinct, in bindings-file order.</param>
public sealed record BindingConflict(
    string Control,
    string? RowKey,
    string? Switch,
    string? Position,
    BindingKind Kind,
    string Gesture,
    IReadOnlyList<string> EliteActions);

/// <summary>
/// The certain conflicts between d47's bindings and Elite's. A stick button counts only when its controller shows exactly
/// one interface; a controller that shows several, or is not attached, is left out.
/// </summary>
public static class BindingConflicts
{
    /// <summary>Every certain conflict among the rows that act while Elite has the foreground and the assigned switch positions.</summary>
    /// <param name="keyLabel">Prints a stored key gesture; the stored form is used when null.</param>
    public static IReadOnlyList<BindingConflict> Find(
        D47Settings settings,
        IReadOnlyList<SettingRow> rows,
        IReadOnlyList<SwitchMapping> switches,
        EliteBinds binds,
        IReadOnlyList<HotasReading> readings,
        Func<string, string>? keyLabel = null) =>
        [.. ForRows(settings, rows, binds, readings, keyLabel), .. ForSwitches(switches, binds, readings)];

    /// <summary>The conflicts on the key and button slots of the rows that act while Elite has the foreground.</summary>
    private static IReadOnlyList<BindingConflict> ForRows(
        D47Settings settings,
        IReadOnlyList<SettingRow> rows,
        EliteBinds binds,
        IReadOnlyList<HotasReading> readings,
        Func<string, string>? keyLabel)
    {
        var conflicts = new List<BindingConflict>();

        foreach (var row in rows.Where(ActsWhileEliteIsInFront))
        {
            if (row.Binding?.Read(settings) is { Length: > 0 } key && ForKey(key, binds) is { Count: > 0 } keyActions)
            {
                conflicts.Add(new BindingConflict(
                    row.Label, row.Key, null, null, BindingKind.Key, keyLabel?.Invoke(key) ?? key, keyActions));
            }

            if (row.AlsoBinds is { } buttonKey
                && rows.FirstOrDefault(other => other.Key == buttonKey) is { Kind: SettingKind.HotasButton } buttonRow
                && HotasButton.Parse(buttonRow.Binding?.Read(settings)) is { } button
                && ForButton(button, binds, readings) is { Count: > 0 } buttonActions)
            {
                conflicts.Add(new BindingConflict(
                    row.Label, buttonKey, null, null, BindingKind.Button, button.Describe(), buttonActions));
            }
        }

        return conflicts;
    }

    /// <summary>The conflicts on every assigned position of every switch.</summary>
    private static IReadOnlyList<BindingConflict> ForSwitches(
        IReadOnlyList<SwitchMapping> switches,
        EliteBinds binds,
        IReadOnlyList<HotasReading> readings) =>
        [.. switches.SelectMany(mapping => mapping.Positions
            .Select(position => ForSwitchPosition(mapping, position, binds, readings))
            .OfType<BindingConflict>())];

    /// <summary>The conflict on one switch position, or null.</summary>
    public static BindingConflict? ForSwitchPosition(
        SwitchMapping mapping,
        SwitchPosition position,
        EliteBinds binds,
        IReadOnlyList<HotasReading> readings)
    {
        if (!position.IsAssigned || position.Button is not { } button)
        {
            return null;
        }

        var actions = ForButton(new HotasButton(mapping.DeviceId, button), binds, readings);

        return actions.Count == 0
            ? null
            : new BindingConflict(
                mapping.Name, null, mapping.Name, position.Describe(), BindingKind.SwitchPosition, position.Describe(), actions);
    }

    /// <summary>
    /// The Elite actions a press stored in one key or button slot would share, by the rule <see cref="Find"/> uses;
    /// none for a slot that does not act while Elite has the foreground.
    /// </summary>
    public static IReadOnlyList<string> ForPress(
        D47Settings settings,
        IReadOnlyList<SettingRow> rows,
        string slotKey,
        string value,
        EliteBinds binds,
        IReadOnlyList<HotasReading> readings)
    {
        if (rows.FirstOrDefault(row => row.Key == slotKey)?.Binding?.Write is not { } write)
        {
            return [];
        }

        return ForRows(write(settings, value), rows, binds, readings, keyLabel: null)
            .FirstOrDefault(conflict => conflict.RowKey == slotKey)?.EliteActions ?? [];
    }

    /// <summary>The d47 control as the conflicts page names it: a button by its own row's label, a switch with its position.</summary>
    public static string ControlName(BindingConflict conflict, IReadOnlyList<SettingRow> rows) => conflict.Kind switch
    {
        BindingKind.Button => rows.FirstOrDefault(row => row.Key == conflict.RowKey)?.Label ?? conflict.Control,
        BindingKind.SwitchPosition => $"Switch {conflict.Control}, {conflict.Position}",
        _ => conflict.Control,
    };

    /// <summary>The Settings row's sentence: how many conflicts, naming the first three controls.</summary>
    public static string Summary(IReadOnlyList<string> controls)
    {
        const int Named = 3;

        return controls.Count switch
        {
            0 => "No D47 key or button is bound in Elite",
            1 => $"1 of D47's keys and buttons is bound in Elite too: {controls[0]}",
            <= Named => $"{controls.Count} of D47's keys and buttons are bound in Elite too: {Listed(controls)}",
            _ => $"{controls.Count} of D47's keys and buttons are bound in Elite too: "
                 + $"{string.Join(", ", controls.Take(Named))} and {controls.Count - Named} more",
        };
    }

    /// <summary>Why a press Elite binds is refused on the conflicts page.</summary>
    public static string EliteBindsIt(string press, IReadOnlyList<string> actions, bool button, string old) =>
        $"Elite binds {press} to {Listed(actions)}. Press another {(button ? "button" : "key")}, or click elsewhere to keep {old}.";

    /// <summary>"A", "A and B", "A, B and C".</summary>
    private static string Listed(IReadOnlyList<string> items) =>
        items.Count <= 1
            ? string.Concat(items)
            : $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}";

    /// <summary>The Elite actions on a keyboard gesture.</summary>
    private static IReadOnlyList<string> ForKey(string gesture, EliteBinds binds) => Actions(binds.Using(gesture));

    /// <summary>The Elite actions on a stick button, or none when its controller does not show exactly one interface.</summary>
    private static IReadOnlyList<string> ForButton(HotasButton button, EliteBinds binds, IReadOnlyList<HotasReading> readings) =>
        binds.SharingButton(button, readings) is { Certain: true } sharing ? Actions(sharing.Bindings) : [];

    /// <summary>Push-to-talk and comms push-to-talk are sampled whatever window is in front; other hotkeys only when system-wide.</summary>
    private static bool ActsWhileEliteIsInFront(SettingRow row) =>
        row.Kind == SettingKind.Hotkey
        && (row.SystemWide || row.Key is ListeningCapability.PushToTalkKeyKey or ListeningCapability.CommsPushToTalkKeyKey);

    private static IReadOnlyList<string> Actions(IReadOnlyList<EliteBinding> bindings) =>
        [.. bindings.Select(binding => binding.Action).Distinct(StringComparer.OrdinalIgnoreCase)];
}
