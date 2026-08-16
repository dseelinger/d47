using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using D47.App.Controls;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Hearing a voice is not choosing it (list.md Phase 19, "Hear a voice before you choose it").
/// <para>
/// The picker lists several hundred names and the Commander is casting a character from them,
/// which is a judgement no label supports. The button that lets them listen has to leave the
/// dialog exactly as it was, has to cost nothing until it is pressed, and has to say what
/// pressing it will do — because on a paid provider it spends money.
/// </para>
/// </summary>
public class AuditionDoesNotCommitTests
{
    private static PickerRequest Voices(
        Func<string, CancellationToken, Task>? play = null,
        string label = "Hear it (free)",
        string? unavailable = null) => new()
    {
        Prompt = "Voice",
        Choices = ["en-GB-RyanNeural", "en-GB-SoniaNeural"],
        Current = "en-GB-RyanNeural",
        AllowsFreeText = true,
        Audition = new PickerAudition
        {
            Play = play ?? ((_, _) => Task.CompletedTask),
            Label = label,
            Unavailable = unavailable,
        },
    };

    [AvaloniaFact]
    public void ARowThatOffersNoAuditionHasNoButton()
    {
        // Absent rather than present and inert. A device picker with a shut "Hear it" on it is a
        // control asserting a capability that does not exist.
        var picker = PickerWindow.For(new PickerRequest { Prompt = "Microphone", Choices = ["a"] });

        Assert.False(picker.GetControl<Button>("AuditionButton").IsVisible);
    }

    [AvaloniaFact]
    public void ThePriceIsOnTheButtonBeforeItIsPressed()
    {
        var picker = PickerWindow.For(Voices(label: "Hear it (about $0.013)"));
        var button = picker.GetControl<Button>("AuditionButton");

        Assert.True(button.IsVisible);
        Assert.True(button.IsEnabled);
        Assert.Equal("Hear it (about $0.013)", button.Content);
    }

    /// <summary>
    /// Shut and explained rather than silently inert: "no voice provider is selected" is a fact
    /// the Commander can act on and an unresponsive button is not.
    /// </summary>
    [AvaloniaFact]
    public void WhenItCannotBePressedItSaysWhy()
    {
        var picker = PickerWindow.For(
            Voices(unavailable: "ElevenLabs needs an API key before it will speak."));

        var button = picker.GetControl<Button>("AuditionButton");

        Assert.True(button.IsVisible);
        Assert.False(button.IsEnabled);
        Assert.Equal("ElevenLabs needs an API key before it will speak.", ToolTip.GetTip(button));
    }

    [AvaloniaFact]
    public async Task PressingItPlaysTheHighlightedVoiceAndLeavesTheDialogOpen()
    {
        var played = new List<string>();

        var picker = PickerWindow.For(Voices((voice, _) =>
        {
            played.Add(voice);
            return Task.CompletedTask;
        }));

        picker.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        picker.GetControl<Button>("AuditionButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Yield();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(["en-GB-RyanNeural"], played);

        // Still open, and nothing chosen. The whole point is that a Commander can walk the list
        // and listen without committing to anything.
        Assert.True(picker.IsVisible);

        picker.Close();
    }

    /// <summary>
    /// Starting a second audition drops the first mid-word rather than queueing behind it. Two
    /// voices talking over each other tells you nothing about either.
    /// </summary>
    [AvaloniaFact]
    public async Task ASecondPressCancelsTheFirst()
    {
        var cancelled = new TaskCompletionSource();
        var started = 0;

        var picker = PickerWindow.For(Voices(async (_, token) =>
        {
            Interlocked.Increment(ref started);

            using var registration = token.Register(() => cancelled.TrySetResult());

            // Stands in for a synthesis still in flight when the next press arrives.
            await Task.Delay(Timeout.Infinite, token);
        }));

        picker.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var button = picker.GetControl<Button>("AuditionButton");

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Yield();

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        // The first one being cancelled is the requirement. It happens inside the second press's
        // handler, before that press has reached Play — so the count is read after it, not with
        // it.
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        for (var attempt = 0; attempt < 100 && Volatile.Read(ref started) < 2; attempt++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.Equal(2, Volatile.Read(ref started));

        picker.Close();
    }

    /// <summary>
    /// A provider that would not speak says so on the button that was pressed. The picker has
    /// nowhere else to put a message, and an audition that silently does nothing is
    /// indistinguishable from a voice that is very quiet.
    /// </summary>
    [AvaloniaFact]
    public async Task AFailedAuditionIsReportedRatherThanSwallowed()
    {
        var picker = PickerWindow.For(
            Voices((_, _) => throw new InvalidOperationException("ElevenLabs refused that voice.")));

        picker.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var button = picker.GetControl<Button>("AuditionButton");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        await Task.Yield();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("ElevenLabs refused that voice.", ToolTip.GetTip(button));

        picker.Close();
    }
}
