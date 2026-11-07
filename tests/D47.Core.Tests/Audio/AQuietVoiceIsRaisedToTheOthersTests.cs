using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary><see cref="SpeechLeveller"/> on generated speech: words of tone with silence between them.</summary>
public class AQuietVoiceIsRaisedToTheOthersTests
{
    private const int Rate = 48_000;

    /// <summary>One word: 300 ms, six measured frames.</summary>
    private const int WordSamples = Rate * 300 / 1000;

    /// <summary>The silence after it: 200 ms.</summary>
    private const int GapSamples = Rate * 200 / 1000;

    private const int Words = 10;

    private static double Db(double rms) => 20 * Math.Log10(rms);

    private static double Linear(double dbfs) => Math.Pow(10, dbfs / 20);

    /// <summary>
    /// Words of a 300 Hz tone at <paramref name="voicedDbfs"/> RMS, with 5 ms edges, and noise at <paramref name="silenceDbfs"/>
    /// RMS between them. A spike of <paramref name="spikeDbfs"/> peak lands every 100 ms within a word when given.
    /// </summary>
    private static AudioClip Speech(double voicedDbfs, double silenceDbfs = -120, double? spikeDbfs = null)
    {
        var amplitude = Linear(voicedDbfs) * Math.Sqrt(2);

        // Uniform noise on ±a has an RMS of a/√3.
        var noise = Linear(silenceDbfs) * Math.Sqrt(3);
        var edge = Rate * 5 / 1000;
        var random = new Random(47);
        var samples = new List<double>();

        for (var word = 0; word < Words; word++)
        {
            for (var at = 0; at < WordSamples; at++)
            {
                var envelope = Math.Min(1, Math.Min(at, WordSamples - 1 - at) / (double)edge);
                var sample = amplitude * envelope * Math.Sin(2 * Math.PI * 300 * at / Rate);

                if (spikeDbfs is { } spike && at % (Rate / 10) is >= 2_000 and < 2_048)
                {
                    sample = Linear(spike) * Math.Sin(2 * Math.PI * (at - 2_000) / 48);
                }

                samples.Add(sample);
            }

            for (var at = 0; at < GapSamples; at++)
            {
                samples.Add(noise * ((random.NextDouble() * 2) - 1));
            }
        }

        var pcm = new byte[samples.Count * 2];

        for (var index = 0; index < samples.Count; index++)
        {
            var value = (short)Math.Clamp(Math.Round(samples[index] * 32768), short.MinValue, short.MaxValue);
            pcm[index * 2] = (byte)(value & 0xFF);
            pcm[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return new AudioClip("speech", pcm, AudioFormat.Standard);
    }

    private static double[] Samples(AudioClip clip)
    {
        var pcm = clip.Pcm.Span;
        var samples = new double[pcm.Length / 2];

        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = (short)(pcm[index * 2] | (pcm[(index * 2) + 1] << 8)) / 32768.0;
        }

        return samples;
    }

    /// <summary>RMS over 50 ms frames, counting only frames above −45 dBFS, in dBFS.</summary>
    private static double VoicedDbfs(AudioClip clip)
    {
        var samples = Samples(clip);
        const int frame = Rate / 20;
        var gate = Linear(-45);
        var power = 0.0;
        var voiced = 0;

        for (var start = 0; start + frame <= samples.Length; start += frame)
        {
            var mean = samples.AsSpan(start, frame).ToArray().Sum(sample => sample * sample) / frame;

            if (Math.Sqrt(mean) >= gate)
            {
                power += mean;
                voiced++;
            }
        }

        return Db(Math.Sqrt(power / voiced));
    }

    [Fact]
    public void AVoiceAtMinusThirtyTwoComesOutAtMinusEighteen()
    {
        var levelled = SpeechLeveller.Apply(Speech(-32));

        Assert.InRange(VoicedDbfs(levelled), -19, -17);
    }

    [Fact]
    public void AVoiceAboveTheTargetIsLeftAlone()
    {
        var speech = Speech(-15);
        var levelled = SpeechLeveller.Apply(speech);

        Assert.InRange(VoicedDbfs(levelled) - VoicedDbfs(speech), -0.5, 0.5);
    }

    [Fact]
    public void TheSilenceBetweenWordsIsNotRaised()
    {
        // Noise at −76 dBFS stays under −60 at the +14 dB this voice is given, and would not at the +18 dB the gain
        // reaches if it rose through the gaps.
        var samples = Samples(SpeechLeveller.Apply(Speech(-32, silenceDbfs: -76)));
        var margin = Rate * 20 / 1000;

        for (var word = 0; word < Words; word++)
        {
            var gap = (word * (WordSamples + GapSamples)) + WordSamples + margin;
            var middle = samples.AsSpan(gap, GapSamples - (2 * margin)).ToArray();
            var rms = Math.Sqrt(middle.Sum(sample => sample * sample) / middle.Length);

            Assert.True(Db(rms) < -60, $"the silence after word {word + 1} is at {Db(rms):0.0} dBFS");
        }
    }

    [Fact]
    public void NoRaisedSampleReachesMinusOneDbfs()
    {
        var speech = Speech(-31.6, spikeDbfs: -10);
        var levelled = Samples(SpeechLeveller.Apply(speech));

        Assert.True(Samples(speech).Max(Math.Abs) * SpeechLeveller.MostGain > 1, "the spikes would not have reached full scale");
        Assert.True(levelled.Max(Math.Abs) < Linear(-1), $"a sample reached {Db(levelled.Max(Math.Abs)):0.00} dBFS");
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(1_001)]
    public void StreamingInChunksGivesTheSameBytesAsTheWholeClip(int chunk)
    {
        var speech = Speech(-32, silenceDbfs: -70, spikeDbfs: -10);
        var pcm = speech.Pcm.ToArray();
        var leveller = new SpeechLeveller();
        var streamed = new List<byte>();

        for (var at = 0; at < pcm.Length; at += chunk)
        {
            streamed.AddRange(leveller.Push(pcm.AsSpan(at, Math.Min(chunk, pcm.Length - at))));
        }

        streamed.AddRange(leveller.Finish());

        Assert.Equal(SpeechLeveller.Apply(speech).Pcm.ToArray(), streamed);
    }

    [Fact]
    public void AVoicesNextClipStartsAtTheGainItsLastOneEndedAt()
    {
        var levels = new SpeechLevels();
        // The first measured frame, 50 ms, in bytes.
        var opening = Rate / 20 * 2;

        var first = levels.For("elevenlabs", "quiet").Run(Speech(-32));
        var second = levels.For("elevenlabs", "quiet").Run(Speech(-32));
        var other = levels.For("elevenlabs", "another").Run(Speech(-32));

        static AudioClip Opening(AudioClip clip, int bytes) => clip with { Pcm = clip.Pcm[..bytes] };

        Assert.True(VoicedDbfs(Opening(first, opening)) < -20, "a fresh voice was already raised in its first syllable");
        Assert.InRange(VoicedDbfs(Opening(second, opening)), -19, -17);
        Assert.True(VoicedDbfs(Opening(other, opening)) < -20, "another voice started where this one ended");
    }

    [Fact]
    public async Task APipelineLevelsEveryClipBeforeItsColour()
    {
        var sink = new RecordingAudioSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var tts = new StreamingTtsProvider();
        var speech = Speech(-32);
        var coloured = new List<AudioClip>();

        await using var pipeline = new SpeechPipeline(
            arbiter,
            tts,
            VoiceSelection.Default,
            "turn-1",
            NullLogger.Instance,
            colour: clip =>
            {
                coloured.Add(clip);
                return clip;
            },
            keep: true,
            levels: new SpeechLevels());

        pipeline.Push("You are in Sol. ");
        var arriving = await tts.ClipFor("You are in Sol.");
        arriving.Append(speech.Pcm.Span);
        arriving.Complete();
        await pipeline.CompleteAsync();

        var expected = SpeechLeveller.Apply(speech).Pcm.ToArray();

        Assert.Equal(expected, Assert.Single(coloured).Pcm.ToArray());
        Assert.Equal(expected, Assert.Single(pipeline.Kept!.Parts).Pcm.ToArray());
    }

    [Fact]
    public async Task APipelineLevelsAClipWhileItArrivesAheadOfTheRunningFilter()
    {
        var sink = new RecordingAudioSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var tts = new StreamingTtsProvider();
        var speech = Speech(-32);
        var pcm = speech.Pcm.ToArray();

        await using var pipeline = new SpeechPipeline(
            arbiter,
            tts,
            VoiceSelection.Default,
            "turn-1",
            NullLogger.Instance,
            colour: CovasVoice.Apply,
            guardianTreated: true,
            keep: true,
            running: CovasVoice.Filter,
            levels: new SpeechLevels());

        pipeline.Push("You are in Sol. ");
        var arriving = await tts.ClipFor("You are in Sol.");
        arriving.Append(pcm.AsSpan(0, 9_601));

        await StreamingTtsProvider.Eventually(() => sink.Started.Count == 1, "the group was never queued");
        Assert.NotNull(sink.Started[0].Arriving);

        arriving.Append(pcm.AsSpan(9_601));
        arriving.Complete();
        await pipeline.CompleteAsync();

        Assert.Equal(
            CovasVoice.Apply(SpeechLeveller.Apply(speech)).Pcm.ToArray(),
            Assert.Single(pipeline.Kept!.Parts).Pcm.ToArray());
    }
}
