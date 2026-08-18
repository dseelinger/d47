using System.Reflection;
using D47.Stt;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The bias list reaches the recogniser (remediation.md 10, item 17).
/// <para>
/// <b>This is the gap the reported mishearing was actually in.</b> <c>properNouns</c> has been a
/// parameter of <c>TranscribeAsync</c> since Phase 6; the list has been built from the journal,
/// capped, and handed over on every utterance the whole time — and then counted in a log line and
/// dropped on the floor. "Transcribed 2.4s of audio in 310ms with 23 name hints" was written while
/// nothing was biased by anything, which is the worst shape a gap can have: it reports as working.
/// </para>
/// <para>
/// Asserted without the weights, which are a download that is not present in CI. What can be
/// checked without them is what was actually wrong — whether the names are turned into a prompt at
/// all — and that priming a transcriber with no model is harmless rather than a crash, since that
/// is the ordinary state on a fresh install.
/// </para>
/// </summary>
public class TheNamesReachWhisperTests
{
    private static string? PromptOf(WhisperTranscriber transcriber) =>
        (string?)typeof(WhisperTranscriber)
            .GetField("_prompt", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(transcriber);

    private static void Prime(WhisperTranscriber transcriber, params string[] names) =>
        typeof(WhisperTranscriber)
            .GetMethod("Prime", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(transcriber, [(IReadOnlyList<string>)names]);

    /// <summary>
    /// Comma-separated, which is how an initial prompt is meant to carry a vocabulary: Whisper
    /// reads it as text that came just before the audio, and a list of names is exactly the
    /// context that makes the next name likelier.
    /// </summary>
    [Fact]
    public void TheNamesBecomeAVocabulary()
    {
        Assert.Equal(
            "Shinrarta Dezhra, Jameson Memorial, Lei Cheung",
            WhisperTranscriber.Vocabulary(["Shinrarta Dezhra", "Jameson Memorial", "Lei Cheung"]));
    }

    /// <summary>
    /// No names is no prompt, rather than an empty one. An empty initial prompt is not free — it
    /// is still conditioning the decode on something — and there is nothing to condition on.
    /// </summary>
    [Fact]
    public void NoNamesIsNoPrompt()
    {
        Assert.Null(WhisperTranscriber.Vocabulary([]));
        Assert.Null(WhisperTranscriber.Vocabulary(["", "   "]));
    }

    /// <summary>
    /// A Commander whose ship has no name should not spend prompt on the fact. Blanks are dropped
    /// rather than joined into ", , ".
    /// </summary>
    [Fact]
    public void BlanksAreDroppedRatherThanJoined()
    {
        Assert.Equal("Sol, Lei Cheung", WhisperTranscriber.Vocabulary(["Sol", "  ", "Lei Cheung"]));
    }

    /// <summary>
    /// The ordinary state on a fresh install: no model on disk, and the transcriber asked for a
    /// transcription anyway. Priming has to be a no-op there rather than a crash.
    /// </summary>
    [Fact]
    public void PrimingWithNoModelLoadedIsHarmless()
    {
        using var transcriber = new WhisperTranscriber(NullLogger<WhisperTranscriber>.Instance);

        Prime(transcriber, "Lei Cheung", "Shinrarta Dezhra");

        Assert.Null(PromptOf(transcriber));
    }
}
