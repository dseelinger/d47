using System.Text.RegularExpressions;

namespace D47.Core.Listening;

/// <summary>
/// Whether a transcription is actually somebody speaking (remediation.md 14, item 8).
/// <para>
/// <b>Whisper describes what it hears when it cannot transcribe it.</b> Handed a stretch of
/// mouse clicks it does not return nothing — it returns <c>(mouse clicking)</c>, and handed
/// silence it returns <c>[BLANK_AUDIO]</c>. Those are annotations about the audio rather than
/// words anybody said, and they are not empty, so every check written as "is the text blank"
/// passes them straight through.
/// </para>
/// <para>
/// What that cost: a Commander clicking around with the microphone open got a turn, and d47
/// answered it — <em>"Nothing spoken, Commander. Only hands at work. I'll hold the channel
/// open."</em> — which is a model being asked to reply to a description of the room, doing it
/// gracefully, and spending a turn to say nothing. Worse in a prompt, where the same text would
/// have been committed as the value being asked for.
/// </para>
/// <para>
/// <b>A shape rather than a list of phrases.</b> Enumerating "mouse clicking" would leave
/// "keyboard typing" and the several hundred other things a room does, and none of them is
/// discoverable except by a Commander being talked at. What every one of them has in common is
/// the bracket: the annotation is parenthesised, square-bracketed, asterisked or bounded by
/// musical notes, because that is the convention the training transcripts used. So the rule is
/// that a transcription with nothing outside its brackets is a transcription of no speech.
/// </para>
/// <para>
/// <b>Only when it is the whole of it.</b> "(clears throat) what is my fuel" is somebody asking
/// about their fuel, and the annotation in front of it is not a reason to ignore them — the text
/// is passed on untouched and the model reads past it, which it does well.
/// </para>
/// </summary>
public static partial class SpeechNoise
{
    /// <summary>
    /// Whether this is nothing anybody said: blank, or an annotation and nothing else.
    /// </summary>
    public static bool IsNothingSaid(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        // What is left once every annotation is taken out. Punctuation goes with them: a
        // transcription of "[BLANK_AUDIO]." is the same claim about the room as one without the
        // full stop, and a lone "." is not a question either.
        var remaining = Annotations().Replace(text, string.Empty);

        return !remaining.Any(char.IsLetterOrDigit);
    }

    /// <summary>
    /// The bracketed forms, non-greedy and without nesting, so the pattern cannot run away on a
    /// line of open brackets. Musical notes are their own case: Whisper marks music with them
    /// and does not always close the pair.
    /// </summary>
    [GeneratedRegex(@"\([^()]*\)|\[[^\[\]]*\]|\*[^*]*\*|[♪♫♬♩]+")]
    private static partial Regex Annotations();
}
