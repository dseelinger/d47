namespace D47.Core.Hotas;

/// <summary>Where a button capture has got to.</summary>
public enum ButtonCaptureStage
{
    /// <summary>Waiting for a press.</summary>
    Waiting,

    /// <summary>Something is being held.</summary>
    Held,

    /// <summary>Pressed and released.</summary>
    Captured,

    /// <summary>Not a button d47 will bind, with the reason.</summary>
    Declined,
}

/// <summary>
/// <param name="Stage">Where the capture got to.</param> <param name="Binding">The button, when there
/// is one.</param> <param name="Says">What to show.
/// </summary>
/// <param name="Stage">Where the capture got to.</param>
/// <param name="Binding">The button, when there is one.</param>
/// <param name="Says">What to show.</param>
public sealed record ButtonCaptureResult(ButtonCaptureStage Stage, HotasButton? Binding, string Says)
{
    public bool IsOver => Stage is ButtonCaptureStage.Captured or ButtonCaptureStage.Declined;
}

/// <summary>Press the button you want (Phase 53).</summary>
public sealed class ButtonCapture
{
    /// <summary>How long a held button may stay held before it is called a maintained switch.</summary>
    public static readonly TimeSpan HoldCeiling = TimeSpan.FromSeconds(5);

    /// <summary>How long the whole walk waits for anything to happen.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly HashSet<HotasButton> _atRest = [];
    private readonly Dictionary<string, IReadOnlyList<int>> _presses = new(StringComparer.Ordinal);

    private bool _baselined;
    private HotasButton? _holding;
    private TimeSpan _heldSince;

    /// <summary>One sample.</summary>
    public ButtonCaptureResult Poll(IReadOnlyList<HotasReading> readings, TimeSpan elapsed)
    {
        var down = new HashSet<HotasButton>();

        foreach (var reading in readings)
        {
            foreach (var button in reading.Held())
            {
                down.Add(new HotasButton(reading.Id, button));
            }
        }

        var rose = Rose(readings);

        // Everything already held when the walk opens is a switch sitting where it was left.
        if (!_baselined)
        {
            _baselined = true;

            foreach (var held in down)
            {
                _atRest.Add(held);
            }

            return Waiting(elapsed);
        }

        if (_holding is { } holding)
        {
            if (!down.Contains(holding))
            {
                return Captured(holding);
            }

            return elapsed - _heldSince > HoldCeiling
                ? new ButtonCaptureResult(
                    ButtonCaptureStage.Declined,
                    null,
                    "That one stays where you put it, so it is a switch rather than a button. "
                    + "Maintained switches are assigned on the switch panel; push-to-talk needs one "
                    + "that springs back.")
                : new ButtonCaptureResult(ButtonCaptureStage.Held, null, "Holding… let go when you are ready.");
        }

        var fresh = down.Where(button => !_atRest.Contains(button)).Union(rose).ToArray();

        if (fresh.Length > 1)
        {
            return new ButtonCaptureResult(
                ButtonCaptureStage.Declined,
                null,
                "Two buttons went down at once, so I cannot tell which one you meant. Try again and "
                + "press one.");
        }

        if (fresh.Length == 1 && !down.Contains(fresh[0]))
        {
            return Captured(fresh[0]);
        }

        if (fresh.Length == 1)
        {
            _holding = fresh[0];
            _heldSince = elapsed;

            return new ButtonCaptureResult(ButtonCaptureStage.Held, null, "Holding… let go when you are ready.");
        }

        // A switch moved back to a position it was resting in when the walk opened stops being at rest, so a
        // later press of it is seen.
        _atRest.RemoveWhere(button => !down.Contains(button));

        return Waiting(elapsed);
    }

    /// <summary>
    /// The buttons whose press count rose since the previous poll. A device not seen before, or a count that
    /// fell, sets a new baseline.
    /// </summary>
    private List<HotasButton> Rose(IReadOnlyList<HotasReading> readings)
    {
        var rose = new List<HotasButton>();

        foreach (var reading in readings)
        {
            if (_presses.TryGetValue(reading.Id, out var last))
            {
                for (var button = 0; button < Math.Min(last.Count, reading.Presses.Count); button++)
                {
                    if (reading.Presses[button] > last[button])
                    {
                        rose.Add(new HotasButton(reading.Id, button));
                    }
                }
            }

            _presses[reading.Id] = reading.Presses;
        }

        return rose;
    }

    private static ButtonCaptureResult Captured(HotasButton button) =>
        new(ButtonCaptureStage.Captured, button, $"Button {button.Button + 1}. Press Save to bind push-to-talk to it.");

    private static ButtonCaptureResult Waiting(TimeSpan elapsed) =>
        elapsed > Patience
            ? new ButtonCaptureResult(
                ButtonCaptureStage.Declined,
                null,
                "Nothing was pressed, so nothing has changed.")
            : new ButtonCaptureResult(
                ButtonCaptureStage.Waiting,
                null,
                "Press and release the button you want to talk with.");
}
