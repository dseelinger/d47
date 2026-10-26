using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>An utterance caption goes on the first request only; without one each sentence is captioned.</summary>
public class ALineInAnotherLanguageIsCaptionedOnceTests
{
    private static async Task<List<string?>> CaptionsAsync(string? utteranceCaption)
    {
        var sink = new RecordingAudioSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var heard = new List<AudioActivity>();
        arbiter.ActivityChanged += heard.Add;

        await using (var pipeline = new SpeechPipeline(
            arbiter,
            new FakeTtsProvider(),
            VoiceSelection.Default,
            "announcement",
            NullLogger.Instance,
            utteranceCaption: utteranceCaption))
        {
            pipeline.Push("First sentence. Second sentence. Third sentence.");
            await pipeline.CompleteAsync();
        }

        var captions = new List<string?> { arbiter.Activity.Caption };

        for (var i = 0; i < 2; i++)
        {
            sink.CompleteCurrent();
            captions.Add(arbiter.Activity.Caption);
        }

        return captions;
    }

    [Fact]
    public async Task TheTranslationCaptionsTheFirstSentenceAndNoOtherAsync()
    {
        var captions = await CaptionsAsync("All of it, in English.");

        Assert.Equal("All of it, in English.", captions[0]);
        Assert.Null(captions[1]);
        Assert.Null(captions[2]);
    }

    [Fact]
    public async Task WithoutATranslationEachSentenceIsCaptionedAsBeforeAsync()
    {
        var captions = await CaptionsAsync(null);

        Assert.Equal("First sentence.", captions[0]);
        Assert.Equal("Second sentence.", captions[1]);
        Assert.Equal("Third sentence.", captions[2]);
    }
}
