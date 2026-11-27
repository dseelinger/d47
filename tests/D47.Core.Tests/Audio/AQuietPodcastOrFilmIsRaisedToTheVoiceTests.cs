using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary><see cref="StreamLeveller"/> on generated stereo audio, and the arbiter applying it to podcasts and films only.</summary>
public class AQuietPodcastOrFilmIsRaisedToTheVoiceTests
{
    private const int Rate = 48_000;

    private static double Linear(double dbfs) => Math.Pow(10, dbfs / 20);

    private static double Db(double rms) => 20 * Math.Log10(rms);

    /// <summary>Stereo 16-bit PCM: a 300 Hz tone at <paramref name="dbfs"/> RMS on the left, a 450 Hz one on the right scaled by <paramref name="rightScale"/>.</summary>
    private static byte[] Tone(double dbfs, double seconds, double rightScale = 1)
    {
        var amplitude = Linear(dbfs) * Math.Sqrt(2);
        var frames = (int)(Rate * seconds);
        var pcm = new byte[frames * 4];

        for (var at = 0; at < frames; at++)
        {
            Put(pcm, at * 4, amplitude * Math.Sin(2 * Math.PI * 300 * at / Rate));
            Put(pcm, (at * 4) + 2, amplitude * rightScale * Math.Sin(2 * Math.PI * 450 * at / Rate));
        }

        return pcm;
    }

    private static void Put(byte[] pcm, int at, double sample)
    {
        var value = (short)Math.Clamp(Math.Round(sample * 32768), short.MinValue, short.MaxValue);

        pcm[at] = (byte)(value & 0xFF);
        pcm[at + 1] = (byte)((value >> 8) & 0xFF);
    }

    /// <summary>The RMS of one channel (0 left, 1 right) of stereo PCM from byte <paramref name="from"/> to the end.</summary>
    private static double Rms(byte[] pcm, int from, int channel = -1)
    {
        double sum = 0;
        var count = 0;

        for (var at = from; at + 3 < pcm.Length; at += 4)
        {
            for (var side = 0; side < 2; side++)
            {
                if (channel >= 0 && channel != side)
                {
                    continue;
                }

                var sample = (short)(pcm[at + (side * 2)] | (pcm[at + (side * 2) + 1] << 8)) / 32768.0;

                sum += sample * sample;
                count++;
            }
        }

        return Math.Sqrt(sum / count);
    }

    private static byte[] ReadAll(IPcmStream stream, int chunk)
    {
        var all = new List<byte>();
        var buffer = new byte[chunk];

        while (stream.Read(buffer) is var read and > 0)
        {
            all.AddRange(buffer.AsSpan(0, read).ToArray());
        }

        return [.. all];
    }

    [Fact]
    public void ThirtyDbBelowFullScaleSettlesWithinADecibelOfTheSpeechTarget()
    {
        var input = Tone(-30, 30);

        var output = ReadAll(new StreamLeveller(new MemoryPcmStream(input)), 4_096);

        Assert.InRange(Db(Rms(output, input.Length - (Rate * 4 * 5))), -19, -17);
    }

    [Fact]
    public void ALoudStreamComesOutByteForByte()
    {
        var input = Tone(-12, 12);

        var output = ReadAll(new StreamLeveller(new MemoryPcmStream(input)), 4_096);

        Assert.Equal(input, output);
    }

    [Fact]
    public void NoRaisedSampleGoesOverTheCeiling()
    {
        var input = Tone(-30, 20);

        for (var at = Rate * 4 * 5; at + 1 < input.Length; at += 4 * 4_800)
        {
            Put(input, at, 0.5);
        }

        var output = ReadAll(new StreamLeveller(new MemoryPcmStream(input)), 4_096);
        var loudest = 0.0;

        for (var at = 0; at < output.Length; at += 2)
        {
            loudest = Math.Max(loudest, Math.Abs((short)(output[at] | (output[at + 1] << 8)) / 32768.0));
        }

        Assert.True(loudest <= SpeechLeveller.Ceiling + (1 / 32768.0), $"peak {loudest}");
        Assert.True(loudest > 0.8, $"peak {loudest}");
    }

    [Fact]
    public void BothChannelsGetTheSameGain()
    {
        var input = Tone(-30, 20, rightScale: 0.5);

        var output = ReadAll(new StreamLeveller(new MemoryPcmStream(input)), 4_096);
        var from = input.Length - (Rate * 4 * 4);

        var leftGain = Rms(output, from, 0) / Rms(input, from, 0);
        var rightGain = Rms(output, from, 1) / Rms(input, from, 1);

        Assert.InRange(leftGain, 2, SpeechLeveller.MostGain);
        Assert.Equal(leftGain, rightGain, 2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(5_000)]
    [InlineData(100_000)]
    public void TheOutputIsAsLongAsTheInputAndTheSameWhateverTheReadSize(int chunk)
    {
        var input = Tone(-30, 3);
        var whole = ReadAll(new StreamLeveller(new MemoryPcmStream(input)), 1_000_000);

        var cut = ReadAll(new StreamLeveller(new MemoryPcmStream(input)), chunk);

        Assert.Equal(input.Length, whole.Length);
        Assert.Equal(whole, cut);
    }

    [Fact]
    public void AnInnerReadThatEndsPartwayThroughAFrameIsLevelledTheSame()
    {
        var input = Tone(-30, 3);
        var whole = ReadAll(new StreamLeveller(new MemoryPcmStream(input)), 1_000_000);

        var cut = ReadAll(new StreamLeveller(new MemoryPcmStream(input) { MostPerRead = 3 }), 4_096);

        Assert.Equal(whole, cut);
    }

    [Fact]
    public void ATrailingPartialFrameIsPassedOnAsItIs()
    {
        var input = Tone(-30, 1).Concat(new byte[] { 1, 2, 3 }).ToArray();

        var output = ReadAll(new StreamLeveller(new MemoryPcmStream(input)), 4_096);

        Assert.Equal(input.Length, output.Length);
        Assert.Equal(new byte[] { 1, 2, 3 }, output[^3..]);
    }

    [Fact]
    public void LengthAndSeekPassThroughAndTheGainSurvivesASeek()
    {
        var inner = new MemoryPcmStream(Tone(-30, 20));
        var leveller = new StreamLeveller(inner);

        Assert.Equal(inner.Length, leveller.Length);
        Assert.True(leveller.CanSeek);

        var first = new byte[Rate * 4 * 10];
        Assert.Equal(first.Length, ReadFully(leveller, first));

        leveller.Seek(TimeSpan.FromSeconds(2));

        Assert.Equal(TimeSpan.FromSeconds(2), inner.LastSeek);

        var after = new byte[Rate * 4];
        Assert.Equal(after.Length, ReadFully(leveller, after));
        Assert.InRange(Db(Rms(after, 0)), -19, -17);
    }

    [Fact]
    public void AStreamThatCannotSeekIsNotSoughtThrough()
    {
        var inner = new MemoryPcmStream(Tone(-30, 1)) { Seekable = false };
        var leveller = new StreamLeveller(inner);

        leveller.Seek(TimeSpan.FromSeconds(1));

        Assert.False(leveller.CanSeek);
        Assert.Null(inner.LastSeek);
    }

    [Fact]
    public void TheLevelIsAveragedOverTenSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50 * StreamLeveller.SettledFrames));
    }

    [Fact]
    public void ThePodcastAndFilmSlotsAreLevelled()
    {
        var sink = new RecordingAudioSink();
        var audio = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var quiet = Tone(-30, 12);

        audio.PlayPodcast(new MusicTrack("episode", () => new MemoryPcmStream(quiet)), TimeSpan.Zero);
        audio.PlayVideo(new MusicTrack("film", () => new MemoryPcmStream(quiet)), TimeSpan.Zero);

        var tracks = sink.Started.Where(request => request.Track is not null).ToList();

        Assert.Equal(2, tracks.Count);

        foreach (var request in tracks)
        {
            using var opened = request.Track!.Open();
            var output = ReadAll(opened, 4_096);

            Assert.NotEqual(quiet, output);
            Assert.InRange(Db(Rms(output, output.Length - (Rate * 4 * 2))), -19, -17);
        }
    }

    private static int ReadFully(IPcmStream stream, byte[] buffer)
    {
        var total = 0;

        while (total < buffer.Length && stream.Read(buffer.AsSpan(total)) is var read and > 0)
        {
            total += read;
        }

        return total;
    }

    private sealed class MemoryPcmStream(byte[] pcm) : IPcmStream
    {
        private int _at;

        public int MostPerRead { get; init; } = int.MaxValue;

        public bool Seekable { get; init; } = true;

        public TimeSpan? LastSeek { get; private set; }

        public TimeSpan? Length => TimeSpan.FromSeconds(pcm.Length / (double)(Rate * 4));

        public bool CanSeek => Seekable;

        public int Read(Span<byte> buffer)
        {
            var count = Math.Min(Math.Min(buffer.Length, MostPerRead), pcm.Length - _at);

            pcm.AsSpan(_at, count).CopyTo(buffer);
            _at += count;
            return count;
        }

        public void Seek(TimeSpan to)
        {
            LastSeek = to;
            _at = (int)(to.TotalSeconds * Rate) * 4;
        }

        public void Dispose()
        {
        }
    }
}
