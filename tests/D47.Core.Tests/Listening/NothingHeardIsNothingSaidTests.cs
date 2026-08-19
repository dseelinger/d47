using D47.Core.Interface;
using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// Whisper describes what it hears when it cannot transcribe it (remediation.md 14, item 8).
/// <para>
/// Reported from a running d47: a Commander clicking around with the microphone open got
/// <c>(mouse clicking)</c> in the transcript and an answer to it — <em>"Nothing spoken,
/// Commander. Only hands at work. I'll hold the channel open."</em> That is a turn spent replying
/// to a description of the room, and it passed every check written as "is the text blank" because
/// it is not blank.
/// </para>
/// </summary>
public class NothingHeardIsNothingSaidTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("(mouse clicking)")]
    [InlineData("[BLANK_AUDIO]")]
    [InlineData("[ Silence ]")]
    [InlineData("(keyboard typing)")]
    [InlineData("(wind blowing)")]
    [InlineData("*clears throat*")]
    [InlineData("♪♪♪")]
    [InlineData("(music playing) [BLANK_AUDIO]")]

    // Punctuation goes with the annotation: "[BLANK_AUDIO]." is the same claim about the room,
    // and a lone full stop is not a question either.
    [InlineData("[BLANK_AUDIO].")]
    [InlineData("...")]
    public void AnAnnotationAndNothingElseIsNothingSaid(string text)
    {
        Assert.True(SpeechNoise.IsNothingSaid(text), text);
        Assert.True(new Transcription(text).IsEmpty, text);
    }

    /// <summary>
    /// <b>Only when it is the whole of it.</b> Somebody who clears their throat and then asks
    /// about their fuel has asked about their fuel, and the text goes on untouched — the model
    /// reads past the annotation, which it does well.
    /// </summary>
    [Theory]
    [InlineData("what is my fuel")]
    [InlineData("(clears throat) what is my fuel")]
    [InlineData("plot a route to Colonia [inaudible]")]
    [InlineData("set a timer for 5 minutes")]

    // A word that merely contains brackets is still a word.
    [InlineData("Colonia (the one out there)")]
    public void SomethingSaidIsStillSomethingSaid(string text)
    {
        Assert.False(SpeechNoise.IsNothingSaid(text), text);
        Assert.False(new Transcription(text).IsEmpty, text);
    }

    /// <summary>
    /// And the same in a prompt, where it matters more: this is the answer to a question the panel
    /// asked, so committing it would write "(mouse clicking)" into a plan rather than merely
    /// spending a turn on it.
    /// </summary>
    [Fact]
    public void APromptTreatsAnAnnotationAsHavingHeardNothing()
    {
        var fallback = TextEntryLoop.Judge(
            new Heard("(mouse clicking)", Confidence: 1, Final: true),
            validate: null,
            out var verdict);

        Assert.Equal(EntryFallback.NothingHeard, fallback);
        Assert.Null(verdict);

        // Which puts the keyboard back saying so, rather than accepting the room as a value.
        Assert.Equal("I did not catch that.", TextEntryLoop.Explain(fallback!.Value, null));
    }

    /// <summary>A real answer to the same prompt is still accepted, or the guard is a wall.</summary>
    [Fact]
    public void APromptStillTakesAnAnswer()
    {
        Assert.Null(TextEntryLoop.Judge(
            new Heard("Deciat", Confidence: 1, Final: true),
            validate: null,
            out _));
    }

    /// <summary>
    /// A pattern that could run away on a line of open brackets does not. Nothing produces this,
    /// and the cost of finding out the hard way is a hung transcription.
    /// </summary>
    [Fact]
    public void ALineOfBracketsIsAnsweredQuickly()
    {
        var pathological = new string('(', 2000) + new string(')', 2000);

        var started = System.Diagnostics.Stopwatch.StartNew();

        SpeechNoise.IsNothingSaid(pathological);

        Assert.True(started.ElapsedMilliseconds < 250, $"took {started.ElapsedMilliseconds} ms");
    }
}
