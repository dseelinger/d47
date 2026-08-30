namespace D47.Core.Hotas;

/// <summary>
/// Push-to-talk on a stick button (Phase 53).
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
    private int _pollsSinceBind;
    private bool _noticedAbsence;

    /// <summary>Raised on the tick that first sees the button down.</summary>
    public event Action? Pressed;

    /// <summary>Raised on the tick that first sees it back up.</summary>
    public event Action? Released;

    /// <summary>What is bound, or null. Read for reporting and for the settings row.</summary>
    public HotasButton? Bound => _bound;

    public bool IsDown => _wasDown;

    /// <summary>
    /// How many polls the bound device gets to turn up in before its absence is called.
    /// <para>
    /// <b>Fifteen, which is 1.5 s at the tick's 10 Hz.</b> That is <c>HotasControllers.Settle</c>'s
    /// figure, arrived at for this same hardware; the six-second one beside it is not the right
    /// number here, because that one waits for a first device to exist at all and this is asked
    /// only after the enumeration has already settled.
    /// </para>
    /// <para>
    /// <b>Counted in polls rather than timed</b>, because no Core component reads the clock and
    /// this one has no need to be handed one — the same shape as
    /// <c>LlmAvailabilityState.ProbeAfterTurns</c>. It is sound because this button is polled from
    /// the tick and from nowhere else, so a poll is a known interval rather than an arbitrary one.
    /// </para>
    /// </summary>
    public const int PollsBeforeAbsenceIsCalled = 15;

    /// <summary>
    /// Whether the bound device has been seen at all since binding.
    /// <para>
    /// <b>Null means nothing is bound</b>, which is not the same as a device that is missing, and
    /// the two must not collapse: one is a Commander who never set this up and the other is one
    /// whose stick is asleep. Only the second is worth interrupting them about.
    /// </para>
    /// <para>
    /// <b>There is a third state and this property does not carry it.</b> <c>false</c> here means
    /// "not seen yet", which is <em>looked and it is absent</em> only once something has looked.
    /// Read at the instant of binding it is always <c>false</c>, and that is not an answer — it is
    /// the question not yet asked. Anything deciding whether to tell the Commander their stick is
    /// missing wants <see cref="MissingDeviceNotice"/> instead, which is the same fact with the
    /// looking done.
    /// </para>
    /// </summary>
    public bool? DevicePresent => _bound is null ? null : _sawDevice;

    /// <summary>
    /// The button whose device has not turned up, once per binding, or null.
    /// <para>
    /// <b>The question is not answerable at bind time, so it is not asked there</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/45">#45</a>). It used to be: the warning
    /// was raised on the line after <see cref="Bind"/>, where <see cref="DevicePresent"/> is
    /// always false because nothing has polled — so a Commander who bound a button on a stick
    /// sitting right in front of them was told it was not there, eight seconds before speaking
    /// through it.
    /// </para>
    /// <para>
    /// <b>Null until the device has had its fair chance</b>, and non-null exactly once after that
    /// — a warning worth saying is worth saying once, and this is polled ten times a second.
    /// Binding again re-arms it, because that is a new question about a new button.
    /// </para>
    /// <para>
    /// The obvious alternative — making <see cref="DevicePresent"/> null until the first poll —
    /// silences the false warning and the true one together, because the old caller asked once
    /// and never again. A Commander whose stick really is unplugged would then hear nothing,
    /// which is the case the warning exists for.
    /// </para>
    /// </summary>
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

    /// <summary>Binds a button, or unbinds with null. Returns whether anything is bound after.</summary>
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

        // Counted before the lookup, so "how many chances has it had" means polls that happened
        // rather than polls that found something.
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
/// The two push-to-talk sources as one gate (Phase 53).
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
/// <para>
/// <b>A press interrupts</b> (<a href="https://github.com/dseelinger/d47/issues/218">#218</a>).
/// The Commander's words: <em>"I don't want to talk while the ship AI is talking. I want it to
/// shut up and listen."</em> So <see cref="Barge"/> runs on the press edge, and the ordering is
/// here rather than in a subscription order at the call site, which is a guarantee nothing can
/// test and anybody can reverse by adding a handler.
/// </para>
/// </summary>
public sealed class PushToTalkSources
{
    private bool _keyDown;
    private bool _buttonDown;

    /// <summary>
    /// What a press interrupts — silencing d47, in production — run before
    /// <see cref="Pressed"/> and before anything is known about what follows.
    /// <para>
    /// <b>On the press edge rather than on the tap being recognised, and that is the whole
    /// design.</b> There is already a notion of a press too short to be speech —
    /// <c>UtteranceEnd.TooShort</c> — and it is the wrong hook, because it is only knowable at
    /// <em>release</em>: a Commander who presses and starts speaking immediately would hear d47
    /// talk over their first half-sentence, which is the exact complaint. Interrupting
    /// unconditionally is also simpler than detecting a tap, not harder.
    /// </para>
    /// <para>
    /// Fires on every press in both hold and toggle modes. The second press of a toggle silences
    /// nothing, because nothing is speaking by then.
    /// </para>
    /// </summary>
    public Action? Barge { get; init; }

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
            // Before the gate opens, not after. See Barge.
            Barge?.Invoke();
            Pressed?.Invoke();
        }
        else
        {
            Released?.Invoke();
        }
    }
}
