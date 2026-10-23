using System.Net;
using System.Text;
using D47.Core.Audio;
using Microsoft.Extensions.Logging;
using NAudio.Utils;
using NAudio.Wave;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>An ElevenLabs free sample decodes whether it is a WAV or an MP3, at whatever sample rates its frames use (#1541).</summary>
public class ASampleThatChangesRateStillPlaysTests
{
    private const int FrameSamples = 1152;

    /// <summary>Silent MPEG-1 Layer III mono frames at 128 kbit/s.</summary>
    private static byte[] Frames(int sampleRate, int count)
    {
        var (rateBits, length) = sampleRate switch
        {
            44100 => (0, 417),
            48000 => (1, 384),
            32000 => (2, 576),
            _ => throw new ArgumentOutOfRangeException(nameof(sampleRate)),
        };

        var frame = new byte[length];
        frame[0] = 0xFF;
        frame[1] = 0xFB;
        frame[2] = (byte)(0x90 | (rateBits << 2));
        frame[3] = 0xC0;

        return [.. Enumerable.Range(0, count).SelectMany(_ => frame)];
    }

    private static TimeSpan Playing(int sampleRate, int count) =>
        TimeSpan.FromSeconds((double)count * FrameSamples / sampleRate);

    private static TimeSpan Duration(byte[] pcm) => new AudioClip("sample", pcm, AudioFormat.Standard).Duration;

    [Theory]
    [InlineData(32000)]
    [InlineData(44100)]
    [InlineData(48000)]
    public void ASampleAtOneRateDecodes(int sampleRate)
    {
        var pcm = ElevenLabsTtsProvider.DecodePreview(Frames(sampleRate, 80));

        Assert.NotEmpty(pcm);
        Assert.InRange(
            Duration(pcm).TotalSeconds,
            Playing(sampleRate, 79).TotalSeconds,
            Playing(sampleRate, 81).TotalSeconds);
    }

    [Fact]
    public void ASampleThatDropsFrom48000To44100AndBackKeepsAllItsAudio()
    {
        byte[] mp3 = [.. Frames(48000, 40), .. Frames(44100, 40), .. Frames(48000, 40)];

        var pcm = ElevenLabsTtsProvider.DecodePreview(mp3);

        var expected = Playing(48000, 80) + Playing(44100, 40);
        var oneFrame = Playing(44100, 1);

        Assert.InRange(
            Duration(pcm).TotalSeconds,
            (expected - oneFrame).TotalSeconds,
            (expected + oneFrame).TotalSeconds);
    }

    /// <summary>A second of 16-bit PCM in a RIFF/WAVE container.</summary>
    private static byte[] Wave(int sampleRate, int channels)
    {
        using var stream = new MemoryStream();

        using (var writer = new WaveFileWriter(new IgnoreDisposeStream(stream), new WaveFormat(sampleRate, 16, channels)))
        {
            var samples = new short[sampleRate * channels];

            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = (short)(8000 * Math.Sin(i * 2 * Math.PI * 440 / (sampleRate * channels)));
            }

            writer.WriteSamples(samples, 0, samples.Length);
        }

        return stream.ToArray();
    }

    [Theory]
    [InlineData(48000, 1)]
    [InlineData(44100, 2)]
    public void ASampleThatIsAWaveDecodes(int sampleRate, int channels)
    {
        var pcm = ElevenLabsTtsProvider.DecodePreview(Wave(sampleRate, channels));

        Assert.InRange(Duration(pcm).TotalSeconds, 0.99, 1.01);
        Assert.Contains(pcm, b => b != 0);
    }

    private sealed class Recorded : ILogger<ElevenLabsTtsProvider>
    {
        public List<(LogLevel Level, string Line)> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add((logLevel, formatter(state, exception)));
    }

    private sealed class Serves : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(
                request.RequestUri!.AbsolutePath.EndsWith("/v1/voices", StringComparison.Ordinal)
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            """{"voices":[{"voice_id":"zain1","name":"Zain","preview_url":"https://storage.googleapis.com/s.mp3"}]}""",
                            Encoding.UTF8,
                            "application/json"),
                    }
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
    }

    [Fact]
    public async Task ASampleThatCannotBePlayedIsLoggedOnceWithTheVoiceAndItsId()
    {
        var log = new Recorded();
        using var http = new HttpClient(new Serves());
        using var provider = new ElevenLabsTtsProvider(() => "sk_test", log, http);

        var failure = await Assert.ThrowsAsync<TtsException>(
            () => provider.PreviewAsync("zain1", TestContext.Current.CancellationToken));

        var warning = Assert.Single(log.Lines, line => line.Level == LogLevel.Warning && line.Line.Contains("sample"));

        Assert.Contains("Zain", warning.Line);
        Assert.Contains("zain1", warning.Line);
        Assert.Contains(failure.InnerException!.Message, warning.Line);
        Assert.StartsWith("ElevenLabs' sample of Zain could not be played", failure.Message);
    }
}
