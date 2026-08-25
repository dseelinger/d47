namespace D47.Core.Hotas;

/// <summary>
/// Push-to-talk on a stick button (list.md Phase 53).
/// <para>
/// <b>The same shape as the keyboard's <c>PushToTalkKey</c>, one layer down.</b> A polled edge
/// detector sampled from the tick, raising <see cref="Pressed"/> and <see cref="Released"/>, and
/// the polling rate is not a risk: the keyboard path already detects push-to-talk on this same
/// tick, so a button read here is no less responsive than the key it replaces.
/// </para>
/// <para>
/// It lives in Core where the keyboard one cannot, because reading a controller is already a Core
/// contract (<see cref="IHotasReader"/>) while reading a key is a P/Invoke. That is worth the
/// asymmetry: it means the whole of this — the edges, the absent device, the fallback — is
/// driveable with nothing plugged in.
/// </para>
/// </summary>
public sealed class PushToTalkButton
{
    private HotasButton? _bound;
    private bool _wasDown;
    private bool _sawDevice;

    /// <summary>Raised on the tick that first sees the button down.</summary>
    public event Action? Pressed;

    /// <summary>Raised on the tick that first sees it back up.</summary>
    public event Action? Released;

    /// <summary>What is bound, or null. Read for reporting and for the settings row.</summary>
    public HotasButton? Bound => _bound;

    public bool IsDown => _wasDown;

    /// <summary>
    /// Whether the bound device has been seen at all since binding.
    /// <para>
    /// <b>Null means nothing is bound</b>, which is not the same as a device that is missing, and
    /// the two must not collapse: one is a Commander who never set this up and the other is one
    /// whose stick is asleep. Only the second is worth interrupting them about.
    /// </para>
    /// </summary>
    public bool? DevicePresent => _bound is null ? null : _sawDevice;

    /// <summary>Binds a button, or unbinds with null. Returns whether anything is bound after.</summary>
    public bool Bind(HotasButton? button)
    {
        ForceUp();

        _bound = button;
        _sawDevice = false;

        return _bound is not null;
    }

    /// <summary>
    /// Samples the controllers.
    /// <para>
    /// A device that is not in the readings leaves the button reading as up rather than as
    /// unchanged, so unplugging a stick mid-transmission closes the gate instead of stranding it
    /// open — the listening equivalent of the stranded key <c>release_all()</c> exists for.
    /// </para>
    /// </summary>
    public void Poll(IReadOnlyList<HotasReading> readings)
    {
        if (_bound is not { } bound)
        {
            return;
        }

        var device = readings.FirstOrDefault(reading =>
            string.Equals(reading.Id, bound.DeviceId, StringComparison.Ordinal));

        if (device is not null)
        {
            _sawDevice = true;
        }

        var down = device?.IsHeld(bound.Button) ?? false;

        if (down == _wasDown)
        {
            return;
        }

        _wasDown = down;

        if (down)
        {
            Pressed?.Invoke();
        }
        else
        {
            Released?.Invoke();
        }
    }

    /// <summary>
    /// Forces it to read as released — for a settings change, or for shutdown. Rebinding while
    /// held would otherwise leave the gate open with nothing able to close it.
    /// </summary>
    public void ForceUp()
    {
        if (!_wasDown)
        {
            return;
        }

        _wasDown = false;
        Released?.Invoke();
    }
}

/// <summary>
/// The two push-to-talk sources as one gate (list.md Phase 53).
/// <para>
/// <b>Either triggers, and both are live</b> (the Commander's call, 2026-08-25). A Commander who
/// bound a key and later bound a button has said two things rather than replaced one, and
/// <c>settings.json</c> is append-only, so they are two keys beside each other rather than one key
/// learning a second meaning.
/// </para>
/// <para>
/// The combining is a hold count rather than an <c>or</c> of two booleans, because the interesting
/// case is both at once: pressing the key, then the button, then letting go of the key must not
/// close the gate while a finger is still down. Releasing on the <em>last</em> release is the only
/// behaviour that is not surprising.
/// </para>
/// </summary>
public sealed class PushToTalkSources
{
    private bool _keyDown;
    private bool _buttonDown;

    public event Action? Pressed;

    public event Action? Released;

    public bool IsDown => _keyDown || _buttonDown;

    public void KeyPressed() => Set(ref _keyDown, true);

    public void KeyReleased() => Set(ref _keyDown, false);

    public void ButtonPressed() => Set(ref _buttonDown, true);

    public void ButtonReleased() => Set(ref _buttonDown, false);

    private void Set(ref bool source, bool down)
    {
        var was = IsDown;

        source = down;

        if (IsDown == was)
        {
            return;
        }

        if (IsDown)
        {
            Pressed?.Invoke();
        }
        else
        {
            Released?.Invoke();
        }
    }
}
