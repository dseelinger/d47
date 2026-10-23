using D47.Core.Configuration;

namespace D47.Core.Hotas;

/// <summary>A lost device found again by flipping one of its switches.</summary>
/// <param name="OldId">The <see cref="HidIdentity"/> the switches were learned on.</param>
/// <param name="NewId">The id the device has now.</param>
/// <param name="Device">The found device's <see cref="HotasReading.Describe"/>.</param>
/// <param name="Flipped">The stored name of the switch that was flipped.</param>
/// <param name="Fills">Whether the flipped switch stored no device text, so it moves alone and takes <paramref name="Device"/>.</param>
public sealed record FoundDevice(string OldId, string NewId, string Device, string Flipped, bool Fills)
{
    /// <summary>Whether a switch with this stored id, device text and stored name moves to the new id.</summary>
    public bool Covers(string deviceId, string device, string name) =>
        string.Equals(deviceId, OldId, StringComparison.Ordinal)
        && (Fills
            ? device.Length == 0 && string.Equals(name, Flipped, StringComparison.OrdinalIgnoreCase)
            : string.Equals(device, Device, StringComparison.Ordinal));

    /// <summary>The stored set with every covered switch on the new id; nothing else changes.</summary>
    public IReadOnlyList<SwitchMapping> Apply(IReadOnlyList<SwitchMapping> stored) =>
    [
        .. stored.Select(mapping => Covers(mapping.DeviceId, mapping.Device, mapping.Name)
            ? mapping with { DeviceId = NewId, Device = Fills ? Device : mapping.Device }
            : mapping),
    ];

    /// <summary>How many of the stored set <see cref="Apply"/> moves.</summary>
    public int Count(IReadOnlyList<SwitchMapping> stored) =>
        stored.Count(mapping => Covers(mapping.DeviceId, mapping.Device, mapping.Name));

    /// <summary>
    /// The settings with push-to-talk and cancel moved to the new id, keeping their button numbers. Only a
    /// found device whose text matched the stored one moves them; a switch that took its text from this flip
    /// has not shown the old and new ids to be the same interface.
    /// </summary>
    public D47Settings RepointButtons(D47Settings settings, out IReadOnlyList<string> moved)
    {
        var names = new List<string>();

        if (Fills)
        {
            moved = names;
            return settings;
        }

        var talk = Repoint(settings.Listening.PushToTalkButton);
        var cancel = Repoint(settings.Speech.CancelButton);

        if (talk is not null)
        {
            names.Add("push-to-talk");
            settings = settings with { Listening = settings.Listening with { PushToTalkButton = talk } };
        }

        if (cancel is not null)
        {
            names.Add("cancel");
            settings = settings with { Speech = settings.Speech with { CancelButton = cancel } };
        }

        moved = names;
        return settings;
    }

    private string? Repoint(string? stored) =>
        HotasButton.Parse(stored) is { } button && string.Equals(button.DeviceId, OldId, StringComparison.Ordinal)
            ? (button with { DeviceId = NewId }).ToString()
            : null;

    /// <summary>What the card says once the switches, and any buttons in <paramref name="buttons"/>, have moved.</summary>
    public string Says(int moved, IReadOnlyList<string>? buttons = null)
    {
        var things = new List<string> { $"{moved} {(moved == 1 ? "switch" : "switches")}" };
        things.AddRange(buttons ?? []);

        var list = things.Count == 1
            ? things[0]
            : $"{string.Join(", ", things[..^1])} and {things[^1]}";

        return $"Found the device — {list} re-pointed.";
    }
}

/// <summary>What one poll of <see cref="LostDeviceFinder"/> decided.</summary>
public sealed record FindResult
{
    public static readonly FindResult None = new();

    /// <summary>The device found, or null.</summary>
    public FoundDevice? Found { get; init; }

    /// <summary>Why a flip did not re-point, by the stored name of the switch it moved.</summary>
    public IReadOnlyDictionary<string, string> Refused { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Finds a switch whose device is no longer read by watching for that switch to flip on another device.
/// Fed every reading the HOTAS switches page takes; owns no thread.
/// </summary>
public sealed class LostDeviceFinder
{
    /// <summary>The last reading per (stored name, device id) at which the switch was at one of its positions.</summary>
    private readonly Dictionary<(string Name, string Id), (HotasReading Reading, SwitchPosition Position)> _anchors =
        new(new AnchorComparer());

    /// <summary>
    /// One reading of every device against the stored switches. An empty reading is from a reader that has not
    /// settled and is ignored.
    /// </summary>
    public FindResult Poll(IReadOnlyList<HotasReading> readings, IReadOnlyList<SwitchMapping> stored)
    {
        if (readings.Count == 0)
        {
            return FindResult.None;
        }

        var lost = stored.Where(mapping => readings.All(reading => reading.Id != mapping.DeviceId)).ToList();

        Forget(lost);

        var refused = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        FoundDevice? found = null;

        foreach (var reading in readings)
        {
            var accepted = new List<SwitchMapping>();

            foreach (var mapping in lost)
            {
                if (!FlippedOn(mapping, reading, out var moved))
                {
                    continue;
                }

                if (Refusal(mapping, reading, moved, stored) is { } why)
                {
                    refused[mapping.Name] = why;
                    continue;
                }

                accepted.Add(mapping);
            }

            if (accepted.Count == 0)
            {
                continue;
            }

            if (accepted.Select(mapping => mapping.DeviceId).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                foreach (var mapping in accepted)
                {
                    refused[mapping.Name] = "That flip fits switches from more than one missing device, so D47 "
                                            + "cannot tell which one this is. Flip a different switch.";
                }

                continue;
            }

            // One device found per poll; a switch flipped on a second device in the same poll has to be flipped again.
            if (found is null)
            {
                var flipped = accepted[0];

                found = new FoundDevice(
                    flipped.DeviceId, reading.Id, reading.Describe(), flipped.Name, flipped.Device.Length == 0);

                foreach (var mapping in accepted)
                {
                    refused.Remove(mapping.Name);
                }
            }
        }

        return found is null && refused.Count == 0 ? FindResult.None : new FindResult { Found = found, Refused = refused };
    }

    /// <summary>
    /// Whether the switch moved from one of its positions to another on this device since the last reading that
    /// placed it, and which buttons changed between the two.
    /// </summary>
    private bool FlippedOn(SwitchMapping mapping, HotasReading reading, out IReadOnlyList<int> moved)
    {
        moved = [];

        // Between positions, or two of its buttons held at once: the anchor stays where it was.
        if (mapping.At(reading) is not { } position)
        {
            return false;
        }

        var key = (mapping.Name, reading.Id);

        if (!_anchors.TryGetValue(key, out var anchor))
        {
            _anchors[key] = (reading, position);
            return false;
        }

        _anchors[key] = (reading, position);

        if (anchor.Position.Button == position.Button)
        {
            return false;
        }

        moved = Changed(anchor.Reading, reading);

        return true;
    }

    private static string? Refusal(
        SwitchMapping mapping, HotasReading reading, IReadOnlyList<int> moved, IReadOnlyList<SwitchMapping> stored)
    {
        var buttons = mapping.Positions.Select(position => position.Button).OfType<int>().ToHashSet();
        var strays = moved.Where(button => !buttons.Contains(button)).ToList();

        if (strays.Count > 0)
        {
            var plural = strays.Count == 1 ? "button" : "buttons";

            return $"That flip also moved {plural} {string.Join(", ", strays)}, which this switch does not use, "
                   + "so D47 cannot be sure it was this switch. Flip it again on its own.";
        }

        if (stored.FirstOrDefault(other => other.DeviceId == reading.Id) is { } working)
        {
            return $"That flip came from a device {working.Name} is already working on, so it is not the missing one.";
        }

        if (mapping.Device.Length > 0 && !string.Equals(mapping.Device, reading.Describe(), StringComparison.Ordinal))
        {
            return "That flip came from a device that does not match the one this switch was learned on. "
                   + "If the throttle's 4x32 mode was turned on or off, assign it again.";
        }

        return null;
    }

    /// <summary>Every button whose held state differs between two readings of one device.</summary>
    private static List<int> Changed(HotasReading before, HotasReading after)
    {
        var changed = new List<int>();
        var count = Math.Max(before.Buttons.Count, after.Buttons.Count);

        for (var button = 0; button < count; button++)
        {
            if (before.IsHeld(button) != after.IsHeld(button))
            {
                changed.Add(button);
            }
        }

        return changed;
    }

    /// <summary>Drops the anchors of switches that are no longer missing their device.</summary>
    private void Forget(List<SwitchMapping> lost)
    {
        var names = lost.Select(mapping => mapping.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var key in _anchors.Keys.Where(key => !names.Contains(key.Name)).ToList())
        {
            _anchors.Remove(key);
        }
    }

    private sealed class AnchorComparer : IEqualityComparer<(string Name, string Id)>
    {
        public bool Equals((string Name, string Id) x, (string Name, string Id) y) =>
            string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Id, y.Id, StringComparison.Ordinal);

        public int GetHashCode((string Name, string Id) key) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(key.Name), StringComparer.Ordinal.GetHashCode(key.Id));
    }
}
