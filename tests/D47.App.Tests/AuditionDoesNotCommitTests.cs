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

/// <summary>
/// Hearing a voice is not choosing it (Phase 19, "Hear a voice before you choose it"),
/// and from change-requests.md 18 and 19 that is a play glyph on the row rather than a button
/// with a price written on it under the list.
/// <para>
/// The picker lists several hundred names and the Commander is casting a character from them,
/// which is a judgement no label supports. So the control that plays a voice sits beside the
/// voice it plays, it leaves the dialog exactly as it was, it costs nothing until it is pressed,
/// and what a press costs is on screen before anything is pressed — because on a paid provider
/// it spends money.
/// </para>
/// </summary>
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
        // Absent rather than present and inert. A device picker with a shut play control on every
        // line is a control asserting a capability that does not exist.
        var picker = PickerWindow.For(new PickerRequest { Prompt = "Microphone", Choices = ["a"] });

        Assert.False(picker.GetControl<TextBlock>("AuditionNote").IsVisible);
        Assert.All(Rows(picker), row => Assert.False(row.CanPlay));
    }

    /// <summary>
    /// The price is in front of the Commander before anything is pressed. It moved off the button
    /// when the button became a glyph, and it did not move onto a tooltip alone — a cost you have
    /// to hover to discover is a cost discovered afterwards on the bill.
    /// </summary>
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
    /// Shut and explained rather than silently inert: "no voice provider is selected" is a fact
    /// the Commander can act on and an unresponsive glyph is not.
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

    /// <summary>
    /// The glyph plays its own row, not the selection. That is the whole reason it moved onto the
    /// row: a Commander can listen to one voice while another stays highlighted, and neither
    /// listening nor the highlight commits to anything.
    /// </summary>
    [AvaloniaFact]
    public async Task PressingPlayPlaysThatRowAndLeavesTheDialogOpen()
    {
        var played = new List<string>();

        var picker = Shown(Voices((voice, _) =>
        {
            played.Add(voice);
            return Task.CompletedTask;
        }));


        // The row that is not the current value, so a handler reading the selection instead of
        // the row would play the wrong voice and this would say so.
        Glyph(picker, "en-GB-SoniaNeural").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Yield();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(["en-GB-SoniaNeural"], played);

        // Still open, still on the voice they arrived with. The whole point is that a Commander
        // can walk the list and listen without committing to anything.
        Assert.True(picker.IsVisible);
        Assert.Equal(0, picker.GetControl<ListBox>("Choices").SelectedIndex);

        picker.Close();
    }

    /// <summary>
    /// Starting a second audition drops the first mid-word rather than queueing behind it. Two
    /// voices talking over each other tells you nothing about either.
    /// </summary>
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

        // The first audition has to be running before the second press, or there is nothing for it
        // to cancel and this test asserts something it never arranged. A bare yield is one turn of
        // the dispatcher, which was enough on a developer's machine and not on a loaded CI runner:
        // the press was still queued, the second press cancelled nothing, and the wait below timed
        // out after its full five seconds. Pumped and then asserted, the same way
        // TheGlyphBecomesStopWhileItIsTalkingAndStopsWhenPressed does it just below.
        Assert.True(
            Rows(picker).First(item => item.Value == "en-GB-RyanNeural").Playing,
            "the first audition never started, so the second press had nothing to cancel");

        // And the *delegate* has to have entered, which is a later moment than the row saying
        // Playing. That gap is the remaining race, and it reappeared on a runner made busier by a
        // seventh test project: cancel the token before the delegate runs and it never reaches
        // token.Register, so nothing ever completes `cancelled` and the wait below burns its full
        // five seconds. Waiting on entry rather than on a flag the UI sets closes it — the thing
        // being cancelled must exist before there is a cancellation to observe.
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Glyph(picker, "en-GB-SoniaNeural").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        // <b>Observed on the token rather than through a callback</b>, which is the third and last
        // race this test has had. `StopAsync` cancels with `CancelAsync`, and that schedules the
        // registered callbacks on the threadpool <em>deliberately</em> — it is what keeps arbitrary
        // continuations off the UI thread. So a callback is exactly the wrong thing to wait on
        // here: with the pool saturated, which is what a CI runner running seven test projects is,
        // the callback is delayed for as long as the pool takes to find a thread. Measured with the
        // pool starved on purpose, the five-second wait below used to come back empty after 23
        // seconds — the timeout was not the product being slow, it was the test asking the busiest
        // resource on the machine to tell it something the token already knew.
        //
        // The token's own flag flips synchronously with the cancellation, and the handler that
        // cancels runs on the dispatcher — so pumping the dispatcher is both necessary and
        // sufficient, and no threadpool thread is on the path at all.
        Pump(() => first.IsCancellationRequested, "the first audition was never cancelled");
        Pump(() => Volatile.Read(ref started) >= 2, "the second audition never started");

        Assert.Equal(2, Volatile.Read(ref started));

        picker.Close();
    }

    /// <summary>
    /// The glyph says what pressing it will do next, and pressing it then does that. A stop
    /// control that restarted the line instead would be the one state a Commander could not get
    /// out of without waiting it through.
    /// </summary>
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

        // And the glyph says so in words as well as in shape, because dropping the label is what
        // buys room for a control on every row.
        Assert.Equal("Stop en-GB-RyanNeural", Avalonia.Automation.AutomationProperties.GetName(glyph));

        glyph.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        // On the token, for the reason the test above says at length: `CancelAsync` puts its
        // callbacks on the threadpool on purpose, so waiting on one asks the busiest resource on
        // the machine about something the token already knows.
        Pump(() => playing.IsCancellationRequested, "the audition was never stopped");

        // Stopped, and not started again — the second press was a stop rather than a restart.
        Assert.False(row.Playing);
        Assert.Equal(1, Volatile.Read(ref started));

        picker.Close();
    }

    /// <summary>
    /// A provider that would not speak says so where it was asked. An audition that silently does
    /// nothing is indistinguishable from a voice that is very quiet.
    /// </summary>
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

    /// <summary>
    /// Clicking a voice highlights it and nothing else (change-requests.md 19). The picker used
    /// to commit and close on the first click, which left no way to look at the list — and no row
    /// for a play glyph to live on, since a row that dismisses the window when touched cannot
    /// hold a control.
    /// </summary>
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

    /// <summary>
    /// The list with its glyphs on it, for a human to look at. They are drawn in repo as path
    /// data rather than taken from a font, and path data is the kind of thing that compiles, lays
    /// out, passes every assertion here and still looks wrong — so "does that read as a play
    /// control" gets an artifact to answer with, the same way the key row's glyphs do.
    /// <para>
    /// Both states, because the stop square is the one a Commander only sees once something is
    /// already talking.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public async Task TheListAndItsGlyphsAreDrawnForLookingAt()
    {
        var (settings, _, _) = TestSurface.Create();

        // The glyphs take their colour from a theme resource, so a capture with no theme manager
        // is a capture of two unpainted paths. Nothing about the drawing, everything about the
        // harness — but it looks identical to the drawing being wrong.
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
    /// The value the Commander arrived with stays highlighted, through opening the picker and
    /// through typing in the filter box — which is what makes Enter with no typing keep what
    /// they had, and what scrolls the list to where they already are.
    /// <para>
    /// A list holds its selection by object, so this is a claim about the rows surviving a
    /// keystroke rather than being made again. Built per filter instead, the highlight was gone
    /// before the window finished opening: a text box raises TextChanged as its template applies,
    /// which re-ran the filter, which handed the list a different row for the same voice.
    /// </para>
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

    /// <summary>
    /// And <b>Use this</b> takes what the click highlighted, which is the half of the old
    /// behaviour worth keeping.
    /// </summary>
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

    /// <summary>
    /// Turns the dispatcher until something is true. <b>No <c>Task.Delay</c> and no threadpool</b>:
    /// both are timer- and pool-bound, which is the thing this file's flake was made of. What is
    /// being waited for here always happens on the dispatcher, so turning it is the whole wait.
    /// </summary>
    private static void Pump(Func<bool> until, string complaint)
    {
        // <b>Wall-clock, not a count of turns.</b> A turn is microseconds on an idle machine and
        // tens of milliseconds on a saturated one, so a budget in turns is a budget that quietly
        // becomes minutes exactly when something has gone wrong — measured at eight and a half of
        // them with the pool pinned. Ten seconds is far past anything the dispatcher needs and
        // still fails while somebody is watching.
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
