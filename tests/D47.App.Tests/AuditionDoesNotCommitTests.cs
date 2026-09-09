using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Hearing a voice is not choosing it: a play glyph on the row, not a button with a price written under the list.</summary>
public class AuditionDoesNotCommitTests
{
    private const string Free = "Play a voice to hear it. This provider costs nothing.";

    private static PickerRequest Voices(
        Func<string, CancellationToken, Task>? play = null,
        string cost = Free,
        string? unavailable = null) => new()
    {
        Prompt = "Voice",
        Choices = ["en-GB-RyanNeural", "en-GB-SoniaNeural"],
        Current = "en-GB-RyanNeural",
        AllowsFreeText = true,
        Audition = new PickerAudition
        {
            Play = play ?? ((_, _) => Task.CompletedTask),
            Cost = cost,
            Unavailable = unavailable,
        },
    };

    /// <summary>The rows as the list holds them, which is what the glyphs are bound to.</summary>
    private static IReadOnlyList<PickerChoice> Rows(PickerWindow picker) =>
        [.. (IEnumerable<PickerChoice>)picker.GetControl<ListBox>("Choices").ItemsSource!];

    /// <summary>The play control on one row, found by the value it plays rather than by position.</summary>
    private static Button Glyph(PickerWindow picker, string value) =>
        picker.GetVisualDescendants().OfType<Button>()
            .First(button => (button.DataContext as PickerChoice)?.Value == value);

    private static PickerWindow Shown(PickerRequest request)
    {
        var picker = PickerWindow.For(request);

        picker.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return picker;
    }

    [AvaloniaFact]
    public void ARowThatOffersNoAuditionHasNoGlyphAtAll()
    {
        // Absent rather than present and inert.
        var picker = PickerWindow.For(new PickerRequest { Prompt = "Microphone", Choices = ["a"] });

        Assert.False(picker.GetControl<TextBlock>("AuditionNote").IsVisible);
        Assert.All(Rows(picker), row => Assert.False(row.CanPlay));
    }

    [AvaloniaFact]
    public void ThePriceIsOnScreenBeforeAnythingIsPressed()
    {
        var picker = PickerWindow.For(
            Voices(cost: "Play a voice to hear it. Each one costs about $0.013."));

        var note = picker.GetControl<TextBlock>("AuditionNote");

        Assert.True(note.IsVisible);
        Assert.Equal("Play a voice to hear it. Each one costs about $0.013.", note.Text);

        // And on the pointer as well, where a Commander reaching for the control is looking.
        Assert.All(
            Rows(picker),
            row =>
            {
                Assert.True(row.Playable);
                Assert.Equal("Play a voice to hear it. Each one costs about $0.013.", row.Why);
            });
    }

    /// <summary>
    /// Shut and explained rather than silently inert: "no voice provider is selected" is a fact the
    /// Commander can act on and an unresponsive glyph is not.
    /// </summary>
    [AvaloniaFact]
    public void WhenNothingCanBePlayedItSaysWhy()
    {
        var picker = PickerWindow.For(
            Voices(unavailable: "ElevenLabs needs an API key before it will speak."));

        Assert.Equal(
            "ElevenLabs needs an API key before it will speak.",
            picker.GetControl<TextBlock>("AuditionNote").Text);

        Assert.All(
            Rows(picker),
            row =>
            {
                Assert.True(row.CanPlay);
                Assert.False(row.Playable);
                Assert.Equal("ElevenLabs needs an API key before it will speak.", row.Why);
            });
    }

    /// <summary>The glyph plays its own row, not the selection.</summary>
    [AvaloniaFact]
    public async Task PressingPlayPlaysThatRowAndLeavesTheDialogOpen()
    {
        var played = new List<string>();

        var picker = Shown(Voices((voice, _) =>
        {
            played.Add(voice);
            return Task.CompletedTask;
        }));

        // The row that is not the current value, so a handler reading the selection instead of the row would
        // play the wrong voice and this would say so.
        Glyph(picker, "en-GB-SoniaNeural").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Yield();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(["en-GB-SoniaNeural"], played);

        // Still open, still on the voice they arrived with.
        Assert.True(picker.IsVisible);
        Assert.Equal(0, picker.GetControl<ListBox>("Choices").SelectedIndex);

        picker.Close();
    }

    /// <summary>Starting a second audition drops the first mid-word rather than queueing behind it.</summary>
    [AvaloniaFact]
    public async Task PlayingASecondVoiceCancelsTheFirst()
    {
        var entered = new TaskCompletionSource();
        var started = 0;
        CancellationToken first = default;

        var picker = Shown(Voices(async (_, token) =>
        {
            Interlocked.Increment(ref started);

            if (Volatile.Read(ref started) == 1)
            {
                first = token;
            }

            entered.TrySetResult();

            // Stands in for a synthesis still in flight when the next press arrives.
            await Task.Delay(Timeout.Infinite, token);
        }));

        Glyph(picker, "en-GB-RyanNeural").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Yield();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // The first audition has to be running before the second press, or there is nothing for it to cancel
        // and this test asserts something it never arranged.
        Assert.True(
            Rows(picker).First(item => item.Value == "en-GB-RyanNeural").Playing,
            "the first audition never started, so the second press had nothing to cancel");

        // And the *delegate* has to have entered, which is a later moment than the row saying Playing.
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Glyph(picker, "en-GB-SoniaNeural").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        // Observed on the token rather than through a callback, which is the third and last race this
        // test has had. `StopAsync` cancels with `CancelAsync`, and that schedules the registered callbacks
        // on the threadpool deliberately — it is what keeps arbitrary continuations off the UI
        // thread.
        Pump(() => first.IsCancellationRequested, "the first audition was never cancelled");
        Pump(() => Volatile.Read(ref started) >= 2, "the second audition never started");

        Assert.Equal(2, Volatile.Read(ref started));

        picker.Close();
    }

    /// <summary>The glyph says what pressing it will do next, and pressing it then does that.</summary>
    [AvaloniaFact]
    public async Task TheGlyphBecomesStopWhileItIsTalkingAndStopsWhenPressed()
    {
        var started = 0;
        CancellationToken playing = default;

        var picker = Shown(Voices(async (_, token) =>
        {
            Interlocked.Increment(ref started);
            playing = token;

            await Task.Delay(Timeout.Infinite, token);
        }));

        var glyph = Glyph(picker, "en-GB-RyanNeural");
        var row = Rows(picker).First(item => item.Value == "en-GB-RyanNeural");

        glyph.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Yield();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(row.Playing);

        // And the glyph says so in words as well as in shape, because dropping the label is what buys room
        // for a control on every row.
        Assert.Equal("Stop en-GB-RyanNeural", Avalonia.Automation.AutomationProperties.GetName(glyph));

        glyph.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        // On the token, for the reason the test above says at length: `CancelAsync` puts its callbacks on the
        // threadpool on purpose, so waiting on one asks the busiest resource on the machine about something
        // the token already knows.
        Pump(() => playing.IsCancellationRequested, "the audition was never stopped");

        // Stopped, and not started again — the second press was a stop rather than a restart.
        Assert.False(row.Playing);
        Assert.Equal(1, Volatile.Read(ref started));

        picker.Close();
    }

    /// <summary>A provider that would not speak says so where it was asked.</summary>
    [AvaloniaFact]
    public async Task AFailedAuditionIsReportedRatherThanSwallowed()
    {
        var picker = Shown(
            Voices((_, _) => throw new InvalidOperationException("ElevenLabs refused that voice.")));

        var glyph = Glyph(picker, "en-GB-RyanNeural");
        glyph.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        await Task.Yield();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("ElevenLabs refused that voice.", ToolTip.GetTip(glyph));
        Assert.Equal(
            "ElevenLabs refused that voice.",
            picker.GetControl<TextBlock>("AuditionNote").Text);

        picker.Close();
    }

    /// <summary>Clicking a voice highlights it and nothing else.</summary>
    [AvaloniaFact]
    public void ClickingAVoiceHighlightsItWithoutTakingIt()
    {
        var picker = Shown(Voices());
        var list = picker.GetControl<ListBox>("Choices");

        var second = list.GetRealizedContainers().ElementAt(1);
        var middle = second.TranslatePoint(new Point(4, second.Bounds.Height / 2), picker)!.Value;

        picker.MouseDown(middle, MouseButton.Left);
        picker.MouseUp(middle, MouseButton.Left);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, list.SelectedIndex);
        Assert.True(picker.IsVisible);

        picker.Close();
    }

    /// <summary>The list with its glyphs on it, for a human to look at.</summary>
    [AvaloniaFact]
    public async Task TheListAndItsGlyphsAreDrawnForLookingAt()
    {
        var (settings, _, _) = TestSurface.Create();

        // The glyphs take their colour from a theme resource, so a capture with no theme manager is a capture
        // of two unpainted paths.
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var talking = new TaskCompletionSource();

        var picker = Shown(Voices(
            async (_, token) =>
            {
                talking.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
            },
            cost: "Play a voice to hear it. Each one costs about $0.013."));

        Capture(picker, "voice-picker-play");

        Glyph(picker, "en-GB-SoniaNeural").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await talking.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Capture(picker, "voice-picker-stop");

        picker.Close();
    }

    private static void Capture(Window window, string name)
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, $"{name}.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    /// <summary>
    /// The value the Commander arrived with stays highlighted, through opening the picker and through
    /// typing in the filter box — which is what makes Enter with no typing keep what they had, and what
    /// scrolls the list to where they already are.
    /// </summary>
    [AvaloniaFact]
    public void TheCurrentVoiceStaysHighlightedThroughOpeningAndFiltering()
    {
        var picker = Shown(Voices());
        var list = picker.GetControl<ListBox>("Choices");

        Assert.Equal(0, list.SelectedIndex);

        // A filter both voices survive, so the highlight has somewhere to stay.
        picker.GetControl<TextBox>("FilterBox").Text = "en-GB";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, list.ItemCount);
        Assert.Equal(0, list.SelectedIndex);
        Assert.Equal("en-GB-RyanNeural", ((PickerChoice)list.SelectedItem!).Value);

        picker.Close();
    }

    /// <summary>Use this takes what the click highlighted.</summary>
    [AvaloniaFact]
    public void UseThisTakesWhatWasHighlighted()
    {
        var picker = Shown(Voices());
        var list = picker.GetControl<ListBox>("Choices");

        list.SelectedIndex = 1;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        picker.GetControl<Button>("AcceptButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(picker.IsVisible);
    }

    /// <summary>Turns the dispatcher until something is true.</summary>
    private static void Pump(Func<bool> until, string complaint)
    {
        // Wall-clock, not a count of turns. A turn is microseconds on an idle machine and tens of
        // milliseconds on a saturated one, so a budget in turns is a budget that quietly becomes minutes
        // exactly when something has gone wrong — measured at eight and a half of them with the pool pinned.
        var watch = System.Diagnostics.Stopwatch.StartNew();

        while (watch.Elapsed < TimeSpan.FromSeconds(10))
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            if (until())
            {
                return;
            }

            Thread.Sleep(1);
        }

        Assert.Fail(complaint);
    }
}
