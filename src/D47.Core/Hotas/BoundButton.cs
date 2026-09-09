namespace D47.Core.Hotas;

/// <summary>One bound stick button, polled (Phase 53).</summary>
public sealed class BoundButton
{
    private HotasButton? _bound;
    private bool _wasDown;
    private bool _sawDevice;
    private int _pollsSinceBind;
    private bool _noticedAbsence;

    /// <summary>Raised on the tick that first sees the button down.</summary>
    public event Action? Pressed;

    /// <summary>Raised on the tick that first sees it back up.</summary>
    public event Action? Released;

    /// <summary>What is bound, or null.</summary>
    public HotasButton? Bound => _bound;

    public bool IsDown => _wasDown;

    /// <summary>How many polls the bound device gets to turn up in before its absence is called.</summary>
    public const int PollsBeforeAbsenceIsCalled = 15;

    /// <summary>Whether the bound device has been seen at all since binding.</summary>
    public bool? DevicePresent => _bound is null ? null : _sawDevice;

    /// <summary>The button whose device has not turned up, once per binding, or null.</summary>
    public HotasButton? MissingDeviceNotice()
    {
        if (_bound is not { } bound || _sawDevice || _noticedAbsence)
        {
            return null;
        }

        if (_pollsSinceBind < PollsBeforeAbsenceIsCalled)
        {
            return null;
        }

        _noticedAbsence = true;

        return bound;
    }

    /// <summary>Binds a button, or unbinds with null.</summary>
    public bool Bind(HotasButton? button)
    {
        ForceUp();

        _bound = button;
        _sawDevice = false;

        // A new binding is a new question, so the counting and the notice both start again.
        _pollsSinceBind = 0;
        _noticedAbsence = false;

        return _bound is not null;
    }

    /// <summary>Samples the controllers.</summary>
    public void Poll(IReadOnlyList<HotasReading> readings)
    {
        if (_bound is not { } bound)
        {
            return;
        }

        // Counted before the lookup, so "how many chances has it had" means polls that happened rather than
        // polls that found something.
        if (_pollsSinceBind < PollsBeforeAbsenceIsCalled)
        {
            _pollsSinceBind++;
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

    /// <summary>Forces it to read as released — for a settings change, or for shutdown.</summary>
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

/// <summary>The two push-to-talk sources as one gate (Phase 53).</summary>
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
