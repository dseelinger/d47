using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Windowing;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// <b>Use this</b> takes the row the Commander can see, or it is shut
/// (<a href="https://github.com/dseelinger/d47/issues/190">#190</a>).
/// <para>
/// Reported as the button not working. It had two faces. On a closed vocabulary — microphones,
/// personas — a keystroke that filtered the highlighted row away left nothing selected, and
/// <c>Accept</c> fell off the end of itself: no close, no result, no message, from a button that
/// was still lit. On a free-text row — voices, models — the same dropped highlight sent the
/// second branch, which wrote <em>the raw contents of the search box</em> into the setting as an
/// id, and the row's derived caption then could not name it. Two mechanisms, one complaint.
/// </para>
/// <para>
/// <b>Driven through the dialog rather than the window</b>, because what is being asserted is
/// what the picker <em>answers</em> — a window that closed and a window that answered nothing are
/// the same thing to look at, and telling them apart is the whole bug.
/// </para>
/// </summary>
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

    /// <summary>
    /// The picker on screen over an owner, with the answer still in flight. Turning the
    /// dispatcher is the whole wait: everything here happens on it.
    /// </summary>
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

    /// <summary>
    /// What the picker answered, <b>read rather than awaited</b>. Everything here happens on the
    /// dispatcher, so turning it is the whole wait — and with the defect in place the button is
    /// inert and the dialog never answers at all, which an await would turn into a suite that
    /// hangs rather than one that fails. That is not hypothetical: it is what these did the first
    /// time they were run against the old code.
    /// </summary>
    private static PickerResult? Answered(Task<PickerResult?> answer)
    {
        Dispatcher.UIThread.RunJobs();

        Assert.True(answer.IsCompleted, "the picker did not answer");

        return answer.Result;
    }

    /// <summary>
    /// Typing past the highlighted row moves the highlight to the top match, so the obvious row
    /// is the one taken. The facet path has had this fixup since #146; the text path, which is
    /// the one every Commander types into, never got it.
    /// </summary>
    [AvaloniaFact]
    public void TypingPastTheHighlightMovesItToTheTopMatch()
    {
        var (picker, answer) = Asking(Voices());
        var list = picker.GetControl<ListBox>("Choices");

        // Opened on the current value, which is what makes Enter with no typing keep it.
        Assert.Equal(0, list.SelectedIndex);

        // And now a filter that excludes it. This is the keystroke that used to leave nothing
        // highlighted and the button lit.
        Type(picker, "sonia");

        Assert.Equal(1, list.ItemCount);
        Assert.Equal(0, list.SelectedIndex);
        Assert.Equal("en-GB-SoniaNeural", ((PickerChoice)list.SelectedItem!).Value);

        UseThis(picker);

        // The row, and not the word "sonia" written into the setting as a voice id.
        Assert.Equal("en-GB-SoniaNeural", Answered(answer)!.Value);
    }

    /// <summary>
    /// A row still selected after a keystroke is the Commander's own and is not moved — the
    /// fixup puts a highlight back, it does not take one over.
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

    /// <summary>
    /// <b>The button is shut rather than lit and inert.</b> A closed-vocabulary picker opened on a
    /// row with nothing stored has nothing to take, and said so nowhere: <b>Use this</b> was
    /// enabled on there being rows at all, which is a question about the list where
    /// <c>Accept</c> asks about the selection.
    /// </summary>
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
    /// Typing into a closed vocabulary still ends somewhere pressable, which is the reported
    /// case: type, see one row, take it.
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
    /// And free text still reaches the setting where it is meant to — a value the catalogue does
    /// not list, typed in full, matching nothing. That is the case the second branch of
    /// <c>Accept</c> exists for; what it used to do as well was fire on every dropped highlight.
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

    /// <summary>
    /// An empty box on a free-text row is not free text. Nothing typed and nothing chosen is
    /// nothing to take, whatever the row allows.
    /// </summary>
    [AvaloniaFact]
    public void AnEmptyBoxIsNotAValue()
    {
        var (picker, _) = Asking(Voices() with { Current = null });

        Assert.False(picker.GetControl<Button>("AcceptButton").IsEnabled);

        picker.Close();
    }
}
