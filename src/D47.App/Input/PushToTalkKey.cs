using System.Runtime.InteropServices;
using Avalonia.Input;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>
/// The push-to-talk key, sampled from the tick loop (list.md Phase 6).
/// <para>
/// <b>Why polling, and not the three alternatives.</b>
/// </para>
/// <para>
/// <c>RegisterHotKey</c> — which <see cref="GlobalHotkey"/> uses for the silence key — delivers
/// <c>WM_HOTKEY</c> on press only. There is no release edge, and push-to-talk is defined by its
/// release edge, so it is not a candidate at all.
/// </para>
/// <para>
/// A <c>WH_KEYBOARD_LL</c> hook has both edges and is <b>forbidden</b>: it is a global input
/// chokepoint, so a stall in d47 becomes a stall in the Commander's controls mid-fight
/// (architecture.md D4, rule 1). That rule is written about injecting keys and applies at least
/// as strongly to reading them.
/// </para>
/// <para>
/// Raw Input with <c>RIDEV_INPUTSINK</c> would work — both edges, event-driven, delivered to a
/// background window, and not a hook, so D4 rule 1 survives. It was rejected on privacy:
/// <c>RIDEV_INPUTSINK</c> delivers <em>every keystroke on the system</em> to d47's window,
/// including passwords typed into other applications. Receiving the Commander's entire keyboard
/// and discarding it is a worse posture than reading one key, for an app whose repository is
/// public and whose selling point is that nothing leaves the machine.
/// </para>
/// <para>
/// <c>GetAsyncKeyState</c> reads <b>exactly the one bound virtual-key code</b>, never the
/// keyboard. That distinction is the whole argument. Its cost is latency — the tick loop samples
/// at 10 Hz, so a key-down is seen up to 100 ms late — and that cost is absorbed by
/// <see cref="Core.Listening.ListenGate"/>'s pre-roll, which opens retroactively into audio
/// already captured. The checklist asks for push-to-talk to be one gate policy over a
/// continuous stream anyway, so the buffer that makes polling viable is required regardless of
/// which input path wins.
/// </para>
/// </summary>
public sealed class PushToTalkKey(ILogger<PushToTalkKey> logger)
{
    /// <summary>The high bit of GetAsyncKeyState: the key is down right now.</summary>
    private const int DownBit = 0x8000;

    private uint? _key;
    private uint[] _modifiers = [];
    private bool _wasDown;

    /// <summary>Raised on the tick that first sees the key down.</summary>
    public event Action? Pressed;

    /// <summary>Raised on the tick that first sees it back up.</summary>
    public event Action? Released;

    /// <summary>The gesture currently bound, for reporting. Null when unbound.</summary>
    public string? Gesture { get; private set; }

    public bool IsDown => _wasDown;

    /// <summary>
    /// Binds a gesture. Unlike the silence key this does not need a modifier: push-to-talk on a
    /// bare key is the normal arrangement, and a Commander flying with a HOTAS has a spare one.
    /// The tradeoff is stated rather than hidden — see <see cref="Poll"/>.
    /// </summary>
    public bool Bind(string? gesture)
    {
        _key = null;
        _modifiers = [];
        Gesture = null;

        if (string.IsNullOrWhiteSpace(gesture))
        {
            return false;
        }

        KeyGesture parsed;

        try
        {
            parsed = KeyGesture.Parse(gesture);
        }
        catch (Exception ex)
        {
            // A hand-edited settings file can contain anything. An unparseable gesture is an
            // unbound key, not a crash.
            logger.LogWarning(ex, "Could not parse the push-to-talk key {Gesture}", gesture);
            return false;
        }

        if (VirtualKeys.Of(parsed.Key) is not { } key)
        {
            logger.LogWarning("{Key} has no virtual-key code D47 knows", parsed.Key);
            return false;
        }

        var modifiers = new List<uint>();
        if (parsed.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers.Add(VirtualKeys.Control);
        if (parsed.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers.Add(VirtualKeys.Alt);
        if (parsed.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers.Add(VirtualKeys.Shift);
        if (parsed.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers.Add(VirtualKeys.Windows);

        _key = key;
        _modifiers = [.. modifiers];
        Gesture = gesture;

        logger.LogInformation("Push-to-talk is bound to {Gesture}", gesture);
        return true;
    }

    /// <summary>
    /// Samples the key. Called from the tick loop, and it reads one virtual-key code plus any
    /// modifiers the gesture declared — never the keyboard as a whole.
    /// <para>
    /// The edge is computed here rather than trusting <c>GetAsyncKeyState</c>'s low
    /// "pressed since last call" bit, which is shared process-wide and is unreliable when
    /// anything else in the process also calls it. Comparing against the previous sample is
    /// exact and costs one boolean.
    /// </para>
    /// </summary>
    public void Poll()
    {
        if (_key is not { } key)
        {
            return;
        }

        var down = IsHeld(key) && _modifiers.All(IsHeld);

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
    /// Forces the key to read as released — for a settings change, or for shutdown. Without
    /// this, rebinding while held leaves the gate open with nothing able to close it, which is
    /// the listening equivalent of a stranded key.
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

    private static bool IsHeld(uint virtualKey) => (GetAsyncKeyState((int)virtualKey) & DownBit) != 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
