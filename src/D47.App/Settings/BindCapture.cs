using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using D47.App.Controls;

namespace D47.App.Settings;

/// <summary>
/// Listens for one key gesture or stick button and returns it, without storing it. The settings view and
/// the setup wizard both bind through this.
/// </summary>
public static class BindCapture
{
    /// <summary>
    /// The setting key that caught and the value to store, or null when Esc or <paramref name="cancel"/>
    /// ended it. <paramref name="keyKey"/> arms the keyboard and <paramref name="buttonKey"/> the stick;
    /// <paramref name="bare"/> makes a modifier released on its own a binding.
    /// </summary>
    public static async Task<(string Key, string? Value)?> RunAsync(
        TopLevel top,
        string? keyKey,
        bool bare,
        string? buttonKey,
        SwitchEditing? switches,
        StatusLine message,
        CancellationToken cancel = default)
    {
        var captured = new TaskCompletionSource<(string Key, string? Value)?>();
        using var cancelled = cancel.Register(() => captured.TrySetResult(null));

        var keys = keyKey is not null;

        // A modifier held on the way down, on a row where one is a binding in its own right.
        Key? held = null;

        void OnKey(object? sender, KeyEventArgs e)
        {
            var modifier = e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;

            if (modifier)
            {
                // Told apart by which edge it arrives on: pressed, it is still someone assembling a chord;
                // released with nothing else pressed, it was the binding.
                held = bare ? e.Key : null;
                return;
            }

            held = null;
            e.Handled = true;

            captured.TrySetResult(e.Key == Key.Escape
                ? null
                : (keyKey!, new KeyGesture(e.Key, e.KeyModifiers).ToString()));
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

        if (keys)
        {
            // Tunnelling: the gesture belongs to the binding, not to whatever control the click left focused.
            top.AddHandler(InputElement.KeyDownEvent, OnKey, RoutingStrategies.Tunnel, handledEventsToo: true);

            if (bare)
            {
                top.AddHandler(InputElement.KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
            }
        }

        var walking = buttonKey is not null && switches is not null
            ? Walk(switches, buttonKey, message, captured)
            : null;

        try
        {
            return await captured.Task;
        }
        finally
        {
            if (keys)
            {
                top.RemoveHandler(InputElement.KeyDownEvent, OnKey);
                top.RemoveHandler(InputElement.KeyUpEvent, OnKeyUp);
            }

            walking?.Stop();
        }
    }

    /// <summary>The controller half of a capture: a 10 Hz walk on a timer this capture owns.</summary>
    private static DispatcherTimer Walk(
        SwitchEditing editing,
        string key,
        StatusLine message,
        TaskCompletionSource<(string Key, string? Value)?> captured)
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

            message.Say(result.Says);

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
