using System.Reflection;
using D47.Stt;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The bias list reaches the recogniser.</summary>
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
    /// Comma-separated, which is how an initial prompt is meant to carry a vocabulary: Whisper reads it
    /// as text that came just before the audio, and a list of names is exactly the context that makes
    /// the next name likelier.
    /// </summary>
    [Fact]
    public void TheNamesBecomeAVocabulary()
    {
        Assert.Equal(
            "Shinrarta Dezhra, Jameson Memorial, Lei Cheung",
            WhisperTranscriber.Vocabulary(["Shinrarta Dezhra", "Jameson Memorial", "Lei Cheung"]));
    }

    [Fact]
    public void NoNamesIsNoPrompt()
    {
        Assert.Null(WhisperTranscriber.Vocabulary([]));
        Assert.Null(WhisperTranscriber.Vocabulary(["", "   "]));
    }

    /// <summary>A Commander whose ship has no name should not spend prompt on the fact.</summary>
    [Fact]
    public void BlanksAreDroppedRatherThanJoined()
    {
        Assert.Equal("Sol, Lei Cheung", WhisperTranscriber.Vocabulary(["Sol", "  ", "Lei Cheung"]));
    }

    /// <summary>
    /// The ordinary state on a fresh install: no model on disk, and the transcriber asked for a
    /// transcription anyway.
    /// </summary>
    [Fact]
    public void PrimingWithNoModelLoadedIsHarmless()
    {
        using var transcriber = new WhisperTranscriber(NullLogger<WhisperTranscriber>.Instance);

        Prime(transcriber, "Lei Cheung", "Shinrarta Dezhra");

        Assert.Null(PromptOf(transcriber));
    }
}
