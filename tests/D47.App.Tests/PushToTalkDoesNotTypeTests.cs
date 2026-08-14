using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Holding push-to-talk must not type into d47's own panel.
/// <para>
/// Reported from a running build: a Commander bound push-to-talk to <kbd>[</kbd>, held it, and
/// got a line of <c>[[[[[[[[</c> in the Ask box instead of a transcript. The key is polled
/// rather than hooked (architecture.md D4), so d47 never consumes it and Windows keeps
/// delivering it to whatever has focus — and the Ask box had focus, because the panel put it
/// there on startup and again after every turn.
/// </para>
/// <para>
/// Two fixes; this covers the second. The Ask box is no longer focused unless the Commander
/// asks for it, and the bound key is swallowed before it reaches any control here. The handlers
/// tunnel, because a bubbling one runs after the text box has already inserted the character.
/// </para>
/// <para>
/// The events are raised directly, in the order Windows produces them — key down, then the
/// character, then key up. The headless harness's <c>KeyPress</c> raises no text input at all,
/// so a test written on it reports success whether or not anything is suppressed; the first
/// version of this file did exactly that and passed while proving nothing.
/// </para>
/// </summary>
public class PushToTalkDoesNotTypeTests
{
    private const string BoundKey = "Oem4";   // "[", as stored in settings

    private static (MainWindow Window, TextBox Ask) Panel(string? gesture)
    {
        var window = new MainWindow(host: null) { PushToTalkGesture = () => gesture };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var ask = Assert.IsType<TextBox>(window.Panel.FindControl<TextBox>("AskBox"));

        ask.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.True(ask.IsFocused, "the Ask box did not take focus, so this would prove nothing");

        return (window, ask);
    }

    /// <summary>One keystroke as the platform delivers it, raised at the focused control.</summary>
    private static void Type(TextBox ask, Key key, string character)
    {
        ask.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        ask.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = character });
        ask.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = key });

        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheBoundKeyDoesNotReachTheAskBoxEvenWhenItHasFocus()
    {
        var (_, ask) = Panel(BoundKey);

        // Held, as push-to-talk is held: auto-repeat is what produced the line of brackets.
        for (var repeat = 0; repeat < 8; repeat++)
        {
            Type(ask, Key.Oem4, "[");
        }

        Assert.True(
            string.IsNullOrEmpty(ask.Text),
            $"push-to-talk typed into the Ask box: '{ask.Text}'");
    }

    /// <summary>
    /// The control case, and what proves this test can see the fault it guards: with nothing
    /// bound, the very same keystroke types normally.
    /// </summary>
    [AvaloniaFact]
    public void WithNothingBoundTheSameKeyTypesNormally()
    {
        var (_, ask) = Panel(gesture: null);

        Type(ask, Key.Oem4, "[");

        Assert.Equal("[", ask.Text);
    }

    /// <summary>Every other key still types while push-to-talk is bound.</summary>
    [AvaloniaFact]
    public void OtherKeysAreUntouched()
    {
        var (_, ask) = Panel(BoundKey);

        Type(ask, Key.A, "a");
        Type(ask, Key.B, "b");

        Assert.Equal("ab", ask.Text);
    }

    /// <summary>
    /// Releasing clears the suppression, or one press would mute the Ask box for the rest of
    /// the session.
    /// </summary>
    [AvaloniaFact]
    public void TypingWorksAgainAfterTheKeyIsReleased()
    {
        var (_, ask) = Panel(BoundKey);

        Type(ask, Key.Oem4, "[");
        Type(ask, Key.A, "a");

        Assert.Equal("a", ask.Text);
    }
}
