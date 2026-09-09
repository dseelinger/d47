using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>What the render tap cannot know.</summary>
public class TheRecorderIsToldWhatRenderedTests
{
    /// <summary>A provider that speaks phonemes, and counts how often it was asked for them.</summary>
    private sealed class PhonemeSpeakingProvider : ITtsProvider
    {
        public string Id => "phonemes";

        public string Name => "A provider that speaks phonemes";

        public int PhonemesAsked { get; private set; }

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Silent);

        public Task<AudioClip> SynthesizeAsync(
            string text,
            VoiceSelection voice,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AudioClip(text, new byte[16], AudioFormat.Standard));

        public string? Phonemes(string text, VoiceSelection voice)
        {
            PhonemesAsked++;
            return $"/{text}/";
        }
    }

    private static AudioArbiter Arbiter() =>
        new AudioArbiter(new RecordingAudioSink(), NullLogger<AudioArbiter>.Instance).Start();

    [Fact]
    public async Task Every_sentence_is_reported_with_its_provider_voice_and_phonemes()
    {
        var notes = new List<SynthesisNote>();
        var tts = new PhonemeSpeakingProvider();

        await using (var pipeline = new SpeechPipeline(
            Arbiter(),
            tts,
            new VoiceSelection("af_heart") { Name = "Heart" },
            "turn-1",
            NullLogger.Instance,
            noted: notes.Add))
        {
            pipeline.Push("You are in Sol. Fuel is fine. ");
            await pipeline.CompleteAsync();
        }

        Assert.Equal(2, notes.Count);
        Assert.Equal("You are in Sol.", notes[0].Text);
        Assert.Equal("A provider that speaks phonemes", notes[0].Provider);
        Assert.Equal("Heart (af_heart)", notes[0].Voice);
        Assert.Equal("/You are in Sol./", notes[0].Phonemes);
        Assert.Equal("Fuel is fine.", notes[1].Text);
    }

    /// <summary>
    /// A provider that takes text has nothing true to say here, and says nothing rather than inventing
    /// a transcription nobody spoke.
    /// </summary>
    [Fact]
    public async Task A_provider_that_takes_text_reports_no_phonemes()
    {
        var notes = new List<SynthesisNote>();

        await using (var pipeline = new SpeechPipeline(
            Arbiter(),
            new FakeTtsProvider(),
            VoiceSelection.Default,
            "turn-1",
            NullLogger.Instance,
            noted: notes.Add))
        {
            pipeline.Push("You are in Sol. ");
            await pipeline.CompleteAsync();
        }

        var note = Assert.Single(notes);
        Assert.Null(note.Phonemes);
        Assert.Equal("the provider's own voice", note.Voice);
    }

    [Fact]
    public async Task Nothing_is_asked_for_when_nobody_is_recording()
    {
        var tts = new PhonemeSpeakingProvider();

        await using (var pipeline = new SpeechPipeline(
            Arbiter(),
            tts,
            VoiceSelection.Default,
            "turn-1",
            NullLogger.Instance))
        {
            pipeline.Push("You are in Sol. ");
            await pipeline.CompleteAsync();
        }

        Assert.Equal(0, tts.PhonemesAsked);
    }
}
