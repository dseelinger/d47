namespace D47.Core.Hotas;

/// <summary>
/// One reading of one controller interface, as Core sees it (Phase 21, "Read the Commander's
/// controllers").
/// </summary>
public sealed record HotasReading
{
    /// <summary>The <see cref="HidIdentity"/> of the interface.</summary>
    public required string Id { get; init; }

    /// <summary>Reported for the capture report only.</summary>
    public ushort VendorId { get; init; }

    public ushort ProductId { get; init; }

    /// <summary>Held or not, by button index.</summary>
    public required IReadOnlyList<bool> Buttons { get; init; }

    /// <summary>Buttons that were down in any sample since the last read; empty when the reader does not track it.</summary>
    public IReadOnlyList<bool> DownSinceLastRead { get; init; } = [];

    /// <summary>
    /// A running count of presses by button index, each up-to-down change the reader saw; empty when the reader
    /// does not count. Never reset on a read, so each consumer keeps the last count it saw.
    /// </summary>
    public IReadOnlyList<int> Presses { get; init; } = [];

    /// <summary>Hat positions, as the platform's own enumeration values.</summary>
    public IReadOnlyList<int> Hats { get; init; } = [];

    public int AxisCount { get; init; }

    /// <summary>Whether a button index exists on this device and is currently held.</summary>
    public bool IsHeld(int button) => button >= 0 && button < Buttons.Count && Buttons[button];

    /// <summary>The running press count of a button index, or null when the reader does not count it.</summary>
    public int? PressCount(int button) => button >= 0 && button < Presses.Count ? Presses[button] : null;

    /// <summary>Whether a button index was down in any sample since the last read, or is held now.</summary>
    public bool WasDownSinceLastRead(int button) =>
        IsHeld(button) || (button >= 0 && button < DownSinceLastRead.Count && DownSinceLastRead[button]);

    /// <summary>Every held button index, ascending.</summary>
    public IReadOnlyList<int> Held()
    {
        var held = new List<int>();

        for (var index = 0; index < Buttons.Count; index++)
        {
            if (Buttons[index])
            {
                held.Add(index);
            }
        }

        return held;
    }

    /// <summary>How a device is named in a report, since Windows will not name it — finding 2.</summary>
    public string Describe() =>
        $"VID 0x{VendorId:X4} PID 0x{ProductId:X4}, {Buttons.Count} buttons, {Hats.Count} hats, {AxisCount} axes";
}

/// <summary>Reading the Commander's controllers.</summary>
public interface IHotasReader
{
    /// <summary>Whether the device list has stopped changing.</summary>
    bool IsSettled { get; }

    /// <summary>Why there is nothing to read, or null.</summary>
    string? Unavailable { get; }

    /// <summary>One reading per device, taken now.</summary>
    IReadOnlyList<HotasReading> Poll();
}

/// <summary>An <see cref="IHotasReader"/> driven by hand.</summary>
public sealed class FakeHotasReader : IHotasReader
{
    private IReadOnlyList<HotasReading> _readings = [];

    public bool IsSettled { get; set; } = true;

    public string? Unavailable { get; set; }

    public int Polls { get; private set; }

    /// <summary>Sets one device holding exactly the buttons named.</summary>
    public FakeHotasReader Holding(string id, int buttonCount, params int[] held)
    {
        var buttons = new bool[buttonCount];

        foreach (var button in held)
        {
            buttons[button] = true;
        }

        _readings = [new HotasReading { Id = id, Buttons = buttons }];

        return this;
    }

    /// <summary>Sets one device holding the buttons named, with a running press count for each of its buttons.</summary>
    public FakeHotasReader Counting(string id, int[] presses, params int[] held)
    {
        Holding(id, presses.Length, held);
        _readings = [_readings[0] with { Presses = [.. presses] }];

        return this;
    }

    public FakeHotasReader Set(params HotasReading[] readings)
    {
        _readings = readings;
        return this;
    }

    public IReadOnlyList<HotasReading> Poll()
    {
        Polls++;
        return IsSettled ? _readings : [];
    }
}
