using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace D47.Core.Input;

/// <summary>One key or button assignment, as the bindings file states it.</summary>
/// <param name="Action">Elite's own action name — "YawLeftButton", "UseBoostJuice".</param>
/// <param name="Slot">"Primary" or "Secondary". Elite allows two bindings per action.</param>
/// <param name="Device">"Keyboard", "Mouse", or a joystick's device name.</param>
/// <param name="Key">Elite's key symbol — "Key_LeftAlt", "Key_W".</param>
public sealed record EliteBinding(string Action, string Slot, string Device, string Key)
{
    /// <summary>Held modifiers, which are part of the binding rather than separate from it.</summary>
    public IReadOnlyList<string> Modifiers { get; init; } = [];

    public bool IsKeyboard => string.Equals(Device, "Keyboard", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The binding as a comparable gesture: modifiers in a fixed order, then the key. Sorted
    /// because Elite writes them in binding order and "Ctrl+Alt+X" and "Alt+Ctrl+X" are the
    /// same gesture to everything except a string comparison.
    /// </summary>
    public string Gesture()
    {
        var parts = Modifiers
            .Select(Friendly)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Append(Friendly(Key));

        return string.Join("+", parts);
    }

    /// <summary>
    /// Elite's key symbols to the spelling the rest of d47 uses. "Key_LeftAlt" becomes "LeftAlt",
    /// which is what a gesture parser and a Commander both expect to see.
    /// </summary>
    public static string Friendly(string key) =>
        key.StartsWith("Key_", StringComparison.OrdinalIgnoreCase) ? key[4..] : key;
}

/// <summary>
/// The Commander's control bindings, parsed. <b>Read-only, always</b> — d47 never writes a
/// bindings file (architecture.md D4).
/// <para>
/// One parse feeds two things: this phase's double-bind detection, and Phase 10's
/// keyboard-action reachability. Keeping a second view of the Commander's controls is how the
/// two end up disagreeing about what is bound.
/// </para>
/// </summary>
public sealed record EliteBinds
{
    public static readonly EliteBinds None = new();

    /// <summary>The preset that was actually in use, per StartPreset. Null when unresolved.</summary>
    public string? PresetName { get; init; }

    /// <summary>The file parsed, for saying where an answer came from.</summary>
    public string? SourceFile { get; init; }

    public IReadOnlyList<EliteBinding> Bindings { get; init; } = [];

    public bool IsKnown => SourceFile is not null;

    /// <summary>
    /// Every slot bound to one Elite action, in file order. Usually none or one; two when the
    /// Commander has filled both Primary and Secondary, which is common on a HOTAS setup where
    /// the stick holds one and the keyboard holds the other.
    /// </summary>
    public IReadOnlyList<EliteBinding> For(string action) =>
        [.. Bindings.Where(binding =>
            string.Equals(binding.Action, action, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// Every action a keyboard gesture is already bound to. This is the question the
    /// push-to-talk key has to ask: a key Elite is already using has no symptom beyond the
    /// binding quietly not working, in one direction or the other.
    /// </summary>
    public IReadOnlyList<EliteBinding> Using(string gesture) =>
        [.. Bindings.Where(binding =>
            binding.IsKeyboard &&
            string.Equals(Normalise(binding.Gesture()), Normalise(gesture), StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// Every binding on a controller button of this index, whichever device (Phase 53).
    /// <para>
    /// <b>Deliberately weaker than <see cref="Using"/>, and the weakness is stated where it is
    /// used.</b> Elite writes a joystick binding against its own device hash —
    /// <c>Device="4098BD65" Key="Joy_24"</c> — and that hash is not the <c>NonRoamableId</c>
    /// d47 reads through <c>Windows.Gaming.Input</c>. Nothing published joins the two, so d47
    /// cannot tell whether Elite's <c>Joy_24</c> is on the <em>same</em> stick as the button being
    /// bound. What it can say is that a button of that number is spoken for somewhere, which on a
    /// HOTAS is worth saying: a key bound in both places has no symptom other than not working, in
    /// one direction or the other, and a stick is far likelier than a keyboard to have every
    /// button already used.
    /// </para>
    /// <para>
    /// So this reports and the caller hedges. A false warning costs a sentence; a missed one costs
    /// an evening of a microphone that will not open.
    /// </para>
    /// </summary>
    public IReadOnlyList<EliteBinding> UsingJoystickButton(int button)
    {
        // Elite counts from one and HotasReading counts from zero, which is the off-by-one this
        // whole feature is most likely to ship.
        var name = $"Joy_{button + 1}";

        return [.. Bindings.Where(binding =>
            !binding.IsKeyboard
            && string.Equals(binding.Key, name, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// Keyboard gestures bound to more than one action, each with the actions that share it.
    /// <para>
    /// Elite binds the same key in different control modes on purpose — the ship, the SRV and
    /// on-foot each have their own set, and a shared key across two of them is normal rather
    /// than a fault. So this reports what is shared and leaves the judgement to the caller,
    /// which for the push-to-talk check is exactly what is wanted: d47's key colliding with
    /// <em>any</em> mode is a real collision.
    /// </para>
    /// </summary>
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
    /// A gesture reduced to something comparable: modifier aliases folded together, order
    /// fixed, case ignored. "Ctrl+Alt+X" from the settings file and "LeftControl+LeftAlt+X"
    /// from the bindings file are the same key to the Commander pressing it.
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

    /// <summary>
    /// Parses one .binds file. Malformed XML produces no bindings rather than an exception: an
    /// unreadable bindings file is a capability being unavailable, not a reason for d47 to fail
    /// to start (Phase 3, "Capabilities as state, not guard").
    /// </summary>
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
        // Actions are elements rather than a list, so the action name is the element name and
        // anything without a Primary or Secondary child is not a binding at all.
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

                // Elite writes Device="{NoDevice}" and an empty Key for an unbound slot, which
                // is most of the file. Treating those as bindings would have every unbound
                // action colliding with every other one.
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
