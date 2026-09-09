using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace D47.Core.Input;

/// <summary>One key or button assignment, as the bindings file states it.</summary>
/// <param name="Action">Elite's own action name — "YawLeftButton", "UseBoostJuice".</param>
/// <param name="Slot">"Primary" or "Secondary".</param>
/// <param name="Device">"Keyboard", "Mouse", or a joystick's device name.</param>
/// <param name="Key">Elite's key symbol — "Key_LeftAlt", "Key_W".</param>
public sealed record EliteBinding(string Action, string Slot, string Device, string Key)
{
    /// <summary>Held modifiers, which are part of the binding rather than separate from it.</summary>
    public IReadOnlyList<string> Modifiers { get; init; } = [];

    public bool IsKeyboard => string.Equals(Device, "Keyboard", StringComparison.OrdinalIgnoreCase);

    /// <summary>The binding as a comparable gesture: modifiers in a fixed order, then the key.</summary>
    public string Gesture()
    {
        var parts = Modifiers
            .Select(Friendly)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Append(Friendly(Key));

        return string.Join("+", parts);
    }

    /// <summary>
    /// Elite's key symbols to the spelling the rest of d47 uses. "Key_LeftAlt" becomes "LeftAlt", which
    /// is what a gesture parser and a Commander both expect to see.
    /// </summary>
    public static string Friendly(string key) =>
        key.StartsWith("Key_", StringComparison.OrdinalIgnoreCase) ? key[4..] : key;
}

/// <summary>The Commander's control bindings, parsed.</summary>
public sealed record EliteBinds
{
    public static readonly EliteBinds None = new();

    /// <summary>The preset that was actually in use, per StartPreset.</summary>
    public string? PresetName { get; init; }

    /// <summary>The file parsed, for saying where an answer came from.</summary>
    public string? SourceFile { get; init; }

    public IReadOnlyList<EliteBinding> Bindings { get; init; } = [];

    public bool IsKnown => SourceFile is not null;

    /// <summary>Every slot bound to one Elite action, in file order.</summary>
    public IReadOnlyList<EliteBinding> For(string action) =>
        [.. Bindings.Where(binding =>
            string.Equals(binding.Action, action, StringComparison.OrdinalIgnoreCase))];

    /// <summary>Every action a keyboard gesture is already bound to.</summary>
    public IReadOnlyList<EliteBinding> Using(string gesture) =>
        [.. Bindings.Where(binding =>
            binding.IsKeyboard &&
            string.Equals(Normalise(binding.Gesture()), Normalise(gesture), StringComparison.OrdinalIgnoreCase))];

    /// <summary>Every binding on a controller button of this index, whichever device (Phase 53).</summary>
    public IReadOnlyList<EliteBinding> UsingJoystickButton(int button) =>
        UsingJoystickButton(button, device: null);

    /// <summary>
    /// The same, narrowed to one device — everything Elite binds to a given button of a given stick
    /// (#147).
    /// </summary>
    public IReadOnlyList<EliteBinding> UsingJoystickButton(int button, string? device)
    {
        // Elite counts from one and HotasReading counts from zero, which is the off-by-one this whole feature
        // is most likely to ship.
        var name = $"Joy_{button + 1}";

        return [.. Bindings.Where(binding =>
            !binding.IsKeyboard
            && string.Equals(binding.Key, name, StringComparison.OrdinalIgnoreCase)
            && (device is not { Length: > 0 }
                || string.Equals(binding.Device, device, StringComparison.OrdinalIgnoreCase)))];
    }

    /// <summary>
    /// Elite's name for the device d47 describes as <c>VID 0x4098 PID 0xBD65, 32 buttons, …</c> — the
    /// two ids run together, <c>4098BD65</c>.
    /// </summary>
    public static string? EliteDeviceToken(string? description)
    {
        if (description is not { Length: > 0 })
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            description,
            @"VID 0x([0-9A-Fa-f]{4}) PID 0x([0-9A-Fa-f]{4})");

        return match.Success
            ? (match.Groups[1].Value + match.Groups[2].Value).ToUpperInvariant()
            : null;
    }

    /// <summary>Keyboard gestures bound to more than one action, each with the actions that share it.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<EliteBinding>> Shared()
    {
        var shared = new Dictionary<string, IReadOnlyList<EliteBinding>>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in Bindings
                     .Where(binding => binding.IsKeyboard)
                     .GroupBy(binding => Normalise(binding.Gesture()), StringComparer.OrdinalIgnoreCase))
        {
            var actions = group
                .DistinctBy(binding => binding.Action, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (actions.Length > 1)
            {
                shared[group.Key] = actions;
            }
        }

        return shared;
    }

    /// <summary>
    /// A gesture reduced to something comparable: modifier aliases folded together, order fixed, case
    /// ignored. "Ctrl+Alt+X" from the settings file and "LeftControl+LeftAlt+X" from the bindings file
    /// are the same key to the Commander pressing it.
    /// </summary>
    public static string Normalise(string gesture)
    {
        var parts = gesture
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => EliteBinding.Friendly(part))
            .Select(part => part.ToLowerInvariant() switch
            {
                "leftcontrol" or "rightcontrol" or "ctrl" or "control" => "control",
                "leftalt" or "rightalt" or "alt" => "alt",
                "leftshift" or "rightshift" or "shift" => "shift",
                "leftwin" or "rightwin" or "win" or "meta" or "cmd" => "win",
                var other => other,
            })
            .ToArray();

        var modifiers = parts.Where(IsModifier).Distinct().Order(StringComparer.Ordinal);
        var keys = parts.Where(part => !IsModifier(part));

        return string.Join("+", modifiers.Concat(keys));

        static bool IsModifier(string part) => part is "control" or "alt" or "shift" or "win";
    }

    /// <summary>Parses one .binds file.</summary>
    public static EliteBinds Parse(string path, string? presetName, ILogger logger)
    {
        XDocument document;

        try
        {
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            document = XDocument.Load(stream);
        }
        catch (Exception ex) when (ex is IOException or System.Xml.XmlException)
        {
            logger.LogWarning(ex, "Could not read the bindings file {Path}", path);
            return None;
        }

        if (document.Root is null)
        {
            return None;
        }

        var bindings = new List<EliteBinding>();

        // The shape is <Root><ActionName><Primary Device= Key=/><Secondary .../></ActionName>…>.
        foreach (var action in document.Root.Elements())
        {
            foreach (var slot in action.Elements())
            {
                if (slot.Name.LocalName is not ("Primary" or "Secondary"))
                {
                    continue;
                }

                var device = slot.Attribute("Device")?.Value;
                var key = slot.Attribute("Key")?.Value;

                // Elite writes Device="{NoDevice}" and an empty Key for an unbound slot, which is most of the
                // file.
                if (string.IsNullOrWhiteSpace(device) ||
                    string.IsNullOrWhiteSpace(key) ||
                    device.Equals("{NoDevice}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var modifiers = slot.Elements("Modifier")
                    .Select(modifier => modifier.Attribute("Key")?.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)
                    .ToArray();

                bindings.Add(new EliteBinding(action.Name.LocalName, slot.Name.LocalName, device, key)
                {
                    Modifiers = modifiers,
                });
            }
        }

        logger.LogInformation(
            "Parsed {Count} bindings from {Path} (preset {Preset})",
            bindings.Count,
            path,
            presetName ?? "unknown");

        return new EliteBinds
        {
            PresetName = presetName,
            SourceFile = path,
            Bindings = bindings,
        };
    }
}
