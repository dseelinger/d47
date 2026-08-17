namespace D47.Core.Hotas;

/// <summary>
/// One reading of one controller interface, as Core sees it (list.md Phase 21, "Read the
/// Commander's controllers").
/// <para>
/// <b>An interface, not a product.</b> The WinWing throttle alone presents four of these with
/// 4x32 mode on, all sharing one VID and PID, and only <see cref="Id"/> separates them
/// (docs/spikes/hotas-switch-read.md, finding 2).
/// </para>
/// <para>
/// <b><see cref="Hats"/> is what <c>Windows.Gaming.Input</c> calls a switch.</b> The two names
/// are swapped here on purpose. A hat reports a compass position, always returns to centre, and
/// is declined by capture; the thing this phase calls a switch is a maintained toggle that shows
/// up in <see cref="Buttons"/>. Leaving the platform's name on it would mean the one type in this
/// namespace called "switch" is the one thing that is not one.
/// </para>
/// </summary>
public sealed record HotasReading
{
    /// <summary>
    /// The <c>NonRoamableId</c>. The only field that separates two interfaces of one product,
    /// and the only thing a mapping is ever keyed on — see <see cref="SwitchMapping.DeviceId"/>.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>Reported for the capture report only. Never an identity — see finding 7.</summary>
    public ushort VendorId { get; init; }

    public ushort ProductId { get; init; }

    /// <summary>Held or not, by button index. Sixteen were held at rest on the bench.</summary>
    public required IReadOnlyList<bool> Buttons { get; init; }

    /// <summary>
    /// Hat positions, as the platform's own enumeration values. Read only so capture can
    /// recognise a hat and decline it; nothing else in the phase looks at these.
    /// </summary>
    public IReadOnlyList<int> Hats { get; init; } = [];

    public int AxisCount { get; init; }

    /// <summary>Whether a button index exists on this device and is currently held.</summary>
    public bool IsHeld(int button) => button >= 0 && button < Buttons.Count && Buttons[button];

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

/// <summary>
/// Reading the Commander's controllers. Declared in Core and implemented in the app, like every
/// other hardware contract, so the replay harness can drive a switch with nothing plugged in
/// (architecture.md §8).
/// <para>
/// <b>It exposes <see cref="Poll"/> and owns no thread</b>, the same shape
/// <see cref="Journal.JournalReader"/> has and for the same reason. The implementation does own a
/// subscription — hot-plug and slow enumeration are one code path, finding 1 — but nothing here
/// decides when a reading is taken.
/// </para>
/// </summary>
public interface IHotasReader
{
    /// <summary>
    /// Whether the device list has stopped changing. False for the first moments after start,
    /// and it is the whole of finding 1: a single enumeration at startup reported three of six
    /// devices, so anything that acts on a device list has to wait for this.
    /// </summary>
    bool IsSettled { get; }

    /// <summary>Why there is nothing to read, or null. A machine with no controllers is not a fault.</summary>
    string? Unavailable { get; }

    /// <summary>
    /// One reading per device, taken now. Empty until <see cref="IsSettled"/>, so a caller that
    /// forgets to check gets nothing rather than half a stick.
    /// </summary>
    IReadOnlyList<HotasReading> Poll();
}

/// <summary>
/// An <see cref="IHotasReader"/> driven by hand. Lives in Core rather than in a test project
/// because the replay harness needs it too — the same rule
/// <see cref="Input.RecordingGameInput"/> follows, and the same reason: a second copy in the
/// tests is a second thing to keep true.
/// </summary>
public sealed class FakeHotasReader : IHotasReader
{
    private IReadOnlyList<HotasReading> _readings = [];

    public bool IsSettled { get; set; } = true;

    public string? Unavailable { get; set; }

    public int Polls { get; private set; }

    /// <summary>Sets one device holding exactly the buttons named. The ordinary case.</summary>
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

    public FakeHotasReader Set(params HotasReading[] readings)
    {
        _readings = readings;
        return this;
    }

    /// <summary>Unplugs everything, which is what a mode change looks like from here.</summary>
    public FakeHotasReader Gone()
    {
        _readings = [];
        return this;
    }

    public IReadOnlyList<HotasReading> Poll()
    {
        Polls++;
        return IsSettled ? _readings : [];
    }
}
