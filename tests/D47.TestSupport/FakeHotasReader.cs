namespace D47.Core.Hotas;

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
