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
                return new ButtonCaptureResult(
                    ButtonCaptureStage.Captured,
                    holding,
                    $"Button {holding.Button + 1}. Press Save to bind push-to-talk to it.");
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

        var fresh = down.Where(button => !_atRest.Contains(button)).ToArray();

        if (fresh.Length > 1)
        {
            return new ButtonCaptureResult(
                ButtonCaptureStage.Declined,
                null,
                "Two buttons went down at once, so I cannot tell which one you meant. Try again and "
                + "press one.");
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
