using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;

namespace D47.App.Settings;

/// <summary>
/// Listens for one key gesture or stick button and returns it, without storing it. The settings view and
/// the setup wizard both bind through this, one slot at a time.
/// </summary>
public static class BindCapture
{
    private static int _stickCaptures;

    /// <summary>Whether a capture is listening to the stick, so a press there binds rather than acts; UI thread.</summary>
    public static bool IsListeningToTheStick => _stickCaptures > 0;

    /// <summary>
    /// <see cref="RunAsync"/> again after each press <paramref name="refuse"/> gives a reason for, saying the
    /// reason on <paramref name="message"/>; returns the first press nothing refused, a null value for Esc, or null for a cancel.
    /// </summary>
    public static async Task<(string Key, string? Value)?> RunUntilFreeAsync(
        TopLevel top,
        string? keyKey,
        bool bare,
        string? buttonKey,
        SwitchEditing? switches,
        StatusLine message,
        Func<string, string, string?> refuse,
        CancellationToken cancel = default)
    {
        string? notice = null;

        while (true)
        {
            var caught = await RunAsync(top, keyKey, bare, buttonKey, switches, message, cancel, notice);

            if (caught is not { Value: { Length: > 0 } value } pressed
                || refuse(pressed.Key, value) is not { } reason)
            {
                return caught;
            }

            notice = reason;
            message.Fail(reason);
        }
    }

    /// <summary>
    /// The setting key that caught and the value to store. Esc returns the armed slot's key with a null
    /// value, which unbinds it; a press anywhere else on the page or <paramref name="cancel"/> returns null.
    /// <paramref name="keyKey"/> arms the keyboard and <paramref name="buttonKey"/> the stick;
    /// <paramref name="bare"/> makes a modifier released on its own a binding.
    /// </summary>
    public static async Task<(string Key, string? Value)?> RunAsync(
        TopLevel top,
        string? keyKey,
        bool bare,
        string? buttonKey,
        SwitchEditing? switches,
        StatusLine message,
        CancellationToken cancel = default,
        string? notice = null)
    {
        var captured = new TaskCompletionSource<(string Key, string? Value)?>();
        using var cancelled = cancel.Register(() => captured.TrySetResult(null));

        var keys = keyKey is not null;

        // A modifier held on the way down, on a row where one is a binding in its own right.
        Key? held = null;

        void OnKey(object? sender, KeyEventArgs e)
        {
            var decision = Decide(keys, e.Key, e.KeyModifiers);

            if (IsModifier(e.Key))
            {
                // Told apart by which edge it arrives on: pressed, it is still someone assembling a chord;
                // released with nothing else pressed, it was the binding.
                held = keys && bare ? e.Key : null;
                return;
            }

            held = null;

            if (decision == KeyDecision.Ignore)
            {
                return;
            }

            e.Handled = true;

            captured.TrySetResult(decision == KeyDecision.Escape
                ? (keyKey ?? buttonKey) is { } slotKey ? (slotKey, null) : null
                : (keyKey!, new KeyGesture(e.Key, e.KeyModifiers).ToString()));
        }

        // A press on the listening slot is swallowed so the slot does not start a second capture.
        void OnPointer(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source is Visual source
                && source.GetSelfAndVisualAncestors().OfType<Control>().Any(IsListeningSlot))
            {
                e.Handled = true;
            }

            captured.TrySetResult(null);
        }

        void OnKeyUp(object? sender, KeyEventArgs e)
        {
            if (held != e.Key)
            {
                return;
            }

            e.Handled = true;
            captured.TrySetResult((keyKey!, new KeyGesture(e.Key, KeyModifiers.None).ToString()));
        }

        // Tunnelling: the gesture belongs to the binding, not to whatever control the click left focused.
        // Attached even when only the stick is armed, so Esc always ends the capture.
        top.AddHandler(InputElement.KeyDownEvent, OnKey, RoutingStrategies.Tunnel, handledEventsToo: true);
        top.AddHandler(InputElement.PointerPressedEvent, OnPointer, RoutingStrategies.Tunnel, handledEventsToo: true);

        if (keys && bare)
        {
            top.AddHandler(InputElement.KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        var walking = buttonKey is not null && switches is not null
            ? Walk(switches, buttonKey, message, captured, notice)
            : null;

        if (walking is not null)
        {
            _stickCaptures++;
        }

        try
        {
            return await captured.Task;
        }
        finally
        {
            top.RemoveHandler(InputElement.KeyDownEvent, OnKey);
            top.RemoveHandler(InputElement.PointerPressedEvent, OnPointer);
            top.RemoveHandler(InputElement.KeyUpEvent, OnKeyUp);

            if (walking is not null)
            {
                walking.Stop();
                _stickCaptures--;
            }
        }
    }

    /// <summary>The class a slot wears while it listens.</summary>
    public const string ListeningClass = "listening";

    private static bool IsListeningSlot(Control control) =>
        control.Classes.Contains(SettingsView.BindingChipClass) && control.Classes.Contains(ListeningClass);

    internal enum KeyDecision { Escape, Gesture, Ignore }

    /// <summary>What a key-down does to a capture: Esc always ends it; any other non-modifier key is a gesture only when the keyboard is armed.</summary>
    internal static KeyDecision Decide(bool keyboardArmed, Key key, KeyModifiers modifiers)
    {
        if (key == Key.Escape)
        {
            return KeyDecision.Escape;
        }

        return keyboardArmed && !IsModifier(key) ? KeyDecision.Gesture : KeyDecision.Ignore;
    }

    private static bool IsModifier(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;

    /// <summary>The controller half of a capture: a 10 Hz walk on a timer this capture owns.</summary>
    private static DispatcherTimer Walk(
        SwitchEditing editing,
        string key,
        StatusLine message,
        TaskCompletionSource<(string Key, string? Value)?> captured,
        string? notice)
    {
        var capture = new D47.Core.Hotas.ButtonCapture();
        var opened = editing.Now();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };

        timer.Tick += (_, _) =>
        {
            if (editing.Reader.Unavailable is { Length: > 0 } why)
            {
                message.Fail(why);
                timer.Stop();
                return;
            }

            // Nothing is read until the device list stops changing: a single enumeration at startup reported
            // three of six devices on the bench (Phase 21, finding 1).
            if (!editing.Reader.IsSettled)
            {
                message.Say("Looking for your controllers…");
                return;
            }

            var result = capture.Poll(editing.Reader.Poll(), editing.Now() - opened);

            if (notice is not null && result.Stage is not (D47.Core.Hotas.ButtonCaptureStage.Captured or D47.Core.Hotas.ButtonCaptureStage.Declined))
            {
                message.Fail(notice);
            }
            else
            {
                message.Say(result.Says);
            }

            if (result.Stage == D47.Core.Hotas.ButtonCaptureStage.Captured)
            {
                timer.Stop();
                captured.TrySetResult((key, result.Binding!.Value.ToString()));
            }
            else if (result.Stage == D47.Core.Hotas.ButtonCaptureStage.Declined)
            {
                // The decline is the answer, and it stays on the line.
                timer.Stop();
            }
        };

        timer.Start();

        return timer;
    }
}
