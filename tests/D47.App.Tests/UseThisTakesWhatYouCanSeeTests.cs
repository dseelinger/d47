using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Windowing;
using Xunit;

namespace D47.App.Tests;

/// <summary>Use this takes the row the Commander can see, or it is shut.</summary>
public class UseThisTakesWhatYouCanSeeTests
{
    private static PickerRequest Voices() => new()
    {
        Prompt = "Voice",
        Choices = ["en-GB-RyanNeural", "en-GB-SoniaNeural", "en-US-AndrewNeural"],
        Current = "en-GB-RyanNeural",
        AllowsFreeText = true,
    };

    /// <summary>A closed vocabulary, opened on a row with nothing stored — the microphone case.</summary>
    private static PickerRequest Microphones() => new()
    {
        Prompt = "Microphone",
        Choices = ["Headset (Logitech)", "Line In (Realtek)"],
        Current = null,
        AllowsFreeText = false,
    };

    /// <summary>The picker on screen over an owner, with the answer still in flight.</summary>
    private static (PickerWindow Picker, Task<PickerResult?> Answer) Asking(PickerRequest request)
    {
        var owner = new Window { Width = 900, Height = 700 };
        owner.Show();

        var picker = PickerWindow.For(request);
        var answer = picker.Over<PickerResult?>(owner);

        Dispatcher.UIThread.RunJobs();

        return (picker, answer);
    }

    private static void Type(PickerWindow picker, string text)
    {
        picker.GetControl<TextBox>("FilterBox").Text = text;
        Dispatcher.UIThread.RunJobs();
    }

    private static void UseThis(PickerWindow picker)
    {
        picker.GetControl<Button>("AcceptButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>What the picker answered, read rather than awaited.</summary>
    private static PickerResult? Answered(Task<PickerResult?> answer)
    {
        Dispatcher.UIThread.RunJobs();

        Assert.True(answer.IsCompleted, "the picker did not answer");

        return answer.Result;
    }

    /// <summary>
    /// Typing past the highlighted row moves the highlight to the top match, so the obvious row is the
    /// one taken.
    /// </summary>
    [AvaloniaFact]
    public void TypingPastTheHighlightMovesItToTheTopMatch()
    {
        var (picker, answer) = Asking(Voices());
        var list = picker.GetControl<ListBox>("Choices");

        // Opened on the current value, which is what makes Enter with no typing keep it.
        Assert.Equal(0, list.SelectedIndex);

        // And now a filter that excludes it.
        Type(picker, "sonia");

        Assert.Equal(1, list.ItemCount);
        Assert.Equal(0, list.SelectedIndex);
        Assert.Equal("en-GB-SoniaNeural", ((PickerChoice)list.SelectedItem!).Value);

        UseThis(picker);

        // The row, and not the word "sonia" written into the setting as a voice id.
        Assert.Equal("en-GB-SoniaNeural", Answered(answer)!.Value);
    }

    /// <summary>
    /// A row still selected after a keystroke is the Commander's own and is not moved — the fixup puts
    /// a highlight back, it does not take one over.
    /// </summary>
    [AvaloniaFact]
    public void ASurvivingHighlightIsLeftWhereItIs()
    {
        var (picker, _) = Asking(Voices());
        var list = picker.GetControl<ListBox>("Choices");

        list.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        // Both en-GB voices survive this, and Sonia is the second of them.
        Type(picker, "en-GB");

        Assert.Equal(2, list.ItemCount);
        Assert.Equal("en-GB-SoniaNeural", ((PickerChoice)list.SelectedItem!).Value);

        picker.Close();
    }

    /// <summary>The button is shut rather than lit and inert.</summary>
    [AvaloniaFact]
    public void WithNothingChosenThereIsNothingToPress()
    {
        var (picker, _) = Asking(Microphones());

        var button = picker.GetControl<Button>("AcceptButton");

        Assert.Equal(-1, picker.GetControl<ListBox>("Choices").SelectedIndex);
        Assert.False(button.IsEnabled);

        // And it lights the moment there is something to take.
        picker.GetControl<ListBox>("Choices").SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        Assert.True(button.IsEnabled);

        picker.Close();
    }

    /// <summary>
    /// Typing into a closed vocabulary still ends somewhere pressable, which is the reported case:
    /// type, see one row, take it.
    /// </summary>
    [AvaloniaFact]
    public void TypingIntoAClosedVocabularyStillEndsSomewherePressable()
    {
        var (picker, answer) = Asking(Microphones());

        Type(picker, "line");

        Assert.True(picker.GetControl<Button>("AcceptButton").IsEnabled);

        UseThis(picker);

        Assert.Equal("Line In (Realtek)", Answered(answer)!.Value);
    }

    /// <summary>
    /// And free text still reaches the setting where it is meant to — a value the catalogue does not
    /// list, typed in full, matching nothing.
    /// </summary>
    [AvaloniaFact]
    public void FreeTextIsTakenWhereNothingMatchesIt()
    {
        var (picker, answer) = Asking(Voices());

        Type(picker, "some-voice-d47-has-never-heard-of");

        var list = picker.GetControl<ListBox>("Choices");

        Assert.Equal(0, list.ItemCount);
        Assert.Equal(-1, list.SelectedIndex);
        Assert.True(picker.GetControl<Button>("AcceptButton").IsEnabled);

        UseThis(picker);

        Assert.Equal("some-voice-d47-has-never-heard-of", Answered(answer)!.Value);
    }

    /// <summary>An empty box on a free-text row is not free text.</summary>
    [AvaloniaFact]
    public void AnEmptyBoxIsNotAValue()
    {
        var (picker, _) = Asking(Voices() with { Current = null });

        Assert.False(picker.GetControl<Button>("AcceptButton").IsEnabled);

        picker.Close();
    }
}
