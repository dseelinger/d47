namespace D47.Core.Hotas;

/// <summary>Where a button capture has got to.</summary>
public enum ButtonCaptureStage
{
    /// <summary>Waiting for a press. Nothing has been touched yet.</summary>
    Waiting,

    /// <summary>Something is being held. Waiting for it to come back.</summary>
    Held,

    /// <summary>Pressed and released. There is a button.</summary>
    Captured,

    /// <summary>Not a button d47 will bind, with the reason. Never guessed at.</summary>
    Declined,
}

/// <param name="Stage">Where the capture got to.</param>
/// <param name="Binding">The button, when there is one.</param>
/// <param name="Says">What to show. A sentence in every stage, declines included.</param>
public sealed record ButtonCaptureResult(ButtonCaptureStage Stage, HotasButton? Binding, string Says)
{
    public bool IsOver => Stage is ButtonCaptureStage.Captured or ButtonCaptureStage.Declined;
}

/// <summary>
/// <em>Press the button you want</em> (Phase 53).
/// <para>
/// <b>The opposite case to <see cref="SwitchCapture"/>, and that is why it is a second walk
/// rather than a flag on the first.</b> A switch needs a <em>position</em> to mean anything, so
/// Phase 21 had to decline every button that springs home — it had nothing for the reconciler to
/// reconcile. Push-to-talk is the mirror image: momentary is not a limitation there, it is the
/// entire mechanism. Between them the two walks partition the hardware rather than competing
/// for it.
/// </para>
/// <para>
/// <b>Buttons already held when the walk starts are ignored.</b> Sixteen were held at rest on the
/// bench — that is what a maintained switch looks like from here — so a capture that took the
/// first button it saw held would bind a switch position the Commander never touched.
/// </para>
/// <para>
/// <b>Captured on release rather than on press</b>, which is what tells a button from a switch
/// without guessing at durations. The Phase 21 spike settled that duration cannot do it: a switch
/// going home by itself ran 407-1611 ms against 206-1751 ms for a Commander walking past, and
/// those overlap. <em>Did it come back at all</em> is a different question and is not close: a
/// momentary button returns in about a second, and a maintained switch does not return until it
/// is moved again. So the ceiling below is generous rather than discriminating.
/// </para>
/// </summary>
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

    /// <summary>
    /// One sample. <paramref name="elapsed"/> is time since the walk began, injected for the
    /// reason every clock in Core is injected.
    /// </summary>
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

        // A switch moved back to a position it was resting in when the walk opened stops being
        // at rest, so a later press of it is seen. Without this, walking a switch off and back
        // during the capture would make that position invisible for the rest of the walk.
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
