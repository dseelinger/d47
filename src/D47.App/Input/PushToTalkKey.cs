using System.Runtime.InteropServices;
using Avalonia.Input;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>The push-to-talk key, sampled from the tick loop (Phase 6).</summary>
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

    /// <summary>The gesture currently bound, for reporting.</summary>
    public string? Gesture { get; private set; }

    public bool IsDown => _wasDown;

    /// <summary>Binds a gesture.</summary>
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
            // A hand-edited settings file can contain anything.
            logger.LogWarning(ex, "Could not parse the push-to-talk key {Gesture}", gesture);
            return false;
        }

        if (VirtualKeys.Of(parsed.Key) is not { } key)
        {
            logger.LogWarning("{Key} has no virtual-key code D47 knows", Gestures.Describe(parsed.Key));
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

        logger.LogInformation("Push-to-talk is bound to {Gesture}", Gestures.Describe(gesture));
        return true;
    }

    /// <summary>Samples the key.</summary>
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

    /// <summary>Forces the key to read as released — for a settings change, or for shutdown.</summary>
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
