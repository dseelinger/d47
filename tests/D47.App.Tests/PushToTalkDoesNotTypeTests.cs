using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Xunit;

namespace D47.App.Tests;

/// <summary>Holding push-to-talk must not type into d47's own panel.</summary>
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
    /// The control case, and what proves this test can see the fault it guards: with nothing bound, the
    /// very same keystroke types normally.
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
    /// Releasing clears the suppression, or one press would mute the Ask box for the rest of the
    /// session.
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
