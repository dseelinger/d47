using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

public class TheClipKeptIsTheClipPlayedTests
{
    private static (AudioArbiter Arbiter, RecordingAudioSink Sink) Build()
    {
        var sink = new RecordingAudioSink();
        return (new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start(), sink);
    }

    [Fact]
    public async Task APipelineAskedToKeepReturnsEverySentenceItQueuedInOrder()
    {
        var (arbiter, _) = Build();
        var tts = new FakeTtsProvider();

        await using var pipeline = new SpeechPipeline(
            arbiter, tts, new VoiceSelection("af_heart"), "announcement", NullLogger.Instance, keep: true);

        pipeline.Push("First line. Second line. ");
        await pipeline.CompleteAsync();

        var kept = Assert.IsType<SpokenClip>(pipeline.Kept);

        Assert.Equal(["First line.", "Second line."], kept.Parts.Select(part => part.Name));
        Assert.Equal("fake", kept.Provider);
        Assert.Equal("af_heart", kept.VoiceId);
    }

    [Fact]
    public async Task APipelineNotAskedToKeepKeepsNothing()
    {
        var (arbiter, _) = Build();

        await using var pipeline = new SpeechPipeline(
            arbiter, new FakeTtsProvider(), VoiceSelection.Default, "turn-1", NullLogger.Instance);

        pipeline.Push("Said and gone. ");
        await pipeline.CompleteAsync();

        Assert.Null(pipeline.Kept);
    }

    [Fact]
    public void PartsInDifferentFormatsAreJoinedInTheStandardOne()
    {
        var spoken = new SpokenClip(
            [
                new AudioClip("a", new byte[480], AudioFormat.Standard),
                new AudioClip("b", new byte[480], new AudioFormat(24_000, 1)),
            ],
            "kokoro",
            null);

        var joined = spoken.Joined("line");

        Assert.Equal(AudioFormat.Standard, joined.Format);
        Assert.Equal(480 + 960, joined.Pcm.Length);
    }
}
